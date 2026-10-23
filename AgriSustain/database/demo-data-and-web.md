# Dữ liệu mẫu ba UC và cách kiểm tra trên web

Ngày 19/10/2026, đã chạy `seed-three-uc-demo.sql` trên
`(localdb)\MSSQLLocalDB` / `AgriSustain` sau khi xác nhận đúng schema 47 bảng và 0 user,
category, product, order. Đã backup/verify trước INSERT. Database `AgriSustainDB` 29 bảng
không bị thay đổi.

Đã kiểm thử seed hai lần trên database thử riêng, số lượng không tăng, FK/CHECK đạt.
Script chỉ thêm mẫu còn thiếu; không reset status, replenish cart hay khôi phục stock đã dùng.

| Bảng | Dữ liệu mẫu | Mục đích |
| --- | --- | --- |
| dbo.User | 10 | Hai SystemAdmin; hai buyer; locked, suspended, support; hai Farmer; WholesaleBuyer |
| dbo.FarmerProfile | 2 | Seller read model cho checkout; không triển khai quản lý farm |
| dbo.Category | 6 | Root, child, trống, ẩn, có product chưa publish |
| dbo.CropSeason | 2 | Đủ FK bắt buộc khi tạo HarvestBatch |
| dbo.Product | 5 | Ba offer dùng chính; một chưa duyệt/chưa publish; một chỉ có lô hết hạn |
| dbo.HarvestBatch | 5 | FEFO 3+20 kg cho cải, cam/cà rốt, một lô hết hạn |
| dbo.Cart / dbo.CartItem | 2 / 4 | Buyer chính có 2 dòng selected + 1 unselected; buyer đối chứng có 1 dòng |
| dbo.DeliveryAddress | 2 | Mỗi buyer sở hữu một địa chỉ code 12/268; District NULL |
| dbo.Voucher | 2 | DEMO3UC-WELCOME10 và DEMO3UC-RAUXANH20, thời hạn tương đối ngày seed |

Không seed Order/Payment/Redemption đã thanh toán, không tạo audit giả, số dư điểm hoặc
commission/subscription chưa chốt. PasswordHash `!DEMO-NO-LOGIN-HASH!` không dùng để
đăng nhập thật; auth vẫn là mock đã được cho phép.

## Chạy script

Từ `AgriSustain/database`:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 -v TargetDatabase="AgriSustain" -i seed-three-uc-demo.sql
```

Nếu chạy từ SSMS: bật SQLCMD Mode và khai báo `TargetDatabase` là đúng database đang chọn.
Không chạy seed trên master hoặc database khác bằng cách sửa guard.

Không giả định ID cố định; xem ID thực bằng query:

```sql
SELECT UserId, Username, FullName, Role, Status, LockedUntil
FROM dbo.[User] WHERE Username LIKE N'demo3uc[_]%' ORDER BY UserId;
SELECT CategoryId, ParentCategoryId, CategoryName, IsActive
FROM dbo.Category WHERE Description LIKE N'DEMO-THREE-UC:%' ORDER BY Priority;
SELECT ProductId, ProductName, ApprovalStatus, IsPublished
FROM dbo.Product WHERE Description LIKE N'DEMO-THREE-UC:%';
SELECT HarvestBatchId, ProductId, BatchCode, ExpirationDate, AvailableQuantity, ReservedQuantity
FROM dbo.HarvestBatch WHERE BatchCode LIKE 'DEMO3UC-%' ORDER BY ProductId, ExpirationDate;
```

AddressCatalogVersion của saved address ban đầu là NULL, không tự đoán hash.
Khi preview, catalog server xác nhận code pair và ghi tên/version vào quote snapshot.

## Chạy web đọc/ghi SQL

`appsettings.Development.json` chọn `Persistence:Mode=SqlServer`. Connection string
Windows Authentication đặt ở `src/AgriSustain.Api/appsettings.json`, trỏ đến
`(localdb)\MSSQLLocalDB` / `AgriSustain`. Ba UC dùng scaffolded DbContext và EF repositories;
xem [lệnh Database First](../docs/ef-core-database-first.md).
`Demo:Enabled=true` và môi trường Development vẫn bắt buộc vì auth/policy còn giả lập.
Không có fallback sang InMemory nếu SQL lỗi. `/health` cho biết mode, chỉ là liveness,
không thay kiểm tra kết nối DB. `/health/database` thực sự mở SQL và đọc Category.

Từ thư mục AgriSustain:

```powershell
npm --prefix frontend run build
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src/AgriSustain.Api --configuration Release --no-launch-profile -- --urls http://127.0.0.1:5181
```

Mở http://127.0.0.1:5181. Dùng 5181 khi Visual Studio đang chiếm 5179; chỉ dừng đúng
process của mình nếu cần đổi cổng. Visual Studio profile Development hiện vẫn dùng 5179.

- SystemAdmin demo: 10 tài khoản SQL, tìm/lọc/phân trang; xem Active, Suspended và khóa tạm.
  Hủy không ghi; xác nhận đổi trạng thái lưu `dbo.User` và `dbo.AuditLog` cùng transaction.
- Danh mục: 6 mẫu SQL. `[DEMO] Danh mục chưa dùng` xóa được; danh mục có product chưa
  công bố không xóa được nhưng ẩn được; root còn child/reference không xóa được.
  Nút tự thao tác admin dùng actor ID server trả về, không giả định ID luôn bằng 1.
- Chọn RetailCustomer demo: context đọc `dbo.CartItem`/`dbo.DeliveryAddress` và product,
  inventory, voucher SQL; địa giới server xác nhận code 12/268. Hai dòng selected, một
  unselected; cải 5 kg lấy FEFO 3 kg lô FIRST + 2 kg lô SECOND.
- Voucher dùng **DEMO3UC-WELCOME10** / **DEMO3UC-RAUXANH20**. 100 điểm là fixture.
  Giỏ gốc: hàng 205.000 + giao 40.000 − voucher 20.500 − điểm 10.000 = **214.500 VND**.
  Tính lại và xem tổng thực tế vì giá/tồn/cart SQL có thể đã thay đổi sau demo.
- Quote lưu `dbo.RetailCheckoutQuote`; đặt hàng ghi Order → SellerOrder → OrderItem →
  OrderItemBatchAllocation, chuyển available sang reserved, ghi VoucherRedemption Reserved,
  xóa đúng selected cart và ghi audit trong **một SQL transaction**.
  Thành công là PendingPayment, chưa phải đã thanh toán. Không tự tạo QR/payment.
- Restart API giữ dữ liệu SQL, nhưng reset điểm mock, retry-key cache và phiên mock.
  Không cam kết chống đặt trùng bền vững sau restart (DB-06), hoặc giữ điểm SQL (DB-09).
  Không có worker thanh toán/hoàn tồn/release voucher; trạng thái Reserved cần nhóm phụ
  thuộc xử lý khi tích hợp, không dùng demo này để mô phỏng hoàn tất vòng đời thanh toán.
- Phí giao 30k/60k +10k seller thêm, cap điểm20%, TTL5/15 phút chỉ là policy mẫu.
  Commission thường2% khác phí giao; chưa tự cộng vào tiền khách.

Seed chạy lại **không reset cart/tồn/status** đã thay đổi. Dùng DB thử riêng nếu cần demo
đặt đơn nhiều lần; không reset dữ liệu đang dùng chỉ để chạy test.

## Bằng chứng và dữ liệu

**Phần test bên dưới thuộc phiên bản trước khi chuyển EF Core/UI.** Bản hiện tại chỉ
build + kiểm tra GET/preview/giao diện; chưa chạy lại bộ test chi tiết theo yêu cầu chờ duyệt.

Seed đã được áp và kiểm tra FK/CHECK; bốn bài `SqlPersistenceTests.cs` chạy trên database
GUID riêng, tự tạo schema+seed và tự dọn. Kiểm chứng persistence, rollback audit thật,
FEFO/two sellers/voucher/cart và hai host có semaphore độc lập tranh tồn cuối.
Hai host này cùng test runner OS process; case hai OS process độc lập vẫn Pending.

Lượt regression đầu đã phát hiện fixture cũ chỉ đổi configuration quá muộn; nó có chạm
dữ liệu demo. Đã backup trạng thái sự cố, hoàn nguyên đúng thay đổi do lượt chạy và sửa
fixture để thay rõ cả UoW/identity reader; lượt regression sau không chạm database ứng dụng.
Trước khi mở web kiểm tra, target trở lại10 user/6category/5product/5batch/4cartitem,
0order/0redemption/0audit; UI preview có thể bổ sung quote bình thường.

Backup trước seed: `../../tmp/AgriSustain-before-demo-seed-20261019.bak`.
Backup sự cố cô lập test: `../../tmp/AgriSustain-test-isolation-incident-20261019.bak`.
Log SQL cuối: `../../tmp/sql-r2-final-list/*.trx`; nguồn test và comment ở project.
Backup/log không phải source code để đưa lên Git.
