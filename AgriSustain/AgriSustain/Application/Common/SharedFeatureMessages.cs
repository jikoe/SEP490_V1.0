namespace AgriSustain.Application.Common;

internal static class SharedFeatureMessages
{
    public const string RequiredFields =
        "Vui lòng nhập đầy đủ thông tin bắt buộc, không chỉ chứa khoảng trắng.";

    public const string InvalidDateRange =
        "Ngày bắt đầu không được sau ngày kết thúc.";

    public const string LoadFailed =
        "Không thể tải dữ liệu. Vui lòng thử lại.";

    public const string RejectionReasonTooLong =
        "Lý do từ chối không được vượt quá 500 ký tự.";
}
