namespace AgriSustain.Application.Common;

// Vietnamese equivalents of Report-3 V.2; shared by all three farm-management features.
public static class FarmManagementMessages
{
    public const string MSG89 = "Đã tạo kế hoạch mùa vụ thành công.";

    public const string MSG90 = "Đã cập nhật mùa vụ thành công.";

    public const string MSG91 = "Đã hoàn thành mùa vụ và ghi nhận sản lượng thực tế.";

    public const string MSG92 = "Ngày thu hoạch dự kiến phải sau ngày trồng; với kế hoạch mới, ngày này phải ở tương lai.";

    public const string MSG93 = "Sản lượng dự kiến phải lớn hơn 0 và dùng đơn vị nhất quán.";

    public const string MSG94 = "Đã ghi nhận nhật ký canh tác thành công.";

    public const string MSG95 = "Ngày thực hiện không được ở tương lai.";

    public const string MSG96 = "Ảnh bằng chứng phải là JPG, JPEG hoặc PNG và không vượt quá 5 MB mỗi ảnh.";

    public const string MSG97 = "Vui lòng chọn loại hoạt động và nhập mô tả; không được để trống hoặc chỉ nhập khoảng trắng.";

    public const string MSG99 = "Nhãn QR đã sẵn sàng để tải xuống hoặc in; giữ nguyên mã truy xuất hiện có.";

    public const string MSG100 = "Đã cập nhật thông tin lô thu hoạch thành công.";

    public const string MSG101 = "Mã lô đã tồn tại. Vui lòng chọn mã khác.";

    public const string MSG102 = "Hạn sử dụng phải sau ngày thu hoạch.";

    public const string MSG103 = "Số lượng, đơn vị hoặc số dư tồn kho không hợp lệ. Không được ảnh hưởng lượng đã giữ chỗ hoặc đã xuất.";

    public const string MSG120 = "Thông tin chưa hợp lệ. Vui lòng kiểm tra các trường được đánh dấu; dữ liệu hợp lệ đã được giữ lại.";

    public const string MSG121 = "Bạn không có quyền thực hiện thao tác hoặc truy cập dữ liệu của nông trại này. Tài khoản phải hoạt động và hồ sơ nông trại đã được duyệt.";

    public const string MSG122 = "Yêu cầu xung đột với dữ liệu hiện tại hoặc đã được xử lý. Vui lòng tải lại; không thực hiện thao tác trùng lặp.";

    public const string MSG123 = "Không thể lưu dữ liệu hoặc bằng chứng. Không có thay đổi nghiệp vụ dở dang được lưu. Vui lòng thử lại.";

    public const string MSG124 = "Không có dữ liệu phù hợp với điều kiện đã chọn.";

    public const string MSG126 = "Không thể tải dữ liệu. Vui lòng thử lại.";

    public const string MSG128 = "Mùa vụ đã chuyển sang giai đoạn thu hoạch.";

    public const string MSG129 = "Đã ghi nhận hủy mùa vụ hoặc mất mùa. Lịch sử và các cam kết hiện có được giữ nguyên.";

    public const string MSG130 = "Thay đổi này ảnh hưởng sản lượng hoặc lịch giao đã cam kết nên không thể được chấp nhận.";

    public const string MSG131 = "Không thể thực hiện thao tác ở trạng thái hiện tại hoặc mở lại mùa vụ đã kết thúc.";

    public const string MSG132 = "Đã thêm bản đính chính. Bản gốc, lý do và liên kết đính chính được giữ nguyên.";

    public const string MSG133 = "Chỉ có thể ghi hoạt động mới cho mùa vụ đang trồng, sinh trưởng hoặc thu hoạch. Mùa vụ đã kết thúc chỉ cho phép đính chính bản gốc hiện có.";

    public const string MSG134 = "Đã lưu lô kế hoạch. Chưa phát sinh hàng thực tế hoặc QR hoạt động.";

    public const string MSG135 = "Đã ghi nhận thu hoạch và nhập kho một lần; tồn khả dụng đã được tính lại.";

    public const string MSG136 = "Chưa thể cấp QR: cần ngày thu hoạch thực tế hợp lệ và ít nhất một nhật ký của mùa vụ.";

    public const string MSG137 = "Đã cấp QR cho lô thu hoạch; mã truy xuất được liên kết cố định với lô này.";

    public const string MSG138 = "Đã ghi nhận vấn đề hoặc điều chỉnh có lý do và lưu lịch sử. Hàng nghi vấn không được phân bổ mới; chưa có quyết định phê duyệt thu hồi.";

    public const string MSG139 = "Lô kế hoạch, hết hạn, bị thu hồi hoặc bị chặn không đủ điều kiện cấp/cung cấp nhãn QR hoặc phân bổ hàng mới.";

    public static string Field(string label)
    {
        var name = label switch
        {
            "seasonName" => "tên mùa vụ",
            "cropVariety" => "giống cây",
            "cultivatedArea" => "diện tích",
            "yieldUnit" or "unit" => "đơn vị",
            "actionType" => "loại hoạt động",
            "detailDescription" => "mô tả công việc",
            "performedBy" => "người thực hiện",
            "inputMaterial" => "vật tư",
            "dosage" => "liều lượng / cách dùng",
            "correctionReason" => "lý do đính chính",
            "reason" => "lý do",
            "batchCode" => "mã lô",
            "qualityNotes" => "ghi chú chất lượng",
            _ => "thông tin đã nhập"
        };
        return $"Vui lòng kiểm tra {name}; không để trống hoặc chỉ nhập khoảng trắng và không vượt độ dài cho phép.";
    }


    public const string DateRange = "Khoảng ngày không hợp lệ hoặc ngày thực hiện nằm ngoài thời gian cho phép.";

    public const string Quantity = "Nhập số hợp lệ, tối đa 3 chữ số thập phân và trong giới hạn lưu trữ.";

    public const string DuplicateSeason = "Tên mùa vụ đã tồn tại trong nông trại.";

    public const string Unit = "Đơn vị phải nhất quán với sản phẩm hoặc có quy đổi hợp lệ.";

    public const string ProductSeason = "Sản phẩm phải thuộc nông trại và khớp với mùa vụ đã chọn.";

    public const string ProofRequired = "Vui lòng cung cấp ảnh bằng chứng hợp lệ cho thao tác này.";

    public const string StageDate = "Chưa đến ngày trồng đã lập trong kế hoạch.";

    public const string Completion = "Ngày hoàn thành phải từ ngày trồng đến hôm nay; sản lượng phải khớp tổng các lô đã thu hoạch. Hoàn thành thông thường yêu cầu sản lượng lớn hơn 0.";

    public const string Immutable = "Thông tin định danh và sản lượng nhập kho ban đầu không thể bị ghi đè; hãy dùng thao tác điều chỉnh có lý do.";
}

public sealed class FarmRuleException(
    string message,
    string code = "VALIDATION",
    string? field = null) : Exception(message)
{
    public string Code { get; } = code;

    public string? Field { get; } = field;
}
