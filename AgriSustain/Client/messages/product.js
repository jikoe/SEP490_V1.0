/* ==========================================================================
   AgriSustain - Product Management Messages (messages/product.js)
   Vietnamese equivalents of Report-3 V.2 System Messages (UC-FAR-03)
   ========================================================================== */

const ProductMessages = {
  // Authoritative Report-3 System Messages
  MSG71: 'Đã gửi sản phẩm thành công! Sản phẩm đang chờ Kiểm duyệt viên nội dung xem xét và chưa hiển thị công khai.',
  MSG72: 'Đã cập nhật thông tin sản phẩm thành công!',
  MSG73: 'Bạn có chắc muốn ẩn sản phẩm này khỏi sàn AgriSustain?',
  MSG74: 'Đã ẩn sản phẩm khỏi sàn thành công!',
  MSG75: 'Hồ sơ nông trại chưa được Thẩm định viên phê duyệt. Bạn không thể gửi duyệt hoặc hiển thị sản phẩm.',
  MSG76: 'Sản phẩm phải có từ 1–10 ảnh khác nhau và đúng một ảnh đại diện.',
  MSG77: 'Giá bán lẻ phải lớn hơn 0. Nhập đủ cả hai trường bán sỉ hoặc để trống cả hai. Nếu bán sỉ, giá sỉ phải lớn hơn 0 và không vượt giá lẻ; ngưỡng bán sỉ phải lớn hơn 1.',
  MSG78: 'Ảnh phải có định dạng JPG, JPEG hoặc PNG và dung lượng không vượt quá 5 MB.',
  MSG79: 'Không thể đổi đơn vị vì sản phẩm đã có lô thu hoạch hoặc đơn hàng liên kết.',
  MSG80: 'Bạn không có quyền truy cập hoặc chỉnh sửa sản phẩm của nông trại khác.',
  MSG122: 'Yêu cầu xung đột với dữ liệu hiện tại hoặc đã được xử lý. Vui lòng tải lại để xem kết quả; không có thao tác trùng được thực hiện.',
  MSG123: 'Không thể lưu sản phẩm. Không có bản ghi nghiệp vụ nào được lưu một phần. Vui lòng thử lại.',
  MSG124: 'Không có sản phẩm phù hợp với điều kiện đã chọn.',
  MSG126: 'Không thể tải sản phẩm. Vui lòng thử lại.',
  MSG164: 'Đã hiển thị sản phẩm được duyệt thành công, tùy theo điều kiện bán hiện tại.',

  certificateLabel: 'Chứng chỉ (không bắt buộc)',
  certificateNone: 'Không liên kết chứng chỉ',
  certificateHint: 'Chỉ chọn chứng chỉ của nông trại đã duyệt và còn hiệu lực. Đổi chứng chỉ cần gửi duyệt lại.',
  certificateUnavailable: 'Chứng chỉ đã lưu không còn đủ điều kiện',
  certificateExpiry: 'Hạn',
  certificateNoExpiry: 'Không có ngày hết hạn',
  certificateInvalid: 'Chứng chỉ đã chọn không thuộc nông trại, chưa được duyệt, chưa có hiệu lực, đã hết hạn hoặc bị thu hồi. Vui lòng chọn chứng chỉ hợp lệ hoặc bỏ lựa chọn.',

  // Product screen actions
  publishPrompt: 'Hiển thị sản phẩm đã được duyệt?',
  updatePriceSuccess: 'Đã cập nhật giá bán thành công!',
  updatePriceKeepApproval: 'Lưu thay đổi giá thành công! Giữ nguyên kết quả duyệt.',
  retailPriceLimit: 'Giá bán lẻ tối đa 999999999999 và 2 chữ số thập phân.',

  fieldError(field, reason) {
    return `Vui lòng sửa ${field}: ${reason}. Dữ liệu hợp lệ đã được giữ lại.`;
  },

  rejected(name, reason) {
    return `Sản phẩm ${name} đã bị Kiểm duyệt viên nội dung từ chối. Lý do: ${reason}. Vui lòng chỉnh sửa và gửi duyệt lại.`;
  }
};

if (typeof window !== 'undefined') {
  window.ProductMessages = ProductMessages;
  window.MSG = ProductMessages; // Alias for backward compatibility
}
