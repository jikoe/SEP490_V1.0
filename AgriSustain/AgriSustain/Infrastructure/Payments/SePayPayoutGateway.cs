using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Infrastructure.Payments;

/// <summary>Creates recipient QR and reads observed debits. Never initiates a bank transfer.</summary>
public sealed class SePayPayoutGateway(HttpClient client, IConfiguration configuration) : IPayoutGateway
{
    public PayoutTransferPlan? CreateQr(FarmerPayout payout)
    {
        string account = FarmerPayoutRules.Digits(configuration["Payment:SePay:AccountNumber"]);
        string? bank = configuration["Payment:SePay:BankShortName"];
        if (account.Length < 6 || string.IsNullOrWhiteSpace(bank)
            || string.IsNullOrWhiteSpace(configuration["Payment:SePay:WebhookApiKey"])
            || string.IsNullOrWhiteSpace(payout.RecipientBankCode) || string.IsNullOrWhiteSpace(payout.RecipientAccountNumber)
            || payout.NetAmount <= 0 || payout.NetAmount != decimal.Floor(payout.NetAmount)) return null;
        // SePay's standard outgoing webhook does not support MB; require the read-API path for this source.
        string normalizedBank = FarmerPayoutRules.BankKey(bank);
        bool outgoingWebhook = normalizedBank is "TPB" or "TPBANK" or "STB" or "SACOMBANK" or "ICB" or "CTG" or "VIETINBANK";
        string? readToken = configuration["Payouts:SePayApiToken"] ?? configuration["Payment:SePay:ApiToken"];
        if (!outgoingWebhook && string.IsNullOrWhiteSpace(readToken)) return null;
        string qr = "https://qr.sepay.vn/img?acc=" + Uri.EscapeDataString(FarmerPayoutRules.Digits(payout.RecipientAccountNumber))
            + "&bank=" + Uri.EscapeDataString(payout.RecipientBankCode)
            + "&amount=" + payout.NetAmount.ToString("0", CultureInfo.InvariantCulture)
            + "&des=" + Uri.EscapeDataString(payout.PayoutCode) + "&template=compact";
        return new(payout.PayoutCode, bank, configuration["Payment:SePay:BankName"] ?? bank,
            account, payout.RecipientBankCode, payout.RecipientAccountNumber, payout.NetAmount, DateTime.UtcNow, qr);
    }

    public bool AuthenticateWebhook(string authorization, string alternateKey)
    {
        string? expected = configuration["Payment:SePay:WebhookApiKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        string value = authorization.Trim();
        if (value.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase)) value = value[7..].Trim();
        else if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) value = value[7..].Trim();
        if (value.Length == 0) value = alternateKey.Trim();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(value));
    }

    public async Task<PayoutGatewayLookup> GetTransferStatusAsync(PayoutTransferPlan plan, CancellationToken ct)
    {
        string? token = configuration["Payouts:SePayApiToken"] ?? configuration["Payment:SePay:ApiToken"];
        if (string.IsNullOrWhiteSpace(token)) return new([], false, FinancePayoutMessages.Text("TOKEN_MISSING"));
        string url = "https://my.sepay.vn/userapi/transactions/list?account_number=" + Uri.EscapeDataString(plan.SourceAccountNumber)
            + "&amount_out=" + plan.Amount.ToString("0", CultureInfo.InvariantCulture)
            + "&transaction_date_min=" + Uri.EscapeDataString(plan.OpenedAt.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
            + "&limit=100";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new([], false, FinancePayoutMessages.Text("MSG175"));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.TryGetProperty("status", out var apiStatus) && apiStatus.ToString() != "200")
                return new([], false, FinancePayoutMessages.Text("MSG175"));
            if (!json.RootElement.TryGetProperty("transactions", out var transactions) || transactions.ValueKind != JsonValueKind.Array)
                return new([], false, FinancePayoutMessages.Text("MSG175"));
            var result = new List<PayoutOutgoingTransfer>();
            foreach (var item in transactions.EnumerateArray())
            {
                string Field(string name) => item.TryGetProperty(name, out var value) ? value.ToString() : "";
                if (!decimal.TryParse(Field("amount_out"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0) continue;
                if (!DateTime.TryParseExact(Field("transaction_date"), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var at) || !long.TryParse(Field("id"), out var id) || id <= 0) continue;
                result.Add(new("SEPAY-OUT-" + id, Field("bank_brand_name"), Field("account_number"), amount,
                    new DateTimeOffset(at, TimeSpan.FromHours(7)).UtcDateTime, Field("transaction_content"),
                    Field("reference_number"), item.GetRawText()));
            }
            return new(result, true, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new([], false, FinancePayoutMessages.Text("MSG175"));
        }
        catch (HttpRequestException) { return new([], false, FinancePayoutMessages.Text("MSG175")); }
        catch (JsonException) { return new([], false, FinancePayoutMessages.Text("MSG175")); }
    }
}
