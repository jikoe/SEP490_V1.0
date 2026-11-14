namespace AgriSustain.Application.Common;

public static class FinancePayoutMessages
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["MSG01"] = "Không có khoản chi trả phù hợp với bộ lọc.",
        ["MSG02"] = "Vui lòng nhập đầy đủ thông tin bắt buộc, không chỉ chứa khoảng trắng.",
        ["MSG169"] = "Đã gửi yêu cầu thanh toán. Các nguồn đã chọn được giữ để Finance Staff xử lý; chưa thực hiện chuyển tiền.",
        ["MSG170"] = "Kiểm tra đúng số tiền và tài khoản nông dân trước khi xác nhận mở mã QR chuyển khoản.",
        ["MSG171"] = "SePay đã xác minh tiền ra từ tài khoản nguồn, khớp mã phiếu và số tiền. Đã ghi nhận chi trả và quyết toán nguồn tiền.",
        ["MSG172"] = "Nguồn tiền, tài khoản nhận hoặc số tiền chi trả chưa đủ điều kiện. Vui lòng kiểm tra các lý do đang giữ.",
        ["MSG173"] = "Bạn không có quyền quản lý khoản chi trả.",
        ["MSG174"] = "Dữ liệu hoặc yêu cầu chi trả đã thay đổi, xung đột hoặc đã được xử lý. Vui lòng tải lại, không chuyển tiền lần nữa.",
        ["MSG175"] = "Chưa đủ bằng chứng xác nhận chuyển tiền thành công. Tiếp tục giữ nguồn tiền và đối soát giao dịch hiện có; không chuyển lại.",
        ["MSG176"] = "Nhà cung cấp đã xác minh chuyển tiền thất bại. Khoản tiền chưa được quyết toán.",
        ["MSG177"] = "Không thể tải dữ liệu chi trả. Vui lòng thử lại; dữ liệu chưa tải được không phải số tiền bằng không.",
        ["MSG178"] = "Không thể lưu khoản chi trả hoặc kết quả đối soát. Nếu đã chuyển khoản, hãy kiểm tra giao dịch cũ, không chuyển lại.",
        ["MSG-BIZ-01"] = "Yêu cầu đã được ghi nhận. Kênh thông báo chưa gửi được sẽ được thử lại; không thực hiện lại thao tác.",
        ["MSG-PO-03"] = "Đã từ chối yêu cầu trước khi mở QR, giải phóng nguồn tiền và gửi lý do cho nông dân.",
        ["MSG120"] = "Lý do từ chối không được vượt quá 500 ký tự.",
        ["MSG121"] = "Bạn không có quyền truy cập yêu cầu thanh toán này.",
        ["QR_READY"] = "Đã mở mã QR. Dùng ứng dụng ngân hàng của tài khoản nguồn đã hiển thị để chuyển tiền. Tạo QR chưa chứng minh đã thanh toán.",
        ["NO_EVENT"] = "Chưa nhận được giao dịch tiền ra phù hợp từ SePay. Giữ nguyên phiếu; kiểm tra webhook hoặc chờ đồng bộ, không chuyển lại.",
        ["TOKEN_MISSING"] = "Chưa cấu hình token truy vấn SePay. Hệ thống vẫn nhận webhook; nút kiểm tra chỉ đối chiếu dữ liệu webhook đã nhận.",
        ["SOURCE_CONFIG"] = "Nguồn chi chưa đủ cấu hình nhận dữ liệu tiền ra SePay. Với MB cần token truy vấn và kết nối hỗ trợ tiền ra; khóa webhook checkout không thay thế token này.",
        ["INVALID_DATES"] = "Ngày bắt đầu không được sau ngày kết thúc.",
        ["ACCOUNT_HOLD"] = "Tài khoản nhận tiền chưa được xác minh, không hoạt động hoặc đang trong thời gian giữ sau thay đổi.",
        ["ACCOUNT_CHANGED"] = "Tài khoản nhận tiền đã thay đổi so với phiếu được lập. Không dùng QR cũ để chuyển khoản.",
        ["WAITING_PERIOD"] = "Chưa hết thời gian chờ quyết toán sau nhận hàng.",
        ["DISPUTE_HOLD"] = "Đang có tranh chấp hoặc yêu cầu hoàn tiền chưa được giải quyết.",
        ["ACCEPTANCE_HOLD"] = "Chưa có nghiệm thu hoặc xác nhận nhận hàng hợp lệ.",
        ["UNPAID_HOLD"] = "Chưa thu đủ tiền đã được xác minh cho nguồn chi trả.",
        ["ALREADY_HELD"] = "Nguồn tiền đã được giữ hoặc quyết toán cho khoản chi khác.",
        ["SUSPENDED"] = "Tài khoản nông dân hoặc hồ sơ nông trại chưa đủ điều kiện nhận chi trả.",
        ["FEE_INVALID"] = "Snapshot phí hoặc khấu trừ không hợp lệ; cần đối soát trước khi lập khoản chi."
    };
    public static string SubmittedResult(string code, decimal amount) =>
        $"Đã gửi yêu cầu thanh toán {code}, số tiền {amount:N0} VND. Các nguồn đã chọn được giữ để Finance Staff xử lý; chưa thực hiện chuyển tiền.";
    public static string SubmissionTitle(string code) => "Yêu cầu thanh toán " + code;
    public static string RejectionTitle(string code) => "Yêu cầu thanh toán bị từ chối " + code;
    public static string PaidTitle(string code) => "Kết quả chi trả " + code;
    public static string Submission(string farmer, string code, decimal amount, DateTime at, string link) =>
        $"Nông dân {farmer} đã gửi yêu cầu {code}, số tiền {amount:N0} VND lúc {at.AddHours(7):dd/MM/yyyy HH:mm} (giờ Việt Nam). Xem và xử lý: {link}";
    public static string Rejection(string code, string reason) =>
        $"Yêu cầu {code} đã bị từ chối trước khi mở QR. Lý do: {reason}. Các nguồn tiền của yêu cầu đã được giải phóng; chưa ghi nhận chuyển tiền.";
    public static string Payment(AgriSustain.Domain.Entities.FarmerPayout payout,
        AgriSustain.Application.DTOs.Payouts.PayoutOutgoingTransfer transfer) =>
        $"Khoản chi {payout.PayoutCode}: {payout.NetAmount:N0} VND, ngân hàng {payout.RecipientBankCode}, "
        + $"tài khoản {payout.RecipientAccountNumber}. Đã xác minh tiền ra từ tài khoản nguồn qua SePay. "
        + $"Tham chiếu {transfer.BankReference ?? transfer.EventId}.";

    public static string Text(string code) => All.TryGetValue(code, out var text) ? text : All["MSG172"];
}
