using AgriSustain.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AgriSustain.Infrastructure.Payments;

/// <summary>
/// Builds the SePay/VietQR image for a bank transfer to the platform account (same account and QR service as
/// the retail payment screen). The transfer content is the payment reference, which the SePay webhook matches.
/// </summary>
public sealed class SePayQrProvider(IConfiguration configuration) : IPaymentQrProvider
{
    public PaymentQr? Create(string reference, decimal amount, DateTime expiresAt)
    {
        var section = configuration.GetSection("Payment:SePay");
        string? account = section["AccountNumber"];
        string? bank = section["BankShortName"];
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(bank) || amount <= 0)
        {
            return null;
        }

        string url = "https://qr.sepay.vn/img?acc=" + Uri.EscapeDataString(account)
            + "&bank=" + Uri.EscapeDataString(bank)
            + "&amount=" + decimal.Round(amount, 0).ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + "&des=" + Uri.EscapeDataString(reference)
            + "&template=compact";
        return new PaymentQr(reference, amount, expiresAt, section["BankName"] ?? bank, bank, account,
            section["AccountName"] ?? "", url);
    }
}
