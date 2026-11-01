/* ==========================================================================
   AgriSustain - Cart On-Screen Messages (messages/cart.js)
   Vietnamese equivalents of UC-RC-01 Cart System Messages
   ========================================================================== */

const CartMessages = {
  // Authoritative UC-RC-01 Cart Messages
  noSelection: 'Vui lòng chọn ít nhất một sản phẩm trong giỏ hàng để tiến hành thanh toán.',
  empty: 'Giỏ hàng của bạn đang trống. Tiếp tục mua sắm để thêm sản phẩm.',
  confirmRemoval: count => `Xóa ${count} sản phẩm đã chọn khỏi giỏ hàng của bạn?`,
  invalidQuantity: unit => `Nhập số lượng lớn hơn 0 theo bước bán cho phép của đơn vị ${unit}. Số lượng hợp lệ trước đó đã được giữ lại.`,

  // Cart operations
  pricePolicyChanged: 'Giá bán hoặc chính sách giá của một hoặc nhiều sản phẩm đã thay đổi. Vui lòng kiểm tra giá và tổng tiền đã cập nhật rồi tiếp tục.',
  quantityUpdated: 'Đã cập nhật số lượng.',
  removeSuccess: count => `Đã xóa ${count} sản phẩm khỏi giỏ hàng.`,
  addSuccess: 'Đã thêm sản phẩm vào giỏ hàng thành công!',
  addFailed: 'Không thể thêm sản phẩm vào giỏ hàng.',
  cannotProceed: 'Chưa thể tiến hành thanh toán. Vui lòng kiểm tra lại giỏ hàng.',
  loadCartError: 'Không thể tải giỏ hàng từ máy chủ.',
  updateQuantityFailed: 'Không thể lưu số lượng. Vui lòng tải lại giỏ hàng để xem dữ liệu đã được lưu.'
};

if (typeof window !== 'undefined') {
  window.CartMessages = CartMessages;
  window.CART_MESSAGES = CartMessages; // Alias for backward compatibility
}

