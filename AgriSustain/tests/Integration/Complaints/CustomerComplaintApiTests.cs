using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AgriSustain.Api.Controllers;
using AgriSustain.Api.Security;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AgriSustain.IntegrationTests.Complaints;

public sealed class CustomerComplaintApiTests
{
    [Theory]
    [InlineData("RetailCustomer", HttpStatusCode.OK)]
    [InlineData("Farmer", HttpStatusCode.Forbidden)]
    [Trait("UseCase", "UC-RC-07")]
    public async Task ComplaintEndpoints_RequireRetailCustomer(string role, HttpStatusCode expected)
    {
        string root = Path.Combine(Path.GetTempPath(), "customer-complaint-api-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        byte[] pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        string evidencePath = Path.Combine(root, "evidence.pdf");
        try
        {
            await File.WriteAllBytesAsync(evidencePath, pdf);
            var signingKey = new SymmetricSecurityKey(new byte[32]);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = root
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentActor, CurrentActor>();
            builder.Services.AddScoped<IFarmerComplaintFiles, FarmerComplaintFileStorage>();
            builder.Services.AddSingleton<ICustomerComplaintService>(new StubService(evidencePath));
            builder.Services.AddControllers().AddApplicationPart(typeof(CustomerComplaintsController).Assembly);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = signingKey,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = false,
                    ValidateAudience = false
                });
            builder.Services.AddAuthorization(options =>
                options.AddPolicy("RetailCustomer", policy =>
                    policy.RequireAuthenticatedUser().RequireRole("RetailCustomer")));

            await using var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            using var client = app.GetTestClient();
            var token = new JwtSecurityToken(
                claims: [new(ClaimTypes.NameIdentifier, "10"), new(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new(signingKey, SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new(
                "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

            const string path = "/api/customer/complaints";
            Assert.Equal(expected, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(expected, (await client.GetAsync(path + "/42")).StatusCode);
            using var multipart = new MultipartFormDataContent();
            using var file = new ByteArrayContent(pdf);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            multipart.Add(file, "file", "evidence.pdf");
            using var upload = await client.PostAsync(path + "/evidence", multipart);
            Assert.Equal(expected, upload.StatusCode);
            if (expected == HttpStatusCode.OK)
            {
                Assert.Contains("/api/files/evidence/10/", await upload.Content.ReadAsStringAsync());
            }
            var response = new CustomerComplaintResponseRequest(
                1, "Thông tin bổ sung cho khiếu nại.", ["/api/files/evidence/10/owned.pdf"],
                Guid.NewGuid().ToString("D"), new string('A', 64));
            Assert.Equal(expected, (await client.PostAsJsonAsync(path + "/42/responses", response)).StatusCode);
            using var evidence = await client.GetAsync(path + "/42/evidence/1");
            Assert.Equal(expected, evidence.StatusCode);
            if (expected == HttpStatusCode.OK)
            {
                Assert.Equal(pdf, await evidence.Content.ReadAsByteArrayAsync());
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubService(string evidencePath) : ICustomerComplaintService
    {
        public Task<Result<PagedResponse<CustomerComplaintListItem>>> ListAsync(
            long buyerId, CustomerComplaintQuery query, CancellationToken ct)
        {
            Assert.Equal(10, buyerId);
            Assert.Equal(1, query.Page);
            return Task.FromResult(Result<PagedResponse<CustomerComplaintListItem>>.SuccessResult(
                new([], 0, 1, 10)));
        }

        public Task<Result<CustomerComplaintDetail>> GetAsync(long buyerId, long id, CancellationToken ct)
        {
            Assert.Equal((10L, 42L), (buyerId, id));
            return Task.FromResult(Result<CustomerComplaintDetail>.SuccessResult(
                new(id, "SO-42", "Damaged", null, "WaitingCustomerInfo", null, null, null,
                    [], [], new string('A', 64))));
        }

        public Task<Result<CustomerComplaintResponse>> RespondAsync(
            long buyerId, long id, CustomerComplaintResponseRequest input, CancellationToken ct)
        {
            Assert.Equal((10L, 42L, 1L), (buyerId, id, input.EvidenceRequestId));
            return Task.FromResult(Result<CustomerComplaintResponse>.SuccessResult(
                new(CustomerComplaintMessages.Saved)));
        }

        public Task<string?> GetEvidencePathAsync(
            long buyerId, long id, long evidenceId, CancellationToken ct)
        {
            Assert.Equal((10L, 42L, 1L), (buyerId, id, evidenceId));
            return Task.FromResult<string?>(evidencePath);
        }
    }
}
