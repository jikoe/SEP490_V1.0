# Database AgriSustain — bản đã cập nhật

Đã áp dụng ngày 18/10/2026 lên **`(localdb)\MSSQLLocalDB` / `AgriSustain`** bằng Windows
Authentication. Database đích đã tồn tại với đúng schema 46 bảng nên dùng migration,
không tạo lại bảng. Sau cập nhật có **47 bảng**: 46 bảng gốc và `RetailCheckoutQuote`.
Kiểm tra chính xác tại thời điểm áp dụng: 0 dòng dữ liệu nghiệp vụ. Database 29 bảng
`AgriSustainDB` không bị thay đổi. Ngày 19/10 đã bổ sung dữ liệu mẫu theo yêu cầu mới; xem `demo-data-and-web.md`.

Nguồn là file `AgriSustain.sql` người dùng cung cấp, không phải thiết kế UUID/snake_case
`Database_Design_AgriSustain.sql` ở root workspace. SHA-256 file nguồn:
`918A21EA2930DA94FBF4223FEEB9F74C5D74AF0795F0CC98232CECDA4BD98A05`.

## 1. File sử dụng

| File | Mục đích |
| --- | --- |
| `AgriSustain.sql` | Cài mới trong database trống đã chọn; gồm baseline 46 bảng và phần cập nhật |
| `migrations/20261018_approved_three_uc.sql` | Nâng cấp schema BIGINT 46 bảng; chạy lại được |
| `apply-with-integrity-check.sql` | Bao migration trong transaction, so sánh số dòng và SHA-256 toàn bộ cột cũ trước/sau rồi mới commit |
| `verify-approved-schema.sql` | Kiểm tra schema; fixture hành vi chỉ bật trên database thử riêng |

Các file dùng **SQLCMD mode** và biến `TargetDatabase`, không tự chọn server/database.
Trong SSMS bật SQLCMD Mode; trên terminal dùng `sqlcmd`. Không chạy file cài mới lên
database đã có bảng. File đề xuất cũ trong `docs/database` đã được thay bằng thông báo
trỏ tới bộ script này, để tránh dùng nhầm phiên bản review cũ.

## 2. Các thay đổi đã áp dụng

| Quyết định | Bảng / thay đổi |
| --- | --- |
| DB-01..04 | Không thêm ảnh, archive, NormalizedName, unique tên cha, CHECK self-parent hoặc Version vào `Category`/`User` |
| DB-05 B | `CartItem` giữ ProductId, không thêm HarvestBatchId. `OrderItemBatchAllocation` có sẵn hỗ trợ nhiều lô; thêm index đọc lô theo ProductId/Status/ExpirationDate |
| DB-07 | `Order`: RetailQuoteId, DeliveryMethod, PaymentMethod, OrderNotes, VoucherCodeSnapshot, VoucherDiscount, PointDiscount, RedeemedPoints và các snapshot địa chỉ tỉnh/xã/phiên bản catalog. `SellerOrder`: SellerNameSnapshot; `OrderItem`: ProductNameSnapshot. Các cột snapshot mới nullable, không suy đoán đơn cũ |
| DB-08 | Tạo `RetailCheckoutQuote` với buyer, snapshot JSON, fingerprint, CreatedAt/ExpiresAt, kiểm tra JSON/thời hạn và FK tới User. Order tham chiếu quote bằng FK |
| DB-10/12 | `VoucherRedemption.ExpiresAt` và index kiểm tra quota theo VoucherId/Status/ExpiresAt. Thời hạn cụ thể do policy cung cấp, không mặc định 15 phút |
| DB-11 | CHECK AvailableQuantity/ReservedQuantity không âm trên `HarvestBatch` |
| DB-13 | `AuditLog.EntityId`, `TraceId`; EntityId không có FK để vẫn giữ target của danh mục đã xóa |
| DB-15 | Đổi default timestamp sang SYSUTCDATETIME trên User, Product, Cart, CartItem, InventoryMovement, Order, OrderItemBatchAllocation, VoucherRedemption, AuditLog; không đổi timestamp đã lưu hoặc cột DATE |
| Địa chỉ JSON hai cấp | `DeliveryAddress`: ProvinceCode, WardCode, AddressCatalogVersion; District cho phép NULL. Giữ ProvinceCity/Ward/AddressDetail và các địa chỉ cũ |
| Xóa danh mục | Giữ NO ACTION FK từ Product, Category con, RegionalPriceBenchmark, CommissionPolicy. Có product bất kỳ thì DB chặn DELETE; danh mục hoàn toàn không có liên kết có thể DELETE; ẩn/khóa dùng IsActive |

Không có cột request key/hash hoặc unique quote-order (DB-06 chưa chốt); FK tới quote
một mình không ngăn hai đơn dùng một quote. Không tạo hai bảng điểm (DB-09 đang xem xét).
Các field điểm trên Order chỉ là snapshot, không phải số dư hoặc cơ chế giữ điểm.

Role RetailCustomer/SystemAdmin/CustomerSupport,... có thể lưu trong User.Role hiện có;
chưa thêm CHECK role hoặc đổi hàng loạt dữ liệu theo ma trận quyền chưa chốt. Không tự
tạo module session/JWT, bảng role/permission hoặc policy subscription ngoài ba UC.
Phí giao dịch 2% đã được ghi nhận; bên chịu phí, cơ sở tính và mapping gói chưa chốt nên
không tự seed commission vào dữ liệu nghiệp vụ. Bản gốc vẫn giữ bảng CommissionPolicy.

## 3. Cách chạy lại trên bản schema tương thích

Từ thư mục `AgriSustain/database`, sau khi chọn đúng database và có backup:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 -v TargetDatabase="AgriSustain" -i apply-with-integrity-check.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 -v TargetDatabase="AgriSustain" RunFixtures="0" -i verify-approved-schema.sql
```

Database đích lần này đã được áp dụng, không cần chạy cài mới. Đối với database trống
khác, tạo database đó trước, thay cả `-d` và `TargetDatabase` bằng tên tương ứng rồi dùng
`-i AgriSustain.sql`. Không dùng bộ migration này cho schema Accounts/Categories/Orders
29 bảng; script kiểm tra baseline và dừng nếu không khớp.

## 4. Kết quả kiểm tra

- Database thử riêng: tạo baseline 46 bảng, thêm 3 dòng fixture legacy, nâng cấp, chạy lại
  migration; role, timestamp, địa chỉ và snapshot chưa biết của fixture được giữ nguyên.
- Kiểm tra wrapper transaction/SHA-256 trước khi dùng trên database đích: đạt.
- Kiểm tra file cài mới đầy đủ: đạt, 47 bảng.
- Fixture SQL trên database thử: chặn tồn âm; conditional UPDATE không bán vượt; danh mục
  có product chưa publish không xóa được nhưng ẩn được; danh mục trống xóa được; quote
  chặn JSON và thời hạn sai; một OrderItem có hai batch allocation; địa chỉ không cần
  District và audit target ghi được. Fixture rollback, database thử đã được dọn.
- Database AgriSustain: backup COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY đạt; migration
  kiểm tra SHA-256 tất cả 46 bảng/cột cũ trước khi commit; kiểm tra schema chỉ đọc đạt.
- Không chạy fixture trên database ứng dụng, để không tăng identity counter của dữ liệu thật.

Backup trước cập nhật nằm trong `../../tmp/AgriSustain-before-three-uc-20261018-71432ab9.bak`.
Log tương ứng: `../../tmp/db-backup-AgriSustain.log`, `db-apply-AgriSustain.log`,
`db-verify-AgriSustain.log`, và `db-verify-*.log` của database thử.

## 5. Kết nối SQL cho ba UC (19/10/2026)

Đã thêm `seed-three-uc-demo.sql`, áp trên AgriSustain sau backup, và nối SQL adapter cho
UC-ADM-03/ADM-05/RC-02. Development chọn `Persistence:Mode=SqlServer` qua EF Core;
không tự fallback. Xem [Database First và appsettings](../docs/ef-core-database-first.md).
Repository/UoW dùng parameterized SQL, Serializable transaction, SQL application lock,
conditional stock update và audit cùng transaction. Không dùng EF hoặc thay schema mới.

```text
Server=(localdb)\MSSQLLocalDB;Database=AgriSustain;Integrated Security=True;TrustServerCertificate=True;
```

Xem [dữ liệu mẫu/cách chạy](demo-data-and-web.md), [luồng theo file](../docs/three-uc-file-flows.md).
Auth, điểm, phí/vùng giao và request-key retry vẫn có mock đã comment; apply/release/expiry
và payment thuộc phụ thuộc chưa triển khai. SQL không tự giải quyết DB-06/DB-09.
Bốn SQL integration tests dùng database GUID tự tạo/xóa, không target database ứng dụng.
