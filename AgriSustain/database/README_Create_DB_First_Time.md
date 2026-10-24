# Tạo AgriSustain lần đầu trên máy mới

Dùng **Create_DB_First_Time.sql**. File này độc lập, được làm sạch từ
`Update_DB (1).sql` người dùng cung cấp; file nguồn được giữ nguyên.

1. Cài SQL Server 2016 trở lên (Express, Developer hoặc LocalDB) và SSMS.
2. Mở SSMS, kết nối vào instance SQL Server của máy mới bằng tài khoản có quyền
   tạo database và các đối tượng bên trong database.
3. Mở `Create_DB_First_Time.sql`, chạy **toàn bộ file** bằng F5.
   Không cần bật SQLCMD Mode hoặc chạy thêm các file migration/Insert.sql.
4. Khi thành công, Messages có `AgriSustain first installation completed successfully.`
   Kết quả có `TableCount = 56`: 55 bảng nghiệp vụ và 1 bảng ghi nhận seed.
5. Sửa connection string của backend theo server/instance của máy mới,
   database là `AgriSustain`.

Mặc định `DECLARE @SeedDemo bit = 1` tạo schema và dữ liệu demo. Tài khoản
`admin`, `farmer1`–`farmer5`, `retail1`–`retail5` có mật khẩu `123456`.
Chế độ này có 24 tài khoản; giao dịch thanh toán, quyết toán và URL là dữ liệu
minh họa dùng khi phát triển.

Muốn tạo schema không có tài khoản/giao dịch demo, đổi thành:

```sql
DECLARE @SeedDemo bit = 0;
```

Chế độ này vẫn tạo `BusinessPolicy` mặc định (`PolicyId = 1`,
`PayoutDelayHours = 48`) để có cấu hình ban đầu.

Script chỉ nhận database chưa tồn tại hoặc database rỗng. Nếu đã có bảng,
view, procedure, function, synonym hoặc sequence, script dừng; dùng quy trình
nâng cấp riêng cho database đã dùng. Chạy lại sau khi cài thành công cũng dừng.
Nếu có lỗi thực thi trong transaction, schema và seed của lượt cài được rollback;
database mới tạo có thể còn rỗng để sửa lỗi rồi chạy lại. Không có lệnh DROP DB,
đường dẫn MDF/LDF cố định hoặc tài khoản SQL phụ thuộc máy cũ.

## Những điểm đã làm sạch

- Thay các khối compatibility/migration bằng CREATE TABLE trực tiếp, giữ đủ
  cột, kiểu dữ liệu, khóa ngoại, computed column và thứ tự phụ thuộc.
- Schema, index, trigger và seed cùng một transaction; bỏ session context,
  nhiều lần commit riêng và điều khiển NOEXEC giữa các batch.
- Bổ sung các ràng buộc bị bỏ qua do bảng đã được tạo trước các nhánh migration:
  JSON/hạn dùng checkout quote; điểm dương/loại point ledger; kích thước/loại ảnh
  danh mục và mặc định `IsArchived = 0`. Giữ kiểm tra tồn kho không âm.
- Kiểm tra ảnh yêu cầu MIME khác NULL khi có bytes, tránh CHECK nhận UNKNOWN.
- Bỏ 4 unique index trùng các alternate key: payment allocation, deposit
  allocation, harvest batch và checkout quote. Composite FK của order vẫn ràng
  buộc quote thuộc đúng người mua, không cần thêm FK đơn trùng chức năng.
- `InventoryMovement.RequestKey` dùng unique index có lọc `IS NOT NULL`, cho phép
  nhiều movement chưa có request key và vẫn chặn request key trùng khi có giá trị.
- Giữ nguyên nội dung fixture và ánh xạ identity; đặt tên dễ đọc cho các PK,
  unique index và hai FK của reward ledger vốn có tên sinh tự động.

## Phạm vi kiểm tra

Đã kiểm tra cú pháp bằng Microsoft ScriptDom với parser SQL Server 2016,
gồm cả SQL động tạo database/trigger; kiểm tra cột INSERT, cột bắt buộc,
NULL, độ dài chuỗi literal, thứ tự tạo bảng và tham chiếu tới 52 bảng ánh xạ ID.
Đã đối chiếu phần fixture giữ nguyên sau khi chuẩn hóa xuống dòng.

**Chưa thực thi trên SQL Server:** LocalDB hiện có trên máy không khởi động
và không kết nối được tại thời điểm kiểm tra. Kiểm tra tĩnh chưa xác nhận toàn bộ
hành vi thực thi, constraint và workflow của ứng dụng. Bản này không bổ sung các
procedure nghiệp vụ vốn không có trong file nguồn và không thay cấu hình backend.
