using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories.EfCore;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AgriSustain.IntegrationTests;

/// <summary>Opt-in real LocalDB tests; ordinary component runs never touch SQL.</summary>
internal sealed class SqlFactAttribute : FactAttribute
{
    /// <summary>Requires an explicit environment opt-in before any database is created.</summary>
    public SqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("THREE_UC_SQL_TESTS") != "1")
        {
            Skip = "Set THREE_UC_SQL_TESTS=1 to create disposable LocalDB test databases.";
        }
    }
}

/// <summary>Exercises actual SQL persistence using a newly created database for every test.</summary>
public sealed class SqlPersistenceTests
{
    /// <summary>Verifies persisted account transitions, audit, local revocation and fresh-host reads.</summary>
    [SqlFact]
    public async Task Sql_AccountStatusAndAudit_SurviveHostRestart()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        long buyer = await db.NumberAsync("SELECT UserId FROM dbo.[User] WHERE Username='demo3uc_retail'");
        using (var host = new SqlApiFactory(db.ConnectionString))
        using (var admin = host.Client("admin"))
        using (var retail = host.Client("retail-customer"))
        {
            var page = await admin.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users?page=2&pageSize=3");
            Assert.Equal(10, page!.Total);
            Assert.Equal(3, page.Items.Count);
            Assert.Empty((await admin.GetFromJsonAsync<PagedResponse<UserResponse>>("/api/users?keyword=%25"))!.Items);
            Assert.Equal(HttpStatusCode.OK, (await retail.GetAsync("/api/checkout")).StatusCode);
            var result = await admin.PostAsJsonAsync($"/api/users/{buyer}/status-actions",
                new AccountActionRequest("suspend", "SQL persistence test", true));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await retail.GetAsync("/api/checkout")).StatusCode);
        }
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[User] WHERE Username='demo3uc_retail' AND Status='Suspended'"));
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType='User' AND Action='suspend' AND Reason='SQL persistence test' AND TraceId IS NOT NULL"));
        using var restarted = new SqlApiFactory(db.ConnectionString);
        using var client = restarted.Client("admin");
        var response = await client.GetStringAsync($"/api/users/{buyer}");
        Assert.Contains("Suspended", response);
    }

    /// <summary>Verifies SQL category identity/CRUD, all-product reference guards and reversible hiding.</summary>
    [SqlFact]
    public async Task Sql_CategoryCrudAndProductReferences_UsePersistedRows()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        using var host = new SqlApiFactory(db.ConnectionString);
        using var admin = host.Client("admin");
        var page = await admin.GetFromJsonAsync<PagedResponse<CategoryResponse>>("/api/categories?page=2&pageSize=2");
        Assert.Equal(6, page!.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Single((await admin.GetFromJsonAsync<PagedResponse<CategoryResponse>>("/api/categories?keyword=ch%C6%B0a%20d%C3%B9ng"))!.Items);
        var created = await admin.PostAsJsonAsync("/api/categories",
            new SaveCategoryRequest("SQL category", null, "SQL test", 2, true, true));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        long id = (await created.Content.ReadFromJsonAsync<Dictionary<string, long>>())!["id"];
        Assert.Equal(1, await db.NumberAsync($"SELECT COUNT(*) FROM dbo.Category WHERE CategoryId={id} AND CategoryName=N'SQL category'"));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/categories/{id}",
            new SaveCategoryRequest("SQL updated", null, "SQL test", 3, true, true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/categories/{id}/delete", new CategoryActionRequest(true))).StatusCode);
        Assert.Equal(0, await db.NumberAsync($"SELECT COUNT(*) FROM dbo.Category WHERE CategoryId={id}"));
        long referenced = await db.NumberAsync("SELECT CategoryId FROM dbo.Category WHERE Description='DEMO-THREE-UC:unpublished'");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/categories/{referenced}/delete", new CategoryActionRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/categories/{referenced}/deactivate", new CategoryActionRequest(true))).StatusCode);
        Assert.Equal(1, await db.NumberAsync($"SELECT COUNT(*) FROM dbo.Category WHERE CategoryId={referenced} AND IsActive=0"));
        Assert.Equal(4, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog"));
    }

    /// <summary>Injects a real SQL audit failure, proves rollback, then uses the saved quote after restart.</summary>
    [SqlFact]
    public async Task Sql_Checkout_AuditFailureRollsBackThenRestartCommitsSnapshots()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        CheckoutQuote quote;
        using (var host = new SqlApiFactory(db.ConnectionString))
        using (var buyer = host.Client("retail-customer"))
        {
            quote = await CheckoutTests.QuoteAsync(buyer, CheckoutTests.Input("DEMO3UC-WELCOME10", 100));
            Assert.Equal(3, quote.Lines.Sum(line => line.Allocations.Count));
            await db.ExecuteAsync("CREATE TRIGGER dbo.TestAuditFailure ON dbo.AuditLog AFTER INSERT AS THROW 51250,'Injected audit failure',1;");
            var failed = await buyer.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order]"));
            Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.VoucherRedemption"));
            Assert.Equal(0, await db.NumberAsync("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));
            Assert.Equal(4, await db.NumberAsync("SELECT COUNT(*) FROM dbo.CartItem"));
            Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog"));
            var context = await buyer.GetStringAsync("/api/checkout");
            Assert.Contains("\"availablePoints\":1000", context);
            await db.ExecuteAsync("DROP TRIGGER dbo.TestAuditFailure;");
        }
        using (var restarted = new SqlApiFactory(db.ConnectionString))
        using (var buyer = restarted.Client("retail-customer"))
        {
            var placed = await buyer.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
            Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        }
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order] WHERE OrderStatus='PendingPayment' AND RetailQuoteId IS NOT NULL AND AddressCatalogVersionSnapshot IS NOT NULL"));
        Assert.Equal(2, await db.NumberAsync("SELECT COUNT(*) FROM dbo.SellerOrder WHERE SellerNameSnapshot IS NOT NULL"));
        Assert.Equal(2, await db.NumberAsync("SELECT COUNT(*) FROM dbo.OrderItem WHERE ProductNameSnapshot IS NOT NULL"));
        Assert.Equal(3, await db.NumberAsync("SELECT COUNT(*) FROM dbo.OrderItemBatchAllocation WHERE AllocationStatus='Reserved'"));
        Assert.Equal(7, await db.NumberAsync("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));
        Assert.Equal(2, await db.NumberAsync("SELECT COUNT(*) FROM dbo.CartItem"));
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.VoucherRedemption WHERE Status='Reserved' AND DiscountSnapshot>0 AND ExpiresAt IS NOT NULL"));
        Assert.Equal(0, await db.NumberAsync("SELECT UsedCount FROM dbo.Voucher WHERE VoucherCode='DEMO3UC-WELCOME10'"));
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType='Order'"));
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order] o WHERE o.FinalAmount=(SELECT SUM(s.FinalAmount) FROM dbo.SellerOrder s WHERE s.OrderId=o.OrderId)"));
    }

    /// <summary>Runs independent API hosts against the same final five kilograms; only one buyer wins.</summary>
    [SqlFact]
    public async Task Sql_IndependentHosts_LastStockCannotBeOversold()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        await db.ExecuteAsync("DELETE FROM dbo.CartItem WHERE ProductId NOT IN(SELECT ProductId FROM dbo.Product WHERE Description='DEMO-THREE-UC:greens'); UPDATE dbo.HarvestBatch SET AvailableQuantity=CASE WHEN BatchCode='DEMO3UC-RAU-FIRST' THEN 3 WHEN BatchCode='DEMO3UC-RAU-SECOND' THEN 2 ELSE AvailableQuantity END;");
        using var host1 = new SqlApiFactory(db.ConnectionString);
        using var host2 = new SqlApiFactory(db.ConnectionString);
        using var buyer1 = host1.Client("retail-customer");
        using var buyer2 = host2.Client("other-retail-customer");
        var quote1 = await CheckoutTests.QuoteAsync(buyer1);
        var quote2 = await CheckoutTests.QuoteAsync(buyer2);
        var results = await Task.WhenAll(
            buyer1.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote1.Id, Guid.NewGuid(), true)),
            buyer2.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote2.Id, Guid.NewGuid(), true)));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order]"));
        Assert.Equal(5, await db.NumberAsync("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));
        Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.HarvestBatch WHERE AvailableQuantity<0 OR ReservedQuantity<0"));
    }
}

/// <summary>Forces SQL dependencies and the exact disposable connection independently of app defaults.</summary>
internal sealed class SqlApiFactory(string connectionString, TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    /// <summary>Overrides only the three-UC persistence seam; external dependency fixtures remain active.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            if (clock != null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            }
            services.Replace(ServiceDescriptor.Singleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AgriSustain"] = connectionString,
                    ["Persistence:Mode"] = "SqlServer"
                }).Build()));
            services.RemoveAll<IThreeUcUnitOfWork>();
            services.RemoveAll<IDemoIdentityReader>();
            // Never inherit the application's connection: this fixture owns a disposable DB.
            services.RemoveAll<AgriSustainDbContext>();
            services.RemoveAll<DbContextOptions<AgriSustainDbContext>>();
            services.AddDbContext<AgriSustainDbContext>(options => options.UseSqlServer(connectionString));
            services.AddScoped<IThreeUcUnitOfWork, EfThreeUcUnitOfWork>();
            services.AddSingleton<SqlDemoDependencies>();
            services.AddScoped<IDemoIdentityReader, EfDemoIdentityReader>();
        });
    }

    /// <summary>
    /// Builds a client only after checking the resolved EF connection targets this exact
    /// disposable GUID database. Rejects application/configuration leakage before any HTTP action.
    /// </summary>
    public HttpClient Client(string selector)
    {
        var client = CreateClient();
        string expected = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        const string prefix = "AgriSustain_ThreeUcTest_";
        if (!expected.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(expected[prefix.Length..], "N", out _))
        {
            throw new InvalidOperationException("SQL fixture requires a disposable GUID database.");
        }
        using var scope = Services.CreateScope();
        string actual = scope.ServiceProvider.GetRequiredService<AgriSustainDbContext>()
            .Database.GetDbConnection().Database;
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("EF test connection escaped the disposable database.");
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Demo", selector);
        return client;
    }
}

/// <summary>Owns a uniquely named disposable database; cannot target the user's application database.</summary>
internal sealed class SqlTestDatabase : IAsyncDisposable
{
    private readonly string _name = "AgriSustain_ThreeUcTest_" + Guid.NewGuid().ToString("N");
    public string ConnectionString => $"Server=(localdb)\\MSSQLLocalDB;Database={_name};Integrated Security=True;TrustServerCertificate=True;";

    /// <summary>Creates an isolated schema and seed using the reviewed SQLCMD scripts.</summary>
    public static async Task<SqlTestDatabase> CreateAsync()
    {
        var db = new SqlTestDatabase();
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "database")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException("Project database directory missing.");
        }
        await using var master = new SqlConnection(db.ConnectionString.Replace($"Database={db._name}", "Database=master"));
        await master.OpenAsync();
        await new SqlCommand($"CREATE DATABASE [{db._name}]", master).ExecuteNonQueryAsync();
        try
        {
            foreach (string script in new[] { "AgriSustain.sql", "seed-three-uc-demo.sql" })
            {
                var start = new ProcessStartInfo("sqlcmd") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                foreach (string argument in new[] { "-S", "(localdb)\\MSSQLLocalDB", "-d", db._name, "-E", "-b", "-f65001", "-v", $"TargetDatabase={db._name}", "-i", Path.Combine(root, "database", script) })
                {
                    start.ArgumentList.Add(argument);
                }
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                Assert.True(process.ExitCode == 0, await output + await errors);
            }
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    /// <summary>Reads a numeric assertion from this test-owned database only.</summary>
    public async Task<long> NumberAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        return Convert.ToInt64(await new SqlCommand(sql, connection).ExecuteScalarAsync());
    }

    /// <summary>Applies controlled fault/setup SQL exclusively to this test-owned database.</summary>
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
    }

    /// <summary>Drops only the exact generated database after checking its fixed safe prefix.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!_name.StartsWith("AgriSustain_ThreeUcTest_", StringComparison.Ordinal)
            || _name.Length != "AgriSustain_ThreeUcTest_".Length + 32)
        {
            throw new InvalidOperationException("Invalid disposable database name.");
        }
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(ConnectionString.Replace($"Database={_name}", "Database=master"));
        await master.OpenAsync();
        await new SqlCommand($"ALTER DATABASE [{_name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_name}];", master).ExecuteNonQueryAsync();
    }
}
