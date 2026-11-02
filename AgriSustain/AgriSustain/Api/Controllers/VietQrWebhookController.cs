using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgriSustain.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// Partner API called by VietQR.vn: VietQR first requests a token from
/// {basePath}/api/token_generate (Basic auth), then pushes every balance change of the linked
/// account to {basePath}/bank/api/transaction-sync (Bearer token). basePath is "/vqr" or empty.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class VietQrWebhookController : ControllerBase
{
    private const int TokenLifetimeSeconds = 300;
    private const string Gateway = "VietQR";

    private readonly BankTransferReconciler _reconciler;
    private readonly ILogger<VietQrWebhookController> _logger;
    private readonly IConfiguration _configuration;

    public VietQrWebhookController(BankTransferReconciler reconciler, ILogger<VietQrWebhookController> logger, IConfiguration configuration)
    {
        _reconciler = reconciler;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("/api/payment/config")]
    [HttpGet("/payment/config")]
    public IActionResult GetPaymentConfig()
    {
        var vietQrSection = _configuration.GetSection("Payment:VietQR");
        return Ok(new
        {
            bankName = vietQrSection["BankName"] ?? "MB Bank (Ngân hàng TMCP Quân Đội)",
            bankBin = vietQrSection["BankBin"] ?? "970422",
            bankShortName = vietQrSection["BankShortName"] ?? "MB",
            accountNumber = vietQrSection["AccountNumber"] ?? "0988776655",
            accountName = vietQrSection["AccountName"] ?? "CONG TY CP NONG SAN VIET"
        });
    }

    [HttpGet("/vqr")]
    [HttpGet("/api/vqr")]
    public IActionResult Ping()
    {
        return Ok(new
        {
            status = "active",
            gateway = "VietQR Webhook Gateway",
            tokenEndpoint = "/vqr/api/token_generate",
            transactionSyncEndpoint = "/vqr/bank/api/transaction-sync",
            time = DateTime.UtcNow
        });
    }

    [HttpPost("/vqr/api/token_generate")]
    [HttpPost("/api/token_generate")]
    public IActionResult GenerateToken()
    {
        if (!HasValidBasicCredentials())
        {
            _logger.LogWarning("VietQR token request rejected: invalid Basic credentials.");
            return Unauthorized(new { status = "FAILED", message = "INVALID_CREDENTIALS" });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(TokenLifetimeSeconds).ToUnixTimeSeconds();
        return Ok(new
        {
            access_token = IssueToken(expiresAt),
            token_type = "Bearer",
            expires_in = TokenLifetimeSeconds
        });
    }

    [HttpPost("/vqr/bank/api/transaction-sync")]
    [HttpPost("/bank/api/transaction-sync")]
    [HttpPost("/vqr")]
    [HttpPost("/api/vqr")]
    public async Task<IActionResult> TransactionSync(
        [FromBody] VietQrWebhookPayload? payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("VietQR callback received: {Payload}", JsonSerializer.Serialize(payload));

        if (!HasValidBearerToken() && !HasValidBasicCredentials())
        {
            _logger.LogWarning("VietQR callback rejected: missing or invalid token.");
            return Unauthorized(Failure("INVALID_TOKEN", "Invalid or expired token"));
        }

        if (payload is null)
        {
            return BadRequest(Failure("INVALID_PAYLOAD", "Request payload is null or empty"));
        }

        if (string.IsNullOrWhiteSpace(payload.TransactionId))
        {
            return BadRequest(Failure("MISSING_TRANSACTION_ID", "transactionid is required"));
        }

        if (string.Equals(payload.TransType?.Trim(), "D", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(Success(null, "Bỏ qua giao dịch ghi nợ (transType = D)"));
        }

        var result = await _reconciler.RecordAsync(new BankTransfer(
            Gateway,
            $"VQR-{payload.TransactionId.Trim()}",
            ReadAmount(payload.Amount),
            ReadTransactionTime(payload.TransactionTime),
            payload.ReferenceNumber,
            JsonSerializer.Serialize(payload),
            [payload.OrderId, payload.Content]), cancellationToken);

        return Ok(Success(result.TransactionCode, result.Outcome switch
        {
            BankTransferOutcome.Duplicate => "Giao dịch đã được ghi nhận trước đó",
            BankTransferOutcome.Success => $"Xác nhận thanh toán thành công cho đơn hàng #{result.Order!.OrderCode}",
            BankTransferOutcome.AmountMismatch =>
                $"Số tiền {result.Amount:N0} đ chưa đủ cho đơn hàng #{result.Order!.OrderCode}, chờ đối soát",
            _ => result.Order is null
                ? "Giao dịch được ghi nhận (không tìm thấy mã đơn hàng), chờ đối soát"
                : $"Đơn hàng #{result.Order.OrderCode} không còn chờ thanh toán, giao dịch chờ đối soát"
        }));
    }

    private bool HasValidBasicCredentials()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        var section = _configuration.GetSection("Payment:VietQR:Callback");
        var username = section["Username"];
        var password = section["Password"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogError("Payment:VietQR:Callback:Username/Password are not configured.");
            return false;
        }

        return FixedTimeEquals(decoded, $"{username}:{password}");
    }

    private bool HasValidBearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = header["Bearer ".Length..].Trim().Split('.');
        return parts.Length == 2
            && long.TryParse(parts[0], out var expiresAt)
            && expiresAt >= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            && FixedTimeEquals(parts[1], Sign(parts[0]));
    }

    private string IssueToken(long expiresAt) => $"{expiresAt}.{Sign(expiresAt.ToString())}";

    private string Sign(string value)
    {
        var secret = _configuration["Payment:VietQR:Callback:TokenSecret"] ?? _configuration["JWT:Secret"]
            ?? throw new InvalidOperationException("Configure Payment:VietQR:Callback:TokenSecret or JWT:Secret.");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool FixedTimeEquals(string actual, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));

    private static decimal ReadAmount(JsonElement? raw) => raw switch
    {
        { ValueKind: JsonValueKind.Number } number when number.TryGetDecimal(out var value) => value,
        { ValueKind: JsonValueKind.String } text when decimal.TryParse(text.GetString(), out var value) => value,
        _ => 0m
    };

    private static DateTime? ReadTransactionTime(JsonElement? raw)
    {
        long? milliseconds = raw switch
        {
            { ValueKind: JsonValueKind.Number } number when number.TryGetInt64(out var value) => value,
            { ValueKind: JsonValueKind.String } text when long.TryParse(text.GetString(), out var value) => value,
            _ => null
        };
        return milliseconds is > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value).UtcDateTime : null;
    }

    private static object Failure(string reason, string message) =>
        new { error = true, errorReason = reason, toastMessage = message, @object = (object?)null };

    private static object Success(string? refTransactionId, string message) =>
        new { error = false, errorReason = (string?)null, toastMessage = message, @object = new { reftransactionid = refTransactionId } };
}

/// <summary>Body of VietQR.vn "API Transaction Sync".</summary>
public sealed class VietQrWebhookPayload
{
    [JsonPropertyName("bankaccount")]
    public string? BankAccount { get; set; }

    [JsonPropertyName("amount")]
    public JsonElement? Amount { get; set; }

    [JsonPropertyName("transType")]
    public string? TransType { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("transactionid")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("transactiontime")]
    public JsonElement? TransactionTime { get; set; }

    [JsonPropertyName("referencenumber")]
    public string? ReferenceNumber { get; set; }

    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    [JsonPropertyName("terminalCode")]
    public string? TerminalCode { get; set; }

    [JsonPropertyName("subTerminalCode")]
    public string? SubTerminalCode { get; set; }

    [JsonPropertyName("serviceCode")]
    public string? ServiceCode { get; set; }

    [JsonPropertyName("urlLink")]
    public string? UrlLink { get; set; }

    [JsonPropertyName("sign")]
    public string? Sign { get; set; }
}
