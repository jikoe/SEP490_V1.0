# UpdateDB và dữ liệu mẫu

## Cách chạy đơn giản

1. Pull commit chứa code và thư mục AgriSustain/database.
2. Dừng API; backup database nếu có dữ liệu cần giữ.
3. Mở Update_DB.sql trong SSMS, kết nối instance của máy mình, chạy toàn bộ file.
4. Chạy tiếp `20261029_profile_fields.sql` và `20261030_bank_accounts.sql` (bảng SupportedBank,
   UserBankAccount và cột tài khoản nhận trên RefundTransaction/FarmerPayout mà code đang map).
   Hai file chạy lại nhiều lần vẫn an toàn. Thiếu bước này API báo MSG123/MSG126.
5. Build/chạy lại API với ConnectionStrings:MyCnn trỏ đúng instance.

Một file chứa cả schema và INSERT; không cần mở bốn migration riêng.
Bản Downloads/Update_DB (1).sql có cùng nội dung với bản trong repository.
Git chia sẻ file SQL đã commit/push; không tự đồng bộ database giữa các máy.
Chưa commit/push tự động trong lần sửa này.

## Cách hoạt động

- Chưa có AgriSustain: tạo database và bảng theo model EF hiện tại.
- Đã có database: bổ sung bảng/cột còn thiếu, không DROP database, xóa hàng hoặc
  thay kiểu cột hiện có. Cột thêm vào bảng cũ cho phép NULL để giữ dữ liệu lịch sử;
  không tự suy đoán giá trị snapshot/foreign key để backfill hàng cũ.
- Sau schema, nạp 340 dòng demo vào 54 bảng từ nội dung Insert.sql được cung cấp.
  Schema có 55 bảng nghiệp vụ do có thêm CategorySettings; bảng kỹ thuật
  __AgriSustainSeedHistory ghi nhận bộ demo đã nạp.
- Dùng SCOPE_IDENTITY và bảng ánh xạ để liên kết FK dù identity đã có khoảng trống
  hoặc có dữ liệu khác. Không giả định ID bắt đầu từ 1.
- Seed chạy trong một transaction, có sp_getapplock và marker. Chạy lại cùng bộ
  demo bỏ qua phần chèn. Không reset mật khẩu hoặc role của tài khoản cũ.
- Nếu username demo đã có nhưng không có marker, dừng với lỗi 51112, không ghi đè.
  Email, tên danh mục, mã đơn/hợp đồng trùng cũng có thể bị constraint từ chối.
  Không xóa dữ liệu trùng hoặc tự đánh dấu seed thành công để bỏ qua lỗi.
- Các phần schema commit riêng. Nếu một phần lỗi, bản gộp dùng NOEXEC để bỏ qua
  các phần sau trong SSMS. Seed lỗi rollback dữ liệu mẫu của lượt đó; các phần
  schema đã commit trước vẫn được giữ.

Muốn chỉ nâng schema, đổi dòng sau ở đầu Update_DB.sql từ 1 thành 0:

```sql
EXEC sys.sp_set_session_context @key=N'AgriSustain.SeedDemo', @value=1;
```

## Tài khoản demo

Password: 123456, hash BCrypt hợp lệ. Admin: admin; khách bán lẻ: retail1..retail5;
nông dân: farmer1..farmer5. Đây là dữ liệu phát triển, gồm giao dịch thanh toán/
quyết toán giả lập, không phải giao dịch xác minh với cổng thanh toán thật.
Chỉ tài khoản mới trong bộ demo nhận hash này. Đã bỏ UPDATE mật khẩu và xóa khóa
tạm của toàn bộ User ở cuối file gốc.

## Lỗi đã sửa trong Insert.sql

- File gốc gọi nhiều usp_* không tồn tại trong baseline: lock, stock movement,
  cart, contract seal, points, reservation, checkout, payment và validator.
  Bản mới dùng DML cho fixture và sp_getapplock có sẵn; không tạo procedure rỗng,
  tắt trigger hoặc khẳng định đã triển khai workflow nghiệp vụ của các procedure.
- Bổ sung schema compatibility theo metadata EF cho các bảng/cột thiếu, gồm
  BusinessPolicy, phân bổ thanh toán, payment event, preorder reservation và yêu
  cầu bằng chứng khiếu nại. Không chỉnh C# hoặc áp dụng lên DB trong bước này.
- Bỏ OrderItem.HarvestBatchId khỏi INSERT; lô liên kết qua bảng phân bổ.
- Thay ID cố định bằng ánh xạ ID thật; map cả ID trong Notification/PayloadJson
  và AuditLog, không chỉ các khóa ngoại có constraint.
- Chuẩn hóa role staff/admin và Approved của FarmerProfile/WholesaleBuyerProfile
  theo code; tạo hash BCrypt đúng cho tài khoản demo.
- Ghi rõ snapshot/trạng thái của fixture: tồn kho, voucher UsedCount, evidence
  request, payment event và point ledger. Sửa commission/net payout để khớp
  chính sách hoa hồng mẫu. Quote lịch sử được đặt hết hạn; order có hạn reservation.

## File thành phần và giới hạn xác minh

Update_DB.sql đã gộp schema compatibility, các migration cũ và Insert.sql.
Insert.sql là phần seed riêng. Upgrade_existing_database.sql là runner SQLCMD
chỉ nâng schema. Giữ các file thành phần để truy vết; người dùng chỉ cần bản gộp.

Đã kiểm tra cú pháp bằng Microsoft ScriptDom; bản gộp và seed không có lỗi cú pháp.
Đã kiểm tra cột INSERT, giá trị bắt buộc, giới hạn chuỗi và tham chiếu ID theo model.
Chưa thực thi bản mới trên SQL Server hoặc chạy test suite; kiểm tra tĩnh không
xác nhận trigger/constraint tùy biến trên database khác. Procedure/trigger của
schema khác không được phục dựng chỉ từ file INSERT; nếu có, seed vẫn chịu chúng.

Migrations không backfill dữ liệu cũ, đổi kiểu cột hiện có hoặc tạo view nghiệp vụ
chưa được cung cấp. Đủ bảng/cột không xác nhận mọi module cũ đã đúng nghiệp vụ.
Checkout vẫn cần cấu hình vùng giao hàng, phí và chính sách điểm trong
appsettings.Checkout.json; seed không tự thay đổi cấu hình ứng dụng.
