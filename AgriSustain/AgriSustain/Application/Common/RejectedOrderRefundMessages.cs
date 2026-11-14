namespace AgriSustain.Application.Common;

/// <summary>Vietnamese equivalents of the rejected-order messages in the SRS.</summary>
public static class RejectedOrderRefundMessages
{
    public const string Invalid = "Thông tin không hợp lệ. Vui lòng kiểm tra bộ lọc và thử lại.";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này.";
    public const string Missing = "Không tìm thấy yêu cầu hoàn tiền đơn hàng bị từ chối.";
    public const string Stale = "Dữ liệu đã thay đổi hoặc yêu cầu đã được xử lý. Vui lòng tải lại.";
    public const string Ineligible = "Đơn hàng, số tiền còn được hoàn hoặc tài khoản nhận tiền không còn đủ điều kiện. Chưa mở QR hoàn tiền.";
    public const string Pending = "Chưa xác minh được giao dịch tiền ra khớp yêu cầu. Khoản hoàn tiếp tục chờ đối soát; không chuyển tiền lần nữa.";
    public const string Opened = "Đã mở QR hoàn tiền. Yêu cầu đang xử lý; chỉ hoàn tất sau khi SePay xác minh giao dịch tiền ra.";
    public const string Load = "Không thể tải dữ liệu. Vui lòng thử lại.";
    public const string Cancelled = "Đã từ chối đơn hàng. Hệ thống đã thông báo cho khách hàng và chuyển yêu cầu hoàn tiền, nếu có, đến bộ phận Tài chính.";

    public static string Cancellation(string code, string? reason, decimal amount) =>
        $"Đơn hàng {code} đã bị nông trại từ chối. Lý do: {reason}. " + (amount > 0
            ? $"Khoản hoàn {amount:N0} ₫ đang chờ bộ phận Tài chính xử lý về tài khoản nhận tiền đã lưu."
            : "Đơn hàng chưa phát sinh khoản thanh toán cần hoàn.");

    public static string Requested(string order, string codes, decimal amount, string reason, DateTime at) =>
        $"Đơn hàng {order} đã bị nông trại từ chối. Yêu cầu hoàn tiền {codes}: {amount:N0} ₫. "
        + $"Lý do: {reason}. Thời điểm gửi: {at.AddHours(7):dd/MM/yyyy HH:mm}. "
        + "Xử lý tại mục Hoàn tiền đơn bị từ chối trong không gian Tài chính.";

    public static string Succeeded(string code, string order, decimal amount, string? account, string reference) =>
        $"Đã hoàn tiền {amount:N0} ₫ cho đơn hàng {order}, yêu cầu {code}, "
        + $"tài khoản nhận {AgriSustain.Domain.Entities.RefundTransaction.Mask(account)}. "
        + $"SePay đã xác minh giao dịch tiền ra; tham chiếu {reference}. Đơn hàng giữ trạng thái đã hủy.";
}
