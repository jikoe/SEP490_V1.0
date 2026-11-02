using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    IConfiguration configuration,
    ILogger<SePayWebhookController> logger) : ControllerBase
{
    private const string Gateway = "SePay";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    [HttpPost("/api/payment/sepay-webhook")]
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
            logger.LogError("Payment:SePay:WebhookApiKey is not configured.");
            return false;
        }

        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(header["Apikey ".Length..].Trim()), Encoding.UTF8.GetBytes(expected));
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
