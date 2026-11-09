using System.Text.RegularExpressions;
using AgriSustain.Application.DTOs.Offtake;

namespace AgriSustain.Application.Validators;

public static class OfftakeProposalValidator
{
    private static readonly int[] ArticleNumbers = [1, 2, 3, 4, 6, 7, 8];
    private static readonly Regex ArticleBoundary = new(@"\n\n(?=Điều [1-8]: )");

    public static bool IsValid(OfftakeProposalRequest proposal)
    {
        const decimal maximumMoney = 9999999999999999.99m;
        if (proposal.RequestId == Guid.Empty || proposal.FarmerId <= 0 || proposal.ProductId <= 0
            || proposal.Quantity is <= 0 or > 999999999999999.999m
            || proposal.UnitPrice is <= 0 or > maximumMoney
            || proposal.DepositAmount is < 0 or > maximumMoney
            || decimal.Round(proposal.Quantity, 3) != proposal.Quantity
            || decimal.Round(proposal.UnitPrice, 2) != proposal.UnitPrice
            || decimal.Round(proposal.DepositAmount, 2) != proposal.DepositAmount
            || proposal.ValidFrom == default || proposal.DeliveryDate < proposal.ValidFrom
            || proposal.DeliveryDate > proposal.ValidUntil
            || string.IsNullOrWhiteSpace(proposal.DeliveryTerms)
            || proposal.DeliveryTerms.Length > 10000 || proposal.PaymentTerms?.Length > 1000
            || proposal.Quantity > maximumMoney / proposal.UnitPrice)
        {
            return false;
        }

        var total = Total(proposal);
        return total > 0 && proposal.DepositAmount <= total;
    }

    public static bool HasCompleteArticles(OfftakeProposalRequest proposal)
    {
        if (string.IsNullOrWhiteSpace(proposal.DeliveryTerms)
            || string.IsNullOrWhiteSpace(proposal.PaymentTerms))
        {
            return false;
        }

        var sections = ArticleBoundary.Split(proposal.DeliveryTerms.Replace("\r\n", "\n"));
        if (sections.Length != ArticleNumbers.Length)
        {
            return false;
        }

        return sections.Zip(ArticleNumbers).All(section =>
        {
            var lineBreak = section.First.IndexOf('\n');
            return lineBreak >= 0 && section.First.StartsWith($"Điều {section.Second}: ", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(section.First[(lineBreak + 1)..]);
        });
    }

    internal static decimal Total(OfftakeProposalRequest proposal) =>
        decimal.Round(proposal.Quantity * proposal.UnitPrice, 2, MidpointRounding.AwayFromZero);
}
