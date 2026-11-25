/* ==========================================================================
   AgriSustain - Product Management Messages (messages/product.js)
   Vietnamese equivalents of Report-3 V.2 System Messages (UC-FAR-03)
   ========================================================================== */

const ProductMessages = {
  certificateLabel: "Chứng chỉ *",
  certificatePlaceholder: "Chọn chứng chỉ",
  certificateRequired: "Vui lòng chọn chứng chỉ đã duyệt và còn hiệu lực.",
  certificateHint:
    "Bắt buộc khi gửi duyệt. Chỉ chọn chứng chỉ của nông trại đã duyệt và còn hiệu lực.",
  certificateUnavailable: "Chứng chỉ đã lưu không còn đủ điều kiện",
  certificateExpiry: "Hạn",
  certificateNoExpiry: "Không có ngày hết hạn",
  certificateInvalid:
    "Chứng chỉ đã chọn không thuộc nông trại, chưa được duyệt, chưa có hiệu lực, đã hết hạn hoặc bị thu hồi. Vui lòng chọn chứng chỉ hợp lệ.",
};

if (typeof window !== "undefined") {
  window.ProductMessages = ProductMessages;
  window.MSG = ProductMessages; // Alias for backward compatibility
}
