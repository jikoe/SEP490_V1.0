/* ==========================================================================
   AgriSustain - Common On-Screen Messages (messages/common.js)
   Shared system, authentication, network & generic UI notifications
   ========================================================================== */

const CommonMessages = {
  // Authentication & Session
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
  resetSessionExpired: 'Phiên đặt lại mật khẩu đã hết hạn. Vui lòng thử lại từ đầu.',
  resendOtpFailed: 'Không thể gửi lại mã OTP.',
  otpSentSuccess: 'Mã OTP mới đã được gửi tới email của bạn.',
  verifyOtpFailed: 'Xác thực OTP thất bại.',

  // Account & Profile
  changePasswordSuccess: 'Đổi mật khẩu thành công!',
  updateAvatarSuccess: 'Cập nhật ảnh đại diện thành công!',
  cancelChangesSuccess: 'Đã hủy các thay đổi chưa lưu.',
  kycSubmitted: 'Hồ sơ xác minh KYC của bạn đã được gửi thành công và đang chờ duyệt.',
  kycConfirmPrompt: 'Bạn có chắc chắn muốn nộp hồ sơ để thẩm định KYC không?',

  // Network & Server
  networkError: 'Không thể kết nối đến máy chủ Backend AgriSustain. Hãy kiểm tra API đang chạy.',
  serverError: 'Đã xảy ra lỗi khi xử lý yêu cầu từ máy chủ.',
  loadDataError: 'Không thể tải dữ liệu từ máy chủ. Vui lòng thử lại.',
  operationSuccess: 'Thao tác đã được thực hiện thành công!',
  operationFailed: 'Không thể hoàn tất thao tác. Vui lòng kiểm tra lại.',

  // Common Validations
  validationError: 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại.',
  otpInvalidOrExpired: 'Mã xác thực không hợp lệ hoặc đã hết hạn.',
  otpInputLength: 'Vui lòng nhập đủ 6 chữ số mã OTP.',
  passwordMismatch: 'Mật khẩu xác nhận không khớp.',

  // Verification & Promotion
  certificateVerifiedNotice: 'Đã xác thực chứng nhận thành công!',
  voucherPauseNotice: 'Tạm dừng voucher...',
  voucherCreateNotice: 'Đang mở form tạo voucher mới...'
};

if (typeof window !== 'undefined') {
  window.CommonMessages = CommonMessages;
}
