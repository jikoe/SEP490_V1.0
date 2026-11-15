/* ==========================================================================
   AgriSustain - Order & Moderation Messages (messages/order.js)
   Vietnamese equivalents of UC-RC-04, UC-FAR-02 & Moderation messages
   ========================================================================== */

const OrderMessages = {
  invalidDates: "Ngày bắt đầu không được sau ngày kết thúc.",
  loadFailed: "Không thể tải dữ liệu. Vui lòng thử lại.",
  connectionFailed: "Không thể kết nối đến máy chủ backend để tải đơn hàng.",
  confirmed: "Đã xác nhận đơn hàng.",
  confirmFailed: "Xác nhận đơn hàng thất bại.",
  confirmApiFailed: "Lỗi khi gọi API xác nhận đơn hàng.",
  readySuccess: "Đơn hàng sẵn sàng giao.",
  actionFailed: "Thao tác không thành công.",
  readyFailed: "Lỗi khi đánh dấu sẵn sàng giao.",
  shippingSuccess: "Đã bắt đầu giao hàng mô phỏng.",
  shippingFailed: "Bắt đầu giao hàng thất bại.",
  shippingApiFailed: "Lỗi khi gọi API giao hàng mô phỏng.",
  rejectReasonRequired: "Vui lòng nhập lý do từ chối. Không được để trống hoặc chỉ nhập khoảng trắng.",
  rejectFailed: "Từ chối đơn hàng thất bại.",
  rejectApiFailed: "Lỗi khi từ chối đơn hàng.",
  printFailed: "Không thể tải phiếu in. Vui lòng thử lại.",
  printReady: "Phiếu đóng gói đã sẵn sàng in.",
  notificationPending: " Thông báo hệ thống hoặc email đang chờ gửi; hệ thống sẽ tiếp tục xử lý, không cần thực hiện lại thao tác.",

  rejectedPending: 'Đã từ chối đơn hàng. Hệ thống đã thông báo cho khách hàng và chuyển yêu cầu hoàn tiền, nếu có, đến bộ phận Tài chính.', 
  feedbackSuccess: 'Cảm ơn bạn đã gửi đánh giá nông sản!',
  cancelConfirm: code => `Bạn có chắc chắn muốn hủy đơn hàng #${code} không?`,
  cancelSuccess: 'Đã hủy đơn hàng thành công!',
  confirmPacking: 'Xác nhận đơn hàng này? Vui lòng tiến hành đóng gói theo đúng quy cách.',
  readyToShip: 'Đánh dấu đơn hàng sẵn sàng giao?',
  startShipping: 'Bắt đầu giao hàng mô phỏng?',
  rejectConfirm: code => `Bạn có chắc chắn muốn từ chối đơn hàng ${code} không?`,
  popupBlockedNotice: 'Vui lòng cho phép mở cửa sổ popup để in phiếu đóng gói.',
  
  // Moderation & Support
  moderationApproveConfirm: name => `Bạn có chắc chắn muốn duyệt sản phẩm "${name}" không?`,
  moderationRejectReasonRequired: 'Vui lòng nhập lý do từ chối trước khi tiếp tục.',
  refundCommandIssued: 'Đã phát lệnh hoàn tiền tức thời qua PayOS API!',
  refundManualConfirmed: 'Đã xác nhận hoàn tiền thủ công.',
  evidenceAuditNotice: 'Đang mở biên bản đối soát chứng cứ...',
  ticketArchivedNotice: 'Phiếu đã hoàn tất lưu trữ.',
  compensationApprovedNotice: 'Đã chấp thuận đề xuất bồi thường cho khách hàng.',
  weightAuditRequestedNotice: 'Đã gửi yêu cầu đối soát số cân đóng gói cho HTX.'
};

if (typeof window !== 'undefined') {
  window.OrderMessages = OrderMessages;
}
