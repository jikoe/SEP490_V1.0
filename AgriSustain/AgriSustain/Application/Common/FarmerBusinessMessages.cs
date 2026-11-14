namespace AgriSustain.Application.Common;

/// <summary>Vietnamese UI wording for Report 3, V.2. Codes are transport metadata only.</summary>
public static class FarmerBusinessMessages
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        ["MSG02"] = "Vui lòng nhập đầy đủ thông tin bắt buộc, không chỉ chứa khoảng trắng.",
        ["MSG104"] = "Các bên đã xác nhận cùng phiên bản điều khoản. Việc thực hiện vẫn phụ thuộc điều kiện tiền cọc đã thỏa thuận.",
        ["MSG105"] = "Đã gửi phản hồi từ chối đề xuất đến doanh nghiệp mua hàng.",
        ["MSG106"] = "Hợp đồng có đề nghị chấm dứt đang chờ xem xét và quyết toán; chưa chấm dứt ngay.",
        ["MSG107"] = "Đã gửi đề nghị chấm dứt đến đối tác và người có thẩm quyền. Hợp đồng chỉ chấm dứt sau phê duyệt và quyết toán.",
        ["MSG109"] = "Lý do từ chối hoặc đề nghị sửa đổi phải có từ 20–500 ký tự sau khi bỏ khoảng trắng đầu và cuối.",
        ["MSG110"] = "Không có dữ liệu doanh thu phù hợp với tiêu chí đã chọn.",
        ["MSG111"] = "Ngày bắt đầu không được sau ngày kết thúc.",
        ["MSG120"] = "Thông tin không hợp lệ. Vui lòng kiểm tra lại.",
        ["MSG121"] = "Bạn không có quyền truy cập hoặc thực hiện thao tác này.",
        ["MSG122"] = "Dữ liệu đã thay đổi hoặc yêu cầu xung đột. Vui lòng tải lại; không thực hiện thao tác trùng.",
        ["MSG123"] = "Không thể lưu dữ liệu hoặc bằng chứng. Vui lòng thử lại.",
        ["MSG124"] = "Không có hợp đồng phù hợp với bộ lọc.",
        ["MSG125"] = "Không thể xuất báo cáo. Vui lòng thử lại.",
        ["MSG126"] = "Không thể tải dữ liệu. Vui lòng thử lại.",
        ["MSG127"] = "Báo cáo đã sẵn sàng để tải xuống.",
        ["MSG130"] = "Không thể đơn phương sửa các cam kết đã thực hiện hoặc có nghĩa vụ được bảo vệ.",
        ["MSG131"] = "Trạng thái hiện tại không cho phép thực hiện thao tác này.",
        ["MSG139"] = "Lô hàng không đủ điều kiện giao: kiểm tra thu hoạch thực tế, tồn kho, hạn sử dụng và mã QR.",
        ["MSG145"] = "Đã gửi báo cáo chậm hoặc thiếu hàng và phương án đề xuất. Điều khoản đã thống nhất được giữ nguyên.",
        ["MSG151"] = "Đã ghi nhận xác nhận của bạn. Đang chờ các xác nhận còn lại trên cùng phiên bản điều khoản.",
        ["MSG152"] = "Hợp đồng đang chờ xác minh đủ tiền cọc theo thỏa thuận.",
        ["MSG153"] = "Hợp đồng đã đủ xác nhận và điều kiện tiền cọc, có thể thực hiện.",
        ["MSG154"] = "Đã gửi đề nghị sửa đổi. Vui lòng chờ bên mua chỉnh sửa và gửi lại đề xuất.",
        ["MSG155"] = "Đã ghi nhận giao hàng theo đợt và xuất kho một lần.",
        ["MSG156"] = "Đợt giao có chênh lệch chưa được giải quyết.",
        ["MSG157"] = "Đang hiển thị kết quả thống nhất đã được ghi nhận.",
        ["MSG158"] = "Đã chuyển đề nghị xem xét tranh chấp đến người có thẩm quyền.",
        ["MSG159"] = "Hợp đồng đã hoàn tất các nghĩa vụ giao hàng và tài chính; uy tín được cập nhật theo kết quả ghi nhận.",
        ["MSG160"] = "Đề nghị chấm dứt đã được phê duyệt và hoàn tất quyết toán.",
        ["MSG161"] = "Đề nghị chấm dứt đã bị từ chối; điều khoản hiện tại được giữ nguyên.",
        ["MSG162"] = "Khoản tiền hoặc nghĩa vụ tài chính đang chờ xử lý, chưa phải chuyển khoản hoàn tất.",
        ["MSG163"] = "Điều khoản đã có phiên bản mới. Vui lòng tải lại trước khi xác nhận.",
        ["MSG-OC-01"] = "Đã gửi giải trình và bằng chứng chênh lệch. Phản hồi không tự thay đổi kết quả nghiệm thu.",
        ["MSG-BIZ-01"] = "Thao tác đã được lưu. Một kênh thông báo chưa gửi được và sẽ được thử lại."
    };

    public static string Text(string code) => All.TryGetValue(code, out var text) ? text : All["MSG120"];
}
