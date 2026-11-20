using AgriSustain.Application.DTOs.Revenue;

namespace AgriSustain.Application.Services;

public sealed class RevenueAggregationRules
{
    public static string ClassifyChannel(string? orderType, bool hasContract) =>
        hasContract ? "Contract" : orderType == "Retail" ? "Retail" : "Wholesale";

    public string? ValidateDateRange(RevenueFilterRequest filter)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        filter.From ??= today.AddDays(-29);
        filter.To ??= today;
        if (filter.From > filter.To)
            return "MSG111";
        if (
            filter.From == DateOnly.MinValue
            || filter.To == DateOnly.MaxValue
            || filter.Page < 1
            || filter.Page > 100000
            || filter.Season < 1
            || filter.DateBasis != "EventDate"
            || filter.Channel is not (null or "" or "Retail" or "Wholesale" or "Contract" or "B2C" or "B2B")
            || filter.PaymentStage
                is not (null or "" or "Goods" or "Deposit" or "Milestone" or "Settlement")
            || filter.SettlementState
                is not (null or "" or "Verified" or "Held" or "Eligible" or "Processed")
        )
            return "MSG120";
        return null;
    }

    public IReadOnlyList<FarmerRevenueEvent> Filter(
        IEnumerable<FarmerRevenueEvent> source,
        RevenueFilterRequest filter
    )
    {
        DateTime from = filter.From!.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        DateTime until = filter.To!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
        return source
            .Where(x =>
                x.At >= from
                && x.At < until
                && (!filter.Season.HasValue || x.SeasonId == filter.Season)
                && MatchesChannel(x.Channel, filter.Channel)
                && (string.IsNullOrEmpty(filter.PaymentStage) || x.Stage == filter.PaymentStage)
                && (
                    string.IsNullOrEmpty(filter.SettlementState)
                    || x.State == filter.SettlementState
                )
            )
            .DistinctBy(x => x.Id)
            .OrderByDescending(x => x.At)
            .ThenBy(x => x.Id)
            .ToList();
    }

    private static bool MatchesChannel(string channel, string? selected) =>
        selected switch
        {
            null or "" => true,
            "B2C" => channel is "Retail" or "B2C",
            "B2B" => channel is "Wholesale" or "Contract" or "B2B",
            _ => channel == selected,
        };

    public RevenueTotals AggregateWithoutDoubleCounting(IEnumerable<FarmerRevenueEvent> rows)
    {
        var unique = rows.DistinctBy(x => x.Id).ToList();
        return new(
            unique.Sum(x => x.Sales),
            unique.Sum(x => x.Receipts),
            unique.Sum(x => x.Deposits),
            unique.Sum(x => x.Refunds),
            unique.Sum(x => x.Commission),
            unique.Sum(x => x.Eligible),
            unique.Sum(x => x.Held),
            unique.Sum(x => x.Payout)
        );
    }
}
