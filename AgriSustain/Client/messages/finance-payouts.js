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
  rejectionRequired: "Vui lòng nhập lý do từ chối, không chỉ chứa khoảng trắng.",
  rejectionTooLong: "Lý do từ chối không được vượt quá 500 ký tự.",
  confirmReject: "Từ chối yêu cầu và giải phóng các nguồn tiền của yêu cầu này?",
  selectRequest: "Vui lòng chọn một yêu cầu từ danh sách.",
  confirmSubmit: "Gửi yêu cầu thanh toán và giữ các nguồn đã chọn để Finance Staff xử lý? Bước này chưa chuyển tiền.",
  selectionLimit: "Mỗi yêu cầu được chọn tối đa 100 nguồn tiền.",
  notificationRejected: "Hệ thống gửi lý do từ chối cho nông dân qua thông báo hệ thống và email.",
  selectSources: "Vui lòng chọn ít nhất một khoản đủ điều kiện thanh toán.",
  recipientBlocked: "Tài khoản nhận tiền chưa đủ điều kiện chi trả.",
  recipientReady: "Tài khoản đã xác minh, không bị giữ chi trả",
  held: "Khoản đang giữ",
  eligible: "Đủ điều kiện",
  submittedPending: "Yêu cầu đã gửi cho Finance Staff; chưa thực hiện chuyển tiền.",
  allSources: "Không có nguồn tiền phù hợp để yêu cầu thanh toán.",
  requestEmpty: "Bạn chưa có yêu cầu thanh toán nào.",
  requestCount: (count) => `${count} yêu cầu · 10 bản ghi/trang`,

  failed:
    "Khoản chi đã có trạng thái thất bại. Không mở một giao dịch mới khi chưa đối soát lịch sử cũ.",
  noFailure:
    "Chưa có kết quả thất bại đã xác minh. Không xem việc chưa nhận được webhook là chuyển tiền thất bại.",
  notifications:
    "Sau khi ghi nhận thành công, hệ thống tạo thông báo và gửi email thật cho nông dân.",
  notificationFailed:
    "Kết quả xử lý đã được ghi nhận. Kênh thông báo chưa gửi được sẽ được thử lại; không thực hiện lại thao tác.",
  sourceReserved: "Đã giữ cho yêu cầu này",
  sourceReleased: "Đã giải phóng nguồn",
  sourceSettled: "Đã quyết toán",
  sourceDebit:
    "Đã xác minh tiền ra từ tài khoản nguồn, khớp mã phiếu và số tiền. Đây không phải xác nhận người nhận đã thấy tiền trong tài khoản.",
  confirmTransfer: (net, holder, bank, account) =>
    `Mở QR chi trả ${net} cho ${holder}, ${bank} – ${account}?`,

  status: {
    Verified: "Đã xác minh",
    Unverified: "Chưa xác minh",
    PendingVerification: "Chờ xác minh",
    Prepared: "Đã lập, chưa mở QR",
    Pending: "Chờ xử lý",
    Rejected: "Đã từ chối",
    Submitted: "Nông dân gửi yêu cầu thanh toán",
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
    VerifiedOut: "SePay xác minh tiền ra",
    MismatchOut: "Giao dịch chưa khớp",
    Settled: "Quyết toán nguồn tiền",
    AdditionalOut: "Giao dịch tiền ra bổ sung cần kiểm tra",
  },
};
