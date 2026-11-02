using AgriSustain.Application.DTOs.Finance;

namespace AgriSustain.Application.Validators;

public static class FinanceQueryValidator
{
    public static readonly string[] Metrics =
        ["Gmv", "Commission", "Subscriptions", "Refunds", "PendingPayouts", "CompletedPayouts"];

    public static FinancePeriod? Period(FinancePeriodQuery query, DateTime now)
    {
        var date = query.Date ?? DateOnly.FromDateTime(now.AddHours(7));
        if (date.Year is < 1900 or > 9998 || query.Period is not ("Day" or "Month" or "Quarter"))
        {
            return null;
        }
        var start = query.Period switch
        {
            "Month" => new DateOnly(date.Year, date.Month, 1),
            "Quarter" => new DateOnly(date.Year, (date.Month - 1) / 3 * 3 + 1, 1),
            _ => date
        };
        var until = query.Period switch
        {
            "Month" => start.AddMonths(1),
            "Quarter" => start.AddMonths(3),
            _ => start.AddDays(1)
        };
        return new(start, until.AddDays(-1), Utc(start), Utc(until), query.Period);
    }

    public static bool IsValid(FinancePaymentQuery query) =>
        IsPageValid(query.Page, query.PageSize) && query.Search?.Length is not > 150
        && query.Category is null or "" or "Order" or "Subscription"
        && query.Provider is null or "" or "PayOS" or "VietQR" or "VNPay" or "MoMo"
        && query.Status is null or "" or "Pending" or "Succeeded" or "Failed" or "Expired"
        && (query.From == null || query.From.Value.Year is >= 1900 and <= 9998)
        && (query.To == null || query.To.Value.Year is >= 1900 and <= 9998)
        && !(query.From > query.To);

    public static bool IsPageValid(int page, int pageSize) =>
        page is >= 1 and <= 100000 && pageSize is >= 1 and <= 100;

    public static bool IsValid(ReconcilePaymentRequest request) =>
        request.Version is { Length: 64 } && request.Version.All(Uri.IsHexDigit)
        && Guid.TryParseExact(request.RequestKey, "D", out _);

    public static DateTime Utc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
}
