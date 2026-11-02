using System.Security.Cryptography;
using System.Text;
namespace AgriSustain.Infrastructure.Payments;
public sealed record RefundTransferRequest(long PaymentId, decimal Amount, string BankCode, string AccountNumber, string AccountHolder);
public sealed record RefundTransferResult(string Reference, decimal Amount);
// Explicit demo adapter: does not transfer real money. A stable key identifies one transfer across retries.
public sealed class PaymentGatewayProxy(IHostEnvironment environment)
{
 private readonly Dictionary<string, (RefundTransferRequest Request, RefundTransferResult Result)> completed = new();
 public Task<RefundTransferResult?> GetRefundResultAsync(string key, CancellationToken ct = default)
 {
  ct.ThrowIfCancellationRequested();
  return Task.FromResult(completed.TryGetValue(key, out var transfer) ? transfer.Result : null);
 }
 public Task<RefundTransferResult> RefundAsync(RefundTransferRequest request, string key, CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();
  if (!environment.IsDevelopment()) throw new InvalidOperationException("Gateway hoàn tiền mô phỏng chỉ được dùng ở môi trường Development.");
  if(request.Amount <= 0 || string.IsNullOrWhiteSpace(request.BankCode)
      || request.AccountNumber.Length is < 6 or > 19 || !request.AccountNumber.All(char.IsAsciiDigit)
      || string.IsNullOrWhiteSpace(request.AccountHolder)) throw new InvalidOperationException("Tài khoản nhận tiền chưa hợp lệ.");
  if (completed.TryGetValue(key, out var existing))
  {
   if (existing.Request != request) throw new InvalidOperationException("Refund key already belongs to another request.");
   return Task.FromResult(existing.Result);
  }
  var reference="DEMO-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..40];
  var result = new RefundTransferResult(reference, request.Amount);
  completed.Add(key, (request, result));
  return Task.FromResult(result);
 }
}
