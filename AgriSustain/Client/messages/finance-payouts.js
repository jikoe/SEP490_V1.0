const FinancePayoutMessages = {
  required:
    "Vui lòng nhập đầy đủ thông tin và chọn ít nhất một nguồn đủ điều kiện.",
  loadFailed:
    "Không thể tải dữ liệu chi trả. Vui lòng thử lại; dữ liệu chưa tải được không phải số tiền bằng không.",
  saveFailed:
    "Không thể lưu yêu cầu. Nếu đã chuyển khoản, hãy kiểm tra giao dịch cũ, không chuyển lại.",
  selectPayout: "Vui lòng chọn khoản chi trả từ danh sách.",
  dates: "Ngày bắt đầu không được sau ngày kết thúc.",
  empty: "Không có khoản chi trả phù hợp với bộ lọc.",
  noSources: "Chưa có nguồn tiền đủ điều kiện cho nông dân này.",
  confirm: "Xác nhận thực hiện thao tác với khoản chi trả đang hiển thị?",
  copied: "Đã sao chép nội dung chuyển khoản.",
  copyFailed:
    "Không thể sao chép tự động. Bạn có thể chọn và sao chép nội dung trên màn hình.",
  pending:
    "Chưa có giao dịch tiền ra SePay khớp đủ điều kiện. Giữ nguồn tiền, kiểm tra giao dịch cũ và không chuyển lại.",
  cancelled: "Khoản chưa mở QR đã được hủy; các nguồn tiền được giải phóng.",
  failed:
    "Khoản chi đã có trạng thái thất bại. Không mở một giao dịch mới khi chưa đối soát lịch sử cũ.",
  noFailure:
    "Chưa có kết quả thất bại đã xác minh. Không xem việc chưa nhận được webhook là chuyển tiền thất bại.",
  notifications:
    "Sau khi ghi nhận thành công, hệ thống tạo thông báo và gửi email thật cho nông dân.",
  notificationFailed:
    "Khoản chi đã được ghi nhận; email chưa gửi được sẽ được thử lại, không chuyển tiền lần nữa.",
  sourceDebit:
    "Đã xác minh tiền ra từ tài khoản nguồn, khớp mã phiếu và số tiền. Đây không phải xác nhận người nhận đã thấy tiền trong tài khoản.",
  confirmTransfer: (net, holder, bank, account) =>
    `Mở QR chi trả ${net} cho ${holder}, ${bank} – ${account}?`,
  confirmCreate:
    "Xác nhận lập phiếu và giữ các nguồn đã chọn? Bước này chưa chuyển tiền.",
  status: {
    Prepared: "Đã lập, chưa mở QR",
    Pending: "Chờ xử lý",
    Approved: "Đã phê duyệt",
    Processing: "Đang chờ tiền ra",
    NeedsReconciliation: "Cần đối soát",
    Processed: "Đã ghi nhận chi trả",
    Paid: "Đã ghi nhận chi trả",
    Completed: "Đã ghi nhận chi trả",
    Succeeded: "Đã ghi nhận chi trả",
    Cancelled: "Đã hủy",
    Failed: "Thất bại đã xác minh",
    Create: "Lập phiếu",
    OpenQr: "Mở mã QR",
    CancelUnsubmitted: "Hủy khoản chưa mở QR",
    VerifiedOut: "SePay xác minh tiền ra",
    MismatchOut: "Giao dịch chưa khớp",
    Settled: "Quyết toán nguồn tiền",
    AdditionalOut: "Giao dịch tiền ra bổ sung cần kiểm tra",
  },
};
