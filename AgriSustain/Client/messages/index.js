/* ==========================================================================
   AgriSustain - Centralized On-Screen Messages Registry (messages/index.js)
   Unifies all screen notifications, modals, validations & toasts into 1 shared folder
   ========================================================================== */

/**
 * Filter out technical report reference markers (e.g. (MSG71)) and double dots
 * matching Frontend displayMessage.js 1:1
 */
function displayMessage(value) {
  return String(value ?? '')
    .replace(/\s*\(MSG\d+\)/g, '')
    .replace(/([.!?])\.+(?=\s|$)/g, '$1');
}

const DefaultCommonMessages = {
  loginRequired: 'Vui lòng đăng nhập để tiếp tục truy cập.',
  loginRequiredCart: 'Vui lòng đăng nhập để xem giỏ hàng của bạn.',
  loginRequiredAddToCart: 'Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.',
  loginRequiredCheckout: 'Vui lòng đăng nhập để tiến hành thanh toán.',
  sessionExpired: 'Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.',
  accessDenied: role => `Bạn không có quyền truy cập trang này (Role: ${role}).`,
  retailOnly: 'Chỉ tài khoản khách mua lẻ mới có thể thực hiện thao tác này.',
  retailOnlyCart: 'Chỉ tài khoản khách mua lẻ mới có giỏ hàng.',
  retailOnlyAddToCart: 'Chỉ tài khoản khách lẻ có thể thêm sản phẩm vào giỏ hàng.',
  loginFailedFallback: 'Đăng nhập không thành công. Vui lòng kiểm tra lại tài khoản và mật khẩu.',
  registerSuccess: 'Đăng ký thành công. Vui lòng đăng nhập để tiếp tục.',
  registerFailed: 'Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.',
  resetPasswordSuccess: 'Đặt lại mật khẩu thành công. Đang chuyển hướng về trang đăng nhập…',
  resetSessionExpired: 'Phiên đặt lại mật khẩu đã hết hạn. Vui lòng xác thực lại email.',
  resendOtpFailed: 'Không thể gửi lại mã OTP.',
  otpSentSuccess: 'Mã OTP mới đã được gửi tới email của bạn.',
  verifyOtpFailed: 'Xác thực OTP thất bại.',
  changePasswordSuccess: 'Đổi mật khẩu thành công!',
  updateAvatarSuccess: 'Cập nhật ảnh đại diện thành công!',
  cancelChangesSuccess: 'Đã hủy các thay đổi chưa lưu.',
  kycSubmitted: 'Hồ sơ xác minh KYC của bạn đã được gửi thành công và đang chờ duyệt.',
  kycConfirmPrompt: 'Bạn có chắc chắn muốn nộp hồ sơ để thẩm định KYC không?',
  networkError: 'Không thể kết nối đến máy chủ Backend AgriSustain. Hãy kiểm tra API đang chạy.',
  serverError: 'Đã xảy ra lỗi khi xử lý yêu cầu từ máy chủ.',
  loadDataError: 'Không thể tải dữ liệu từ máy chủ. Vui lòng thử lại.',
  operationSuccess: 'Thao tác đã được thực hiện thành công!',
  operationFailed: 'Không thể hoàn tất thao tác. Vui lòng kiểm tra lại.',
  validationError: 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.',
  otpInvalidOrExpired: 'Mã xác thực không hợp lệ hoặc đã hết hạn.',
  otpInputLength: 'Vui lòng nhập đủ 6 chữ số mã OTP.',
  passwordMismatch: 'Mật khẩu xác nhận không khớp.',
  certificateVerifiedNotice: 'Đã xác thực chứng nhận thành công!',
  voucherPauseNotice: 'Tạm dừng voucher...',
  voucherCreateNotice: 'Đang mở form tạo voucher mới...'
};

const DefaultProductMessages = {
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

const DefaultCartMessages = {
  noSelection: 'Vui lòng chọn ít nhất một sản phẩm trong giỏ hàng để tiến hành thanh toán.',
  empty: 'Giỏ hàng của bạn đang trống. Tiếp tục mua sắm để thêm sản phẩm.',
  confirmRemoval: count => `Xóa ${count} sản phẩm đã chọn khỏi giỏ hàng của bạn?`,
  invalidQuantity: unit => `Nhập số lượng lớn hơn 0 theo bước bán cho phép của đơn vị ${unit}. Số lượng hợp lệ trước đó đã được giữ lại.`,
  pricePolicyChanged: 'Giá bán hoặc chính sách giá của một hoặc nhiều sản phẩm đã thay đổi. Vui lòng kiểm tra giá và tổng tiền đã cập nhật rồi tiếp tục.',
  quantityUpdated: 'Đã cập nhật số lượng.',
  removeSuccess: count => `Đã xóa ${count} sản phẩm khỏi giỏ hàng.`,
  addSuccess: 'Đã thêm sản phẩm vào giỏ hàng thành công!',
  addFailed: 'Không thể thêm sản phẩm vào giỏ hàng.',
  cannotProceed: 'Chưa thể tiến hành thanh toán. Vui lòng kiểm tra lại giỏ hàng.',
  loadCartError: 'Không thể tải giỏ hàng từ máy chủ.',
  updateQuantityFailed: 'Không thể lưu số lượng. Vui lòng tải lại giỏ hàng để xem dữ liệu đã được lưu.'
};

const DefaultCheckoutMessages = {
  phoneHint: 'Số di động Việt Nam gồm 10 số, ví dụ 0912345678.',
  addressIncompleteHint: 'Điền đủ người nhận, số điện thoại và địa chỉ để tính phí giao và tổng tiền.',
  confirmOrder: (total, name, phone) => `Đặt đơn ${total} giao đến ${name} (${phone}) và giữ hàng để thanh toán?`,
  quoteExpiryHint: time => `Giá giữ đến ${time}. Sau khi đặt, hàng được giữ để bạn hoàn tất thanh toán.`,
  recovering: 'Đang kiểm tra kết quả đơn hàng bạn vừa gửi…',
  noItems: 'Chưa có sản phẩm được chọn. Hãy cập nhật giỏ hàng.',
  noVouchers: 'Hiện chưa có voucher phù hợp với giỏ hàng.',
  voucherMinOrderError: 'Chưa đủ giá trị đơn tối thiểu',
  shippingRateUnavailable: 'Không tính được cước vận chuyển cho địa chỉ đã chọn.',
  addressInvalid: 'Địa chỉ nhận hàng không hợp lệ.',
  quoteFailed: 'Không tính được phí giao hàng.',
  placeOrderSuccess: 'Đặt hàng thành công!',
  placeOrderFailed: 'Đặt hàng không thành công.'
};

const DefaultOrderMessages = {
  feedbackSuccess: 'Cảm ơn bạn đã gửi đánh giá nông sản!',
  cancelConfirm: code => `Bạn có chắc chắn muốn hủy đơn hàng #${code} không?`,
  cancelSuccess: 'Đã hủy đơn hàng thành công!',
  confirmPacking: 'Xác nhận đơn hàng này? Vui lòng tiến hành đóng gói theo đúng quy cách.',
  readyToShip: 'Đánh dấu đơn hàng sẵn sàng giao?',
  startShipping: 'Bắt đầu giao hàng mô phỏng?',
  rejectConfirm: code => `Bạn có chắc chắn muốn từ chối đơn hàng ${code} không?`,
  popupBlockedNotice: 'Vui lòng cho phép mở cửa sổ popup để in phiếu đóng gói.',
  moderationApproveConfirm: name => `Bạn có chắc chắn muốn duyệt sản phẩm "${name}" không?`,
  moderationRejectReasonRequired: 'Vui lòng nhập lý do từ chối trước khi tiếp tục.',
  refundCommandIssued: 'Đã phát lệnh hoàn tiền tức thời qua PayOS API!',
  refundManualConfirmed: 'Đã xác nhận hoàn tiền thủ công.',
  evidenceAuditNotice: 'Đang mở biên bản đối soát chứng cứ...',
  ticketArchivedNotice: 'Phiếu đã hoàn tất lưu trữ.',
  compensationApprovedNotice: 'Đã chấp thuận đề xuất bồi thường cho khách hàng.',
  weightAuditRequestedNotice: 'Đã gửi yêu cầu đối soát số cân đóng gói cho HTX.'
};

const activeCommon = Object.assign({}, DefaultCommonMessages, typeof CommonMessages !== 'undefined' ? CommonMessages : {});
const activeProduct = Object.assign({}, DefaultProductMessages, typeof ProductMessages !== 'undefined' ? ProductMessages : {});
const activeCart = Object.assign({}, DefaultCartMessages, typeof CartMessages !== 'undefined' ? CartMessages : {});
const activeCheckout = Object.assign({}, DefaultCheckoutMessages, typeof CheckoutMessages !== 'undefined' ? CheckoutMessages : {});
const activeOrder = Object.assign({}, DefaultOrderMessages, typeof OrderMessages !== 'undefined' ? OrderMessages : {});

const Messages = {
  common: activeCommon,
  product: activeProduct,
  cart: activeCart,
  checkout: activeCheckout,
  order: activeOrder,
  farmerComplaint: typeof FarmerComplaintMessages !== 'undefined' ? FarmerComplaintMessages : {},
  farm: typeof FarmMessages !== 'undefined' ? FarmMessages : {},
  displayMessage
};

// Aliases for quick access
Messages.MSG = Messages.product;
Messages.CART = Messages.cart;

if (typeof window !== 'undefined') {
  window.CommonMessages = activeCommon;
  window.ProductMessages = activeProduct;
  window.CartMessages = activeCart;
  window.CheckoutMessages = activeCheckout;
  window.OrderMessages = activeOrder;
  window.MSG = activeProduct;
  window.CART_MESSAGES = activeCart;
  window.Messages = Messages;
  window.displayMessage = displayMessage;
  if (window.Utils) {
    window.Utils.displayMessage = displayMessage;
  }
}
