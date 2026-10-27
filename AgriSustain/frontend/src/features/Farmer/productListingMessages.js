// UC-FAR-03: Vietnamese equivalents of Report-3 V.2 System Messages.
export const productListingMessages = {
  "MSG71": "Đã gửi sản phẩm thành công! Sản phẩm đang chờ Kiểm duyệt viên nội dung xem xét và chưa hiển thị công khai.",
  "MSG72": "Đã cập nhật thông tin sản phẩm thành công!",
  "MSG73": "Bạn có chắc muốn ẩn sản phẩm này khỏi sàn AgriSustain?",
  "MSG74": "Đã ẩn sản phẩm khỏi sàn thành công!",
  "MSG75": "Hồ sơ nông trại chưa được Thẩm định viên phê duyệt. Bạn không thể gửi duyệt hoặc hiển thị sản phẩm.",
  "MSG76": "Sản phẩm phải có từ 1–10 ảnh khác nhau và đúng một ảnh đại diện.",
  "MSG77": "Giá bán lẻ phải lớn hơn 0. Nhập đủ cả hai trường bán sỉ hoặc để trống cả hai. Nếu bán sỉ, giá sỉ phải lớn hơn 0 và không vượt giá lẻ; ngưỡng bán sỉ phải lớn hơn 1.",
  "MSG78": "Ảnh phải có định dạng JPG, JPEG hoặc PNG và dung lượng không vượt quá 5 MB.",
  "MSG79": "Không thể đổi đơn vị vì sản phẩm đã có lô thu hoạch hoặc đơn hàng liên kết.",
  "MSG80": "Bạn không có quyền truy cập hoặc chỉnh sửa sản phẩm của nông trại khác.",
  "MSG122": "Yêu cầu xung đột với dữ liệu hiện tại hoặc đã được xử lý. Vui lòng tải lại để xem kết quả; không có thao tác trùng được thực hiện.",
  "MSG123": "Không thể lưu sản phẩm. Không có bản ghi nghiệp vụ nào được lưu một phần. Vui lòng thử lại.",
  "MSG124": "Không có sản phẩm phù hợp với điều kiện đã chọn.",
  "MSG126": "Không thể tải sản phẩm. Vui lòng thử lại.",
  "MSG164": "Đã hiển thị sản phẩm được duyệt thành công, tùy theo điều kiện bán hiện tại."
};
export function productFieldError(field, reason) {
  return 'Vui lòng sửa ' + field + ': ' + reason + '. Dữ liệu hợp lệ đã được giữ lại.';
}
export function productRejected(name, reason) {
  return 'Sản phẩm ' + name + ' đã bị Kiểm duyệt viên nội dung từ chối. Lý do: ' + reason + '. Vui lòng chỉnh sửa và gửi duyệt lại.';
}
