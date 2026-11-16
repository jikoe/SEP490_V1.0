namespace AgriSustain.Application.Common;

public static class CustomerComplaintMessages
{
    public const string Saved = "Đã gửi thông tin bổ sung cho bộ phận hỗ trợ khách hàng.";
    public const string SaveFailed = "Không thể lưu phản hồi. Vui lòng thử lại.";
    public const string SupportTitle = "Khách hàng đã bổ sung thông tin khiếu nại";

    public static string SupportContent(long ticketId, long requestId, DateTime now) =>
        $"Khách hàng đã gửi thông tin bổ sung cho khiếu nại #{ticketId}, yêu cầu #{requestId}, "
        + $"lúc {now.AddHours(7):dd/MM/yyyy HH:mm} (UTC+7). "
        + "Vui lòng xem phản hồi trong hệ thống.";
}
