# UC-FIN-04: chi trả bằng QR và đối soát tiền ra

- Frontend: `Client/FinanceStaff/farmer-payouts.html`; thanh bên Finance Staff → Chi trả cho nông dân.
- Nguồn tiền lấy từ `Payment:SePay` trong cấu hình hiện có. Không thay tài khoản MB của checkout sang TPBank.
- QR chứa tài khoản nhận đã xác minh của Farmer, số tiền thực trả và mã phiếu FPO riêng.
- Tạo phiếu giữ nguồn tiền; mở QR vẫn chưa phải thanh toán. Chỉ giao dịch tiền ra SePay được xác thực, khớp tài khoản nguồn, ngân hàng, mã phiếu, số tiền và thời gian mới chuyển sang Processed.
- Cơ sở xác nhận là tiền đã ra khỏi tài khoản nguồn. Không yêu cầu Farmer xác nhận đã nhận tiền.
- Sau khi mở QR, không hủy/giải phóng nguồn bằng cách đóng trang hoặc vì chưa nhận được giao dịch. Kiểm tra lại cùng giao dịch, không tạo lệnh chuyển mới.

## Kết nối SePay

Khóa webhook hiện tại `Payment__SePay__WebhookApiKey` được dùng lại. Endpoint riêng:

`POST https://<public-host>/api/finance/farmer-payouts/sepay-webhook`

Webhook chung hiện có cũng phân luồng `transferType=out` sang xử lý chi trả; `in` vẫn theo checkout cũ.
Outgoing webhook yêu cầu khóa cấu hình thật trong header Authorization: Apikey ..., không dùng chế độ bỏ qua xác thực.

SePay chuẩn hiện chỉ hỗ trợ webhook tiền ra cho TPBank, Sacombank và VietinBank. Việc MB đã liên kết để nhận tiền checkout không tự bảo đảm có dữ liệu tiền ra.
Với nguồn MB, cấu hình token quyền đọc trong `.env`:

```dotenv
Payment__SePay__ApiToken=<token truy vấn giao dịch, khác khóa webhook>
```

Không commit token. Đồng thời kiểm tra lịch sử SePay thực sự có giao dịch tiền ra của tài khoản MB này.
Backend chỉ gọi API truy vấn lịch sử giao dịch của SePay, không gọi API chuyển tiền. Không có dữ liệu/không truy vấn được thì tiếp tục giữ trạng thái chờ đối soát, không tự đánh dấu thành công hoặc thất bại.
Nếu chưa cấu hình đường nhận dữ liệu tiền ra cho MB, màn hình chặn mở QR và nêu rõ cấu hình còn thiếu.

## Trạng thái và thông báo

Prepared → Processing → Processed; khi nhận giao dịch chưa khớp: NeedsReconciliation.
Một mã giao dịch SePay chỉ quyết toán một lần. Không dùng số dư thay cho chứng từ giao dịch.
Email và thông báo InApp được ghi cùng giao dịch quyết toán; worker dùng Gmail gateway thật, thử lại email lỗi mà không lặp chi tiền.
Không chỉnh sửa code test hay tài liệu thiết kế trong đợt triển khai này.
