namespace AgriSustain.Application.Common;

// UC-FAR-03: Vietnamese equivalents of Report-3 V.2 System Messages.
public static class ProductListingMessages
{
    public const string MSG71 = "Đã gửi sản phẩm thành công! Sản phẩm đang chờ Kiểm duyệt viên nội dung xem xét và chưa hiển thị công khai.";
    public const string MSG72 = "Đã cập nhật thông tin sản phẩm thành công!";
    public const string MSG187 = "Thao tác đã hoàn tất. Thông báo đang chờ gửi và sẽ được tự động gửi lại.";
    public const string MSG75 = "Hồ sơ nông trại chưa được Thẩm định viên phê duyệt. Bạn không thể gửi duyệt hoặc hiển thị sản phẩm.";
    public const string MSG76 = "Sản phẩm phải có từ 1–10 ảnh khác nhau và đúng một ảnh đại diện.";
    public const string MSG77 = "Giá bán lẻ phải lớn hơn 0. Nhập đủ cả hai trường bán sỉ hoặc để trống cả hai. Nếu bán sỉ, giá sỉ phải lớn hơn 0 và không vượt giá lẻ; ngưỡng bán sỉ phải lớn hơn 1.";
    public const string MSG78 = "Ảnh phải có định dạng JPG, JPEG hoặc PNG và dung lượng không vượt quá 5 MB.";
    public const string MSG79 = "Không thể đổi đơn vị vì sản phẩm đã có lô thu hoạch hoặc đơn hàng liên kết.";
    public const string MSG80 = "Bạn không có quyền truy cập hoặc chỉnh sửa sản phẩm của nông trại khác.";
    public const string MSG122 = "Yêu cầu xung đột với dữ liệu hiện tại hoặc đã được xử lý. Vui lòng tải lại để xem kết quả; không có thao tác trùng được thực hiện.";
    public const string MSG123 = "Không thể lưu sản phẩm. Không có bản ghi nghiệp vụ nào được lưu một phần. Vui lòng thử lại.";
    public const string MSG126 = "Không thể tải sản phẩm. Vui lòng thử lại.";

    public static string Field(string field, string reason) =>
        $"Vui lòng sửa {field}: {reason}. Dữ liệu hợp lệ đã được giữ lại.";
}
