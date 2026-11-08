using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// SePay balance-change webhook (API Key auth: "Authorization: Apikey {key}"). SePay retries until
/// it receives HTTP 200 with exactly {"success": true}.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class SePayWebhookController(
    BankTransferReconciler reconciler,
    IEnumerable<IBankTransferHandler> transferHandlers,
    IConfiguration configuration,
    ILogger<SePayWebhookController> logger) : ControllerBase
{
    private const string Gateway = "SePay";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    [HttpGet("/api/payment/config")]
    [HttpGet("/payment/config")]
    public IActionResult GetPaymentConfig()
    {
        var sePaySection = configuration.GetSection("Payment:SePay");
        return Ok(new
        {
            bankName = sePaySection["BankName"] ?? "MB Bank (Ngân hàng TMCP Quân Đội)",
            bankBin = sePaySection["BankBin"] ?? "970422",
            bankShortName = sePaySection["BankShortName"] ?? "MB",
            accountNumber = sePaySection["AccountNumber"] ?? "0000499456980",
            accountName = sePaySection["AccountName"] ?? "CONG TY CP NONG SAN VIET"
        });
    }

    [HttpPost("/api/payment/sepay-webhook")]
    [HttpPost("/api/payment/payos-webhook")]
    [HttpPost("/api/payment/webhook")]
    [HttpPost("/api/sepay/webhook")]
    [HttpPost("/sepay-webhook")]
    [HttpPost("/")]
    public async Task<IActionResult> Receive([FromBody] SePayWebhookPayload? payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("SePay webhook received: {Payload}", JsonSerializer.Serialize(payload));

        if (!HasValidApiKey())
        {
            logger.LogWarning("SePay webhook rejected: missing or invalid API key.");
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        if (payload?.Id is not long id)
        {
            return BadRequest(new { success = false, message = "Invalid payload" });
        }

        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { success = true });
        }

        // B2B payments (UC-WB-08 "SUB...", UC-FIN-06 "HD...") are claimed by their own handlers first.
        var incoming = new IncomingBankTransfer(
            Gateway,
            $"SEPAY-{id}",
            payload.TransferAmount,
            ParseVietnamTime(payload.TransactionDate),
            payload.ReferenceCode,
            JsonSerializer.Serialize(payload),
            [payload.Code, payload.Content, payload.Description]);
        foreach (var handler in transferHandlers)
        {
            if (await handler.TryHandleAsync(incoming, cancellationToken))
            {
                return Ok(new { success = true });
            }
        }

        await reconciler.RecordAsync(new BankTransfer(
            Gateway,
            $"SEPAY-{id}",
            payload.TransferAmount,
            ParseVietnamTime(payload.TransactionDate),
            payload.ReferenceCode,
            JsonSerializer.Serialize(payload),
            [payload.Code, payload.Content, payload.Description]), cancellationToken);

        return Ok(new { success = true });
    }


    private bool HasValidApiKey()
    {
        var expected = configuration["Payment:SePay:WebhookApiKey"];
        if (string.IsNullOrEmpty(expected))
        {
            logger.LogWarning("Payment:SePay:WebhookApiKey is not configured; allowing in development.");
            return true;
        }

        if (Request.Query.TryGetValue("apikey", out var queryKey) && string.Equals(queryKey.ToString().Trim(), expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Request.Headers.TryGetValue("x-api-key", out var xApiKey) && string.Equals(xApiKey.ToString().Trim(), expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var header = Request.Headers.Authorization.ToString().Trim();
        if (string.IsNullOrEmpty(header))
        {
            return false;
        }

        string tokenValue;
        if (header.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            tokenValue = header["Apikey ".Length..].Trim();
        }
        else if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            tokenValue = header["Bearer ".Length..].Trim();
        }
        else
        {
            tokenValue = header;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(tokenValue), Encoding.UTF8.GetBytes(expected));
    }

    private static DateTime? ParseVietnamTime(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var local)
            ? new DateTimeOffset(local, VietnamOffset).UtcDateTime
            : null;
}

/// <summary>Body of the SePay transaction webhook.</summary>
public sealed class SePayWebhookPayload
{
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("gateway")]
    public string? BankGateway { get; set; }

    [JsonPropertyName("transactionDate")]
    public string? TransactionDate { get; set; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; set; }

    [JsonPropertyName("subAccount")]
    public string? SubAccount { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("transferType")]
    public string? TransferType { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("transferAmount")]
    public decimal TransferAmount { get; set; }

    [JsonPropertyName("accumulated")]
    public decimal Accumulated { get; set; }

    [JsonPropertyName("referenceCode")]
    public string? ReferenceCode { get; set; }
}

