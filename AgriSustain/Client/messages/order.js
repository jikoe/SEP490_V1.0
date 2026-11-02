/* ==========================================================================
   AgriSustain - Order & Moderation Messages (messages/order.js)
   Vietnamese equivalents of UC-RC-04, UC-FAR-02 & Moderation messages
   ========================================================================== */

const OrderMessages = {
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
