namespace AgriSustain.Application.Common;

public static class FarmerComplaintMessages
{
    public const string Saved = "Giải trình và bằng chứng đã được gửi để xem xét. Phản hồi này không quyết định kết quả khiếu nại.";
    public const string Invalid = "Nhập giải trình từ 15–2.000 ký tự sau khi bỏ khoảng trắng đầu/cuối và đính kèm 1–5 tệp JPG/JPEG/PNG/PDF, tối đa 5 MB mỗi tệp.";
    public const string Forbidden = "Bạn không có quyền truy cập khiếu nại hoặc thực hiện thao tác này.";
    public const string Expired = "Đã hết thời hạn 48 giờ phản hồi yêu cầu này. Hệ thống không tiếp nhận phản hồi quá hạn.";
    public const string Closed = "Yêu cầu không còn khả dụng, đã đóng hoặc đã được phản hồi. Phản hồi trước đó được giữ nguyên.";
    public const string Conflict = "Thông tin đã thay đổi hoặc yêu cầu đã được xử lý. Vui lòng tải lại chi tiết khiếu nại.";
    public const string LoadFailed = "Không thể tải dữ liệu. Vui lòng thử lại.";
    public const string SaveFailed = "Không thể lưu phản hồi hoặc bằng chứng. Vui lòng thử lại. Không có phản hồi dở dang được tiếp nhận.";
    public const string NotificationPending = "Phản hồi đã được ghi nhận. Có thông báo chưa gửi được đến Customer Support và sẽ được thử lại. Vui lòng không gửi lại phản hồi.";
    public const string SupportTitle = "Nông dân đã phản hồi khiếu nại";
    public const string FullTextReference = "Xem nội dung giải trình đầy đủ trong phản hồi của nông dân.";
    public static string SupportContent(long complaint, long request, DateTime at) =>
        $"Nông dân đã gửi giải trình và bằng chứng cho khiếu nại #{complaint}, yêu cầu #{request}, lúc {at.AddHours(7):dd/MM/yyyy HH:mm}. Vui lòng xem xét phản hồi trên hệ thống.";
}
