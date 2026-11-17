using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Payments;

public sealed class SePayContractDepositReconciler(
    HttpClient client, IConfiguration configuration, AgriSustainContext db, ContractPaymentService payments)
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var sePay = configuration.GetSection("Payment:SePay");
        var token = sePay["ApiToken"] ?? configuration["Payouts:SePayApiToken"];
        var account = sePay["AccountNumber"]?.Trim();
        var bank = sePay["BankShortName"];
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(bank))
            return;

        var deposits = await db.ContractMilestones.AsNoTracking()
            .Where(m => m.MilestoneType == ContractPaymentRules.DepositType
                && m.PaidAmount < m.Amount && m.B2bcontract.Status == "AwaitingDeposit"
                && m.B2bcontract.SignedDate.HasValue
                && m.ContractVersion.VersionNo == m.B2bcontract.CurrentVersionNo
                && m.ContractVersion.VersionStatus == "Signed")
            .Select(m => new { m.ContractMilestoneId, SignedAt = m.B2bcontract.SignedDate!.Value })
            .ToDictionaryAsync(m => m.ContractMilestoneId, m => m.SignedAt, cancellationToken);
        if (deposits.Count == 0)
            return;

        var firstSignedLocal = deposits.Values.Min().Add(VietnamOffset);
        var minDate = firstSignedLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        // shortcut: SePay returns up to 5,000 recent rows per lookup, split the date range if this account outgrows it.
        var url = "https://my.sepay.vn/userapi/transactions/list?account_number=" + Uri.EscapeDataString(account)
            + "&transaction_date_min=" + Uri.EscapeDataString(minDate) + "&limit=5000";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        if (json?.RootElement.ValueKind != JsonValueKind.Object
            || json.RootElement.TryGetProperty("status", out var status) && status.ToString() != "200"
            || !json.RootElement.TryGetProperty("transactions", out var transactions)
            || transactions.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("SePay did not return a transaction list.");

        var credits = new List<(long Id, IncomingBankTransfer Transfer)>();
        foreach (var item in transactions.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string Field(string name) => item.TryGetProperty(name, out var field) ? field.ToString() : "";
            if (Field("account_number").Trim() != account || !FarmerPayoutRules.SameBank(Field("bank_brand_name"), bank)
                || !long.TryParse(Field("id"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
                || !decimal.TryParse(Field("amount_in"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
                || amount <= 0
                || !DateTimeOffset.TryParseExact(Field("transaction_date") + " +07:00", "yyyy-MM-dd HH:mm:ss zzz",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var paidAt))
                continue;

            string[] references = [Field("code"), Field("transaction_content")];
            if (ContractPaymentRules.FindLineId(references) is not long lineId
                || !deposits.TryGetValue(lineId, out var signedAt)
                || signedAt - paidAt.UtcDateTime >= TimeSpan.FromSeconds(1))
                continue;

            credits.Add((id, new IncomingBankTransfer("SePay", $"SEPAY-{id}", amount,
                paidAt.UtcDateTime, Field("reference_number"), item.GetRawText(), references)));
        }

        foreach (var credit in credits.OrderBy(x => x.Transfer.PaidAt).ThenBy(x => x.Id))
            await payments.TryHandleAsync(credit.Transfer, cancellationToken);
    }
}
