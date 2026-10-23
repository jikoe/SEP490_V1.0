using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace AgriSustain.IntegrationTests;

/// <summary>Tests transaction failures, persisted ownership and independent operating-system process contention.</summary>
public sealed class DetailedSqlTests
{
    /// <summary>A missing isolated SQL catalog produces a safe 503 rather than a misleading empty list or leaked connection string.</summary>
    [SqlFact]
    public async Task Sql_UnavailableDatabase_ReturnsSanitized503()
    {
        string missing = "AgriSustain_ThreeUcTest_" + Guid.NewGuid().ToString("N");
        using var host = new SqlApiFactory($"Server=(localdb)\\MSSQLLocalDB;Database={missing};Integrated Security=True;TrustServerCertificate=True;");
        using var client = host.Client("admin");
        var response = await client.GetAsync("/api/users");
        string body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("DATABASE_UNAVAILABLE", body);
        Assert.DoesNotContain(missing, body);
        Assert.DoesNotContain("SqlException", body);
        Assert.DoesNotContain("ConnectionString", body);
    }

    /// <summary>SQL audit failure must restore the account row and must not publish mock revocation.</summary>
    [SqlFact]
    public async Task Sql_AccountAuditFailure_RollsBackStatusAndSession()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        long id = await db.NumberAsync("SELECT UserId FROM dbo.[User] WHERE Username='demo3uc_retail'");
        await db.ExecuteAsync("CREATE TRIGGER dbo.TestAuditFailure ON dbo.AuditLog AFTER INSERT AS THROW 51250,'Injected audit failure',1;");
        using var host = new SqlApiFactory(db.ConnectionString);
        using var admin = host.Client("admin");
        using var buyer = host.Client("retail-customer");
        var response = await admin.PostAsJsonAsync($"/api/users/{id}/status-actions",
            new AccountActionRequest("suspend", "Atomic test", true));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Injected audit failure", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, await db.NumberAsync($"SELECT COUNT(*) FROM dbo.[User] WHERE UserId={id} AND Status='Active'"));
        Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog"));
        Assert.Equal(HttpStatusCode.OK, (await buyer.GetAsync("/api/checkout")).StatusCode);
    }

    /// <summary>A failed audit insert must roll back a physical category deletion in the real database.</summary>
    [SqlFact]
    public async Task Sql_CategoryDeleteAuditFailure_RestoresDeletedRow()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        long id = await db.NumberAsync("SELECT CategoryId FROM dbo.Category WHERE Description='DEMO-THREE-UC:unused'");
        await db.ExecuteAsync("CREATE TRIGGER dbo.TestAuditFailure ON dbo.AuditLog AFTER INSERT AS THROW 51250,'Injected audit failure',1;");
        using var host = new SqlApiFactory(db.ConnectionString);
        using var admin = host.Client("admin");
        Assert.Equal(HttpStatusCode.InternalServerError, (await admin.PostAsJsonAsync(
            $"/api/categories/{id}/delete", new CategoryActionRequest(true))).StatusCode);
        Assert.Equal(1, await db.NumberAsync($"SELECT COUNT(*) FROM dbo.Category WHERE CategoryId={id}"));
        Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog"));
    }

    /// <summary>Database foreign keys protect category references even when the API is not used.</summary>
    [SqlFact]
    public async Task Sql_CategoryDirectDelete_ReferencedRowsRejectedByForeignKey()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        long id = await db.NumberAsync("SELECT CategoryId FROM dbo.Category WHERE Description='DEMO-THREE-UC:unpublished'");
        var error = await Assert.ThrowsAsync<SqlException>(() => db.ExecuteAsync($"DELETE FROM dbo.Category WHERE CategoryId={id}"));
        Assert.Equal(547, error.Number);
        Assert.Equal(1, await db.NumberAsync($"SELECT COUNT(*) FROM dbo.Category WHERE CategoryId={id}"));
    }

    /// <summary>Saved SQL quotes remain buyer-bound and expire exactly at their own server timestamp.</summary>
    [SqlFact]
    public async Task Sql_QuoteOwnerAndExactExpiry_BlockWithoutReservation()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        var clock = new TestClock();
        using var host = new SqlApiFactory(db.ConnectionString, clock);
        using var buyer = host.Client("retail-customer");
        using var other = host.Client("other-retail-customer");
        var quote = await CheckoutTests.QuoteAsync(buyer);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true))).StatusCode);
        clock.Advance(quote.ExpiresAt - clock.GetUtcNow());
        var expired = await buyer.PostAsJsonAsync("/api/checkout/orders",
            new PlaceOrderRequest(quote.Id, Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        Assert.Contains("QUOTE_EXPIRED", await expired.Content.ReadAsStringAsync());
        Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order]"));
        Assert.Equal(0, await db.NumberAsync("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));
        Assert.Equal(4, await db.NumberAsync("SELECT COUNT(*) FROM dbo.CartItem"));
    }

    /// <summary>Two separate API processes must not sell more than the final five kilograms.</summary>
    [SqlFact]
    public async Task Sql_TwoOperatingSystemProcesses_LastStockHasOneWinner()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        await db.ExecuteAsync("DELETE FROM dbo.CartItem WHERE ProductId NOT IN(SELECT ProductId FROM dbo.Product WHERE Description='DEMO-THREE-UC:greens'); UPDATE dbo.HarvestBatch SET AvailableQuantity=CASE WHEN BatchCode='DEMO3UC-RAU-FIRST' THEN 3 WHEN BatchCode='DEMO3UC-RAU-SECOND' THEN 2 ELSE AvailableQuantity END;");
        await using var first = await SqlProcessHost.StartAsync(db.ConnectionString);
        await using var second = await SqlProcessHost.StartAsync(db.ConnectionString);
        Assert.NotEqual(first.ProcessId, second.ProcessId);
        using var buyer1 = first.Client("retail-customer");
        using var buyer2 = second.Client("other-retail-customer");
        var quote1 = await CheckoutTests.QuoteAsync(buyer1);
        var quote2 = await CheckoutTests.QuoteAsync(buyer2);
        var responses = await Task.WhenAll(
            buyer1.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote1.Id, Guid.NewGuid(), true)),
            buyer2.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote2.Id, Guid.NewGuid(), true)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order]"));
        Assert.Equal(5, await db.NumberAsync("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityType='Order'"));
        Assert.Equal(0, await db.NumberAsync("SELECT COUNT(*) FROM dbo.HarvestBatch WHERE AvailableQuantity<0"));
    }

    /// <summary>Distinct API processes share the persisted final voucher reservation quota.</summary>
    [SqlFact]
    public async Task Sql_TwoOperatingSystemProcesses_FinalVoucherQuotaCannotBeExceeded()
    {
        await using var db = await SqlTestDatabase.CreateAsync();
        await db.ExecuteAsync("UPDATE dbo.Voucher SET UsageLimit=1 WHERE VoucherCode='DEMO3UC-WELCOME10'");
        await using var first = await SqlProcessHost.StartAsync(db.ConnectionString);
        await using var second = await SqlProcessHost.StartAsync(db.ConnectionString);
        using var buyer1 = first.Client("retail-customer");
        using var buyer2 = second.Client("other-retail-customer");
        var quote1 = await CheckoutTests.QuoteAsync(buyer1, CheckoutTests.Input("DEMO3UC-WELCOME10"));
        var quote2 = await CheckoutTests.QuoteAsync(buyer2, CheckoutTests.Input("DEMO3UC-WELCOME10"));
        Assert.True(quote1.VoucherDiscount > 0 && quote2.VoucherDiscount > 0);
        var responses = await Task.WhenAll(
            buyer1.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote1.Id, Guid.NewGuid(), true)),
            buyer2.PostAsJsonAsync("/api/checkout/orders", new PlaceOrderRequest(quote2.Id, Guid.NewGuid(), true)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.VoucherRedemption WHERE Status='Reserved'"));
        Assert.Equal(1, await db.NumberAsync("SELECT COUNT(*) FROM dbo.[Order]"));
        Assert.Equal(0, await db.NumberAsync("SELECT UsedCount FROM dbo.Voucher WHERE VoucherCode='DEMO3UC-WELCOME10'"));
    }
}

/// <summary>Owns only the API process it starts, bound to loopback and an exact disposable database.</summary>
internal sealed class SqlProcessHost : IAsyncDisposable
{
    private readonly Process process;
    private readonly Uri baseAddress;
    private readonly Task<string> output;
    private readonly Task<string> errors;
    public int ProcessId => process.Id;

    /// <summary>Captures the child process and drains its logs so pipe buffers cannot stall testing.</summary>
    private SqlProcessHost(Process child, Uri address)
    {
        process = child;
        baseAddress = address;
        output = child.StandardOutput.ReadToEndAsync();
        errors = child.StandardError.ReadToEndAsync();
    }

    /// <summary>Starts the built API on a free loopback port and verifies the database name before test requests.</summary>
    public static async Task<SqlProcessHost> StartAsync(string connectionString)
    {
        string expected = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (!expected.StartsWith("AgriSustain_ThreeUcTest_", StringComparison.Ordinal)
            || expected.Length != "AgriSustain_ThreeUcTest_".Length + 32)
        {
            throw new InvalidOperationException("Process tests require an isolated generated database.");
        }
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AgriSustain.sln")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var address = new Uri($"http://127.0.0.1:{port}");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(root, "src", "AgriSustain.Api"),
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        start.ArgumentList.Add(Path.Combine(start.WorkingDirectory, "bin", "Release", "net8.0", "AgriSustain.Api.dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(address.ToString());
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["Persistence__Mode"] = "SqlServer";
        start.Environment["Demo__Enabled"] = "true";
        start.Environment["ConnectionStrings__AgriSustain"] = connectionString;
        var host = new SqlProcessHost(Process.Start(start)!, address);
        try
        {
            using var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(3) };
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!deadline.IsCancellationRequested && !host.process.HasExited)
            {
                try
                {
                    var ready = await client.GetAsync("/health/database", deadline.Token);
                    if (ready.IsSuccessStatusCode)
                    {
                        using var json = System.Text.Json.JsonDocument.Parse(await ready.Content.ReadAsStringAsync(deadline.Token));
                        Assert.Equal(expected, json.RootElement.GetProperty("database").GetString());
                        return host;
                    }
                }
                catch (HttpRequestException) { }
                await Task.Delay(100, deadline.Token);
            }
            throw new InvalidOperationException("Test API process did not reach SQL readiness.");
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>Creates an HTTP client using only the public local development actor selector.</summary>
    public HttpClient Client(string selector)
    {
        var client = new HttpClient { BaseAddress = baseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Demo", selector);
        return client;
    }

    /// <summary>Stops only this exact child process and waits for exit before its database is dropped.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        await Task.WhenAll(output, errors);
        process.Dispose();
    }
}
