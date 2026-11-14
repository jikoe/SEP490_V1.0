namespace AgriSustain.Application.Common;

public static class FarmerOrderMessages
{
    public const string InvalidFilters = "Bộ lọc hoặc khoảng ngày không hợp lệ.";
    public const string ForbiddenOrder = "Bạn không có quyền truy cập đơn hàng này hoặc thực hiện thao tác.";
    public const string InvalidAction = "Thông tin thao tác không hợp lệ.";
    public const string ReasonRequired = "Vui lòng nhập lý do từ chối. Không được để trống hoặc chỉ nhập khoảng trắng.";
    public const string ReportTypeRequired = "Chọn loại báo cáo giao hàng.";
    public const string EvidenceRequired = "Nhập giải trình (ít nhất 15 ký tự) và bằng chứng.";
    public const string EvidenceNotOwned = "Tệp bằng chứng không thuộc tài khoản của bạn.";
    public const string FarmerNotApproved = "Bạn chưa có quyền xử lý đơn hàng. Hồ sơ nông trại phải được duyệt trước khi thực hiện thao tác.";
    public const string Stale = "Yêu cầu xung đột với dữ liệu hiện tại hoặc đã được xử lý. Vui lòng tải lại để xem kết quả; không thực hiện thao tác trùng lặp.";
    public const string ContractOrder = "Vui lòng xử lý đơn hợp đồng tại mục Quản lý hợp đồng bao tiêu.";
    public const string RefundUnavailable = "Dịch vụ hoàn tiền chưa sẵn sàng. Vui lòng thử lại.";
    public const string UnsupportedAction = "Thao tác không được hỗ trợ.";
    public const string InvalidStatus = "Trạng thái không hợp lệ.";
    public const string NotConfirmed = "Đơn chưa được xác nhận.";
    public const string NotReady = "Không thể chuyển đơn hàng sang đang giao từ trạng thái hiện tại. Đơn phải sẵn sàng giao trước.";
    public const string AlreadyDispatched = "Chỉ từ chối trước khi giao.";
    public const string ReportClosed = "Đơn chưa giao hoặc không còn nhận báo cáo.";
    public const string ReasonTooLong = "Lý do từ chối không được vượt quá 500 ký tự.";
}
