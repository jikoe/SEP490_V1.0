const CustomerComplaintMessages = Object.freeze({
  loading: 'Đang tải khiếu nại…',
  empty: 'Chưa có khiếu nại phù hợp.',
  needsResponse: 'Cần bổ sung',
  noRequests: 'Chưa có yêu cầu bổ sung gửi cho bạn.',
  expired: 'Đã hết thời hạn phản hồi.',
  invalid: 'Nhập 15–2.000 ký tự và chọn 1–5 tệp JPG, PNG hoặc PDF tối đa 5 MB/tệp. Có thể gửi tối đa 1 MP4, tối đa 20 MB.',
  fileRules: 'Chọn 1–5 tệp JPG, PNG hoặc PDF (tối đa 5 MB mỗi tệp), tối đa một video MP4 (20 MB).',
  sending: 'Đang gửi thông tin bổ sung…',
  saved: 'Đã gửi thông tin bổ sung cho bộ phận hỗ trợ khách hàng.',
  loadFailed: 'Không thể tải khiếu nại. Vui lòng thử lại.',
  fileFailed: 'Không thể mở bằng chứng. Vui lòng thử lại.',
  saveFailed: 'Không thể gửi phản hồi. Vui lòng thử lại.',
  reasons: Object.freeze({
    NotAsDescribed: 'Sản phẩm không đúng mô tả',
    Damaged: 'Sản phẩm bị hư hỏng',
    MissingItems: 'Giao thiếu hàng',
    DeliveryIssue: 'Sự cố giao hàng',
    Other: 'Lý do khác'
  }),
  statuses: Object.freeze({
    Open: 'Đã gửi',
    WaitingCustomerInfo: 'Chờ bổ sung thông tin',
    UnderReview: 'Đang xem xét',
    RefundPending: 'Chờ hoàn tiền',
    Rejected: 'Đã từ chối',
    Closed: 'Đã đóng',
    Pending: 'Chờ phản hồi',
    Responded: 'Đã phản hồi',
    Expired: 'Quá hạn'
  })
});
