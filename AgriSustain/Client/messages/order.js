/* ==========================================================================
   AgriSustain - Order & Moderation Messages (messages/order.js)
   Vietnamese equivalents of UC-FAR-06 messages
   ========================================================================== */

const OrderMessages = {
  get invalidDates() {
    return SharedFeatureMessageText.invalidDates;
  },
  get loadFailed() {
    return SharedFeatureMessageText.loadFailed;
  },
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
  rejectReasonRequired:
    "Vui lòng nhập lý do từ chối. Không được để trống hoặc chỉ nhập khoảng trắng.",
  rejectFailed: "Từ chối đơn hàng thất bại.",
  rejectApiFailed: "Lỗi khi từ chối đơn hàng.",
  printFailed: "Không thể tải phiếu in. Vui lòng thử lại.",
  printReady: "Phiếu đóng gói đã sẵn sàng in.",
  notificationPending:
    " Thông báo hệ thống hoặc email đang chờ gửi; hệ thống sẽ tiếp tục xử lý, không cần thực hiện lại thao tác.",

  rejectedPending:
    "Đã từ chối đơn hàng. Hệ thống đã thông báo cho khách hàng và chuyển yêu cầu hoàn tiền, nếu có, đến bộ phận Tài chính.",

};

if (typeof window !== "undefined") {
  window.OrderMessages = OrderMessages;
}
