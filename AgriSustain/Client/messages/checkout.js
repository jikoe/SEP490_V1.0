/* ==========================================================================
   AgriSustain - Checkout On-Screen Messages (messages/checkout.js)
   Vietnamese equivalents of UC-RC-02 Checkout System Messages
   ========================================================================== */

const CheckoutMessages = {
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

if (typeof window !== 'undefined') {
  window.CheckoutMessages = CheckoutMessages;
}

