using System.Globalization;

namespace AgriSustain.Application.Common;

// UC-RC-01: Vietnamese equivalents of Report-3 V.2 System Messages.
public static class CartMessages
{
    public const string Added = "Đã thêm sản phẩm vào giỏ hàng thành công!";
    public const string WholesaleApplied = "Đã áp dụng đơn giá bán sỉ vì sản phẩm đạt ngưỡng số lượng bán sỉ tối thiểu.";
    public const string RetailRestored = "Đã áp dụng đơn giá bán lẻ vì số lượng sản phẩm thấp hơn ngưỡng bán sỉ tối thiểu.";
    public const string NoSelection = "Vui lòng chọn ít nhất một sản phẩm trong giỏ hàng để tiến hành thanh toán.";
    public const string PriceChanged = "Giá bán hoặc chính sách giá của một hoặc nhiều sản phẩm đã thay đổi. Vui lòng kiểm tra giá và tổng tiền đã cập nhật, sau đó tiếp tục thanh toán.";
    public const string Empty = "Giỏ hàng của bạn đang trống. Tiếp tục mua sắm để thêm sản phẩm.";

    public static string Removed(int count) =>
        $"Đã xóa {count} sản phẩm khỏi giỏ hàng của bạn.";

    public static string Adjusted(string productName, decimal availableQuantity,
        decimal adjustedQuantity, string unit) =>
        $"Sản phẩm {productName} hiện chỉ còn {Quantity(availableQuantity)} {unit}. "
        + $"Số lượng trong giỏ hàng đã được điều chỉnh còn {Quantity(adjustedQuantity)} {unit}. "
        + "Vui lòng kiểm tra số lượng và tạm tính đã cập nhật trước khi thanh toán.";

    public static string Unavailable(string productName) =>
        $"{productName} không thể mua (hết hàng, bị thu hồi, ngừng bán hoặc không còn đủ điều kiện bán). "
        + "Bỏ chọn để tiếp tục với các sản phẩm còn mua được hoặc xóa sản phẩm khỏi giỏ hàng.";

    public static string InvalidQuantity(string unit) =>
        $"Nhập số lượng lớn hơn 0 theo bước bán cho phép của đơn vị {unit}. "
        + "Số lượng hợp lệ trước đó đã được giữ lại.";

    private static string Quantity(decimal value) =>
        value.ToString("0.###", CultureInfo.GetCultureInfo("vi-VN"));
}
