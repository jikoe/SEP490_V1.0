namespace AgriSustain.Application.Common;

/// <summary>
/// System messages from Report 3 (SRS V.2 System Messages) used by UC-AUTH-01, UC-AUTH-03 and UC-RC-04.
/// The UI language is Vietnamese, so the SRS wording is kept in Vietnamese with the same meaning.
/// </summary>
public static class SystemMessages
{
    /// <summary>MSG180 – Invalid phone number (UC-AUTH-01, UC-AUTH-03).</summary>
    public const string Msg180InvalidPhone =
        "Số điện thoại không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.";

    /// <summary>MSG179 – KYC dossier submitted (UC-AUTH-03).</summary>
    public const string Msg179KycSubmitted =
        "Hồ sơ xác minh KYC của bạn đã được gửi thành công và đang chờ duyệt.";

    /// <summary>MSG181 – Complaint not available for this order (UC-RC-04, UC-RC-06).</summary>
    public const string Msg181ComplaintNotEligible =
        "Chỉ có thể gửi khiếu nại cho đơn hàng đã hoàn thành trong vòng 48 giờ kể từ khi giao.";
}
