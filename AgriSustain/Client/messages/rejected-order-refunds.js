window.RejectedOrderRefundMessages = {
  loadFailed: "Không thể tải dữ liệu. Vui lòng thử lại.",
  invalidDates: "Ngày bắt đầu không được sau ngày kết thúc.",
  choose: "Vui lòng chọn một yêu cầu từ danh sách.",
  empty: "Không có yêu cầu hoàn tiền phù hợp.",
  copied: "Đã sao chép nội dung chuyển khoản.",
  copyFailed: "Không thể sao chép. Vui lòng sao chép mã yêu cầu đang hiển thị.",
  qrFailed: "Không thể tải ảnh QR. Vui lòng tải lại chi tiết yêu cầu.",
  unopened: "Chưa mở QR. Kiểm tra yêu cầu trước khi xác nhận.",
  pending: "Đang chờ SePay xác minh giao dịch tiền ra. Chưa có kết quả không có nghĩa là chuyển tiền thất bại; không chuyển lại.",
  succeeded: "Đã xác minh giao dịch tiền ra và ghi nhận hoàn tiền. Không chuyển lại.",
  notification: "Sau khi hoàn tiền được xác minh, hệ thống gửi thông báo và email kết quả cho khách hàng.",
  notVerified: "Chưa có kết quả đã xác minh",
  requestCount: total => `${total} yêu cầu · 10 bản ghi/trang`,
  confirm: (code, amount, bank, account, name) =>
    `Mở QR cho yêu cầu ${code}, số tiền ${amount}, ngân hàng ${bank}, tài khoản ${account}, chủ tài khoản ${name}? Thao tác này chưa chuyển tiền.`,
  status: {
    Pending: "Chờ xử lý", Processing: "Đang chờ đối soát",
    NeedsReconciliation: "Cần đối soát", Succeeded: "Đã hoàn tiền", Completed: "Đã hoàn tiền",
    Requested: "Tạo yêu cầu hoàn tiền", QrOpened: "Mở QR hoàn tiền", SePayVerified: "SePay xác minh tiền ra",
  },
};
