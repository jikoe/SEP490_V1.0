namespace AgriSustain.Application.Interfaces;

/// <summary>Bank-transfer QR (SePay/VietQR) the payer scans; the transfer content is the payment reference.</summary>
public sealed record PaymentQr(
    string Reference,
    decimal Amount,
    DateTime ExpiresAt,
    string BankName,
    string BankShortName,
    string AccountNumber,
    string AccountName,
    string QrUrl);

public interface IPaymentQrProvider
{
    /// <summary>Returns null when the receiving account is not configured (payment service unavailable).</summary>
    PaymentQr? Create(string reference, decimal amount, DateTime expiresAt);
}
