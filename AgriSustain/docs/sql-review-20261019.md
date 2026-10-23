# Kiểm tra SQL ngày 19/10/2026

Phạm vi chính: `database/AgriSustain.sql`, migration
`20261018_approved_three_uc.sql`, wrapper kiểm tra toàn vẹn, script verify và seed.
Theo `database/README.md`, đây là bộ schema ứng dụng hiện sử dụng.
File `../../Database_Design_AgriSustain.sql` là thiết kế riêng UUID/snake_case;
chỉ được kiểm chứng cài mới trong lần này, không được review toàn bộ nghiệp vụ.

Kết luận: cả hai file schema đều cài mới được trên LocalDB SQL Server 17.0.4025.3.
Bộ schema ứng dụng vẫn có các điểm thiếu ràng buộc dưới đây. Không chỉnh sửa SQL,
không áp migration/seed/fixture lên database ứng dụng trong lần kiểm tra này.

## Các vấn đề đã tái hiện

### 1. [P1] Chỉ tạo được một Shipment chưa có TrackingNumber trên toàn bảng

Vị trí: `database/AgriSustain.sql:388`.

`TrackingNumber VARCHAR(100) NULL UNIQUE` làm SQL Server chỉ chấp nhận một giá trị
NULL trong unique key. Sau khi tạo một shipment đang Preparing chưa có tracking,
INSERT shipment tiếp theo cũng chưa có tracking bị lỗi **2627**. Ràng buộc áp dụng
toàn bảng, nên đổi SellerOrderId không tránh được vấn đề.

Hướng sửa: cột vẫn cho phép NULL, thay unique constraint bằng unique filtered index
`ON dbo.Shipment(TrackingNumber) WHERE TrackingNumber IS NOT NULL`. Database đã cài
cần migration gỡ constraint hiện có; không chỉ sửa file cài mới.

### 2. [P2] Giá và số lượng âm vẫn được lưu

Vị trí: `database/AgriSustain.sql:141`, `:355`–`:359`.

Đã UPDATE thành công Product.BaseRetailPrice = -100 và INSERT OrderItem với
Quantity = -5, UnitPriceSnapshot = -100, SubTotal = -500. NOT NULL và kiểu DECIMAL
không giới hạn dấu. Migration chỉ bổ sung CHECK tồn không âm trên HarvestBatch,
không bổ sung kiểm tra các cột này.

Đường ghi trực tiếp/import hoặc module khác có thể đưa dữ liệu sai vào database,
khiến tính tiền và xử lý tồn phía sau nhận đầu vào không hợp lệ. Điều này không
khẳng định API checkout hiện tại cũng nhận đầu vào âm.

Hướng sửa: bổ sung CHECK Quantity > 0, giá/tiền không âm; thống nhất riêng trường
hợp giá bằng 0 trước khi siết thành > 0. Kiểm tra dữ liệu hiện có trước khi thêm
constraint WITH CHECK. Rà tương tự CartItem.Quantity và
OrderItemBatchAllocation.AllocatedQuantity.

### 3. [P2] Batch allocation không bảo đảm cùng sản phẩm với OrderItem

Vị trí: `database/AgriSustain.sql:378`–`:379`.

Hai FK độc lập chỉ bảo đảm OrderItemId và HarvestBatchId tồn tại. Đã INSERT thành
công allocation cho dòng đơn **cải ngọt**, dùng lô **cam** của nhà bán khác. Database
không kiểm tra OrderItem.ProductId = HarvestBatch.ProductId.

Khi ghi ngoài luồng checkout hiện tại, dữ liệu có thể trừ/giữ sai lô và sai truy xuất
nguồn gốc. Đây là thiếu bảo vệ ở tầng SQL; chưa phải bằng chứng API tạo allocation sai.

Hướng sửa: thiết kế FK ghép với ProductId và các unique key tương ứng, hoặc dùng
trigger kiểm tra theo tập hợp. Cần bảo vệ cả INSERT/UPDATE allocation và việc thay
ProductId của các bản ghi được tham chiếu; chỉ kiểm tra lúc INSERT chưa đủ.

### 4. [P2] Chứng từ được lưu khi không gắn nguồn nghiệp vụ

Vị trí: `database/AgriSustain.sql:618`–`:619`, `:652`–`:655`.

Đã tạo thành công Invoice với cả SellerOrderId và ContractMilestoneId NULL; đồng thời
tạo PaymentTransaction với toàn bộ OrderId/B2BSubscriptionId/ContractMilestoneId/
B2BContractId NULL. Các FK nullable không bảo đảm có nguồn để đối chiếu.

Hướng sửa: xác định tổ hợp FK hợp lệ theo loại chứng từ/PaymentPurpose rồi thêm CHECK.
Với Invoice nếu đúng quy tắc nguồn là SellerOrder hoặc ContractMilestone thì cần bắt
buộc nguồn tương ứng. Không tự đặt quy tắc chỉ một FK cho mọi PaymentTransaction:
có trường hợp milestone và contract cùng xuất hiện cần quyết định riêng.

## Kết quả thực thi

| Kiểm tra | Kết quả |
| --- | --- |
| Database AgriSustain hiện tại: verify với RunFixtures=0 | PASS, chỉ đọc |
| Cài mới database/AgriSustain.sql | PASS, 47 bảng |
| verify với fixture trong database thử | PASS, fixture rollback |
| Seed lần đầu và chạy lại | PASS, số dòng mẫu không tăng lần hai |
| Wrapper migration trên schema đã seed | PASS, SHA-256 giữ nguyên 40 dòng trên 47 bảng |
| Baseline 46 bảng + một User legacy rồi nâng cấp | PASS, SHA-256 giữ nguyên dòng legacy |
| Chạy wrapper migration lại sau nâng cấp | PASS |
| verify với fixture sau nâng cấp | PASS |
| Cài mới Database_Design_AgriSustain.sql ở root | PASS, 51 bảng |
| Bỏ AuditLog.TraceId trong database thử rồi verify chỉ đọc | Bị chặn lỗi 207, không báo PASS |

Việc verify có sẵn PASS không có nghĩa nó bao phủ toàn bộ quy tắc nghiệp vụ ở phần
trên. Không chạy HTTP/app test vì phạm vi kiểm tra lần này là SQL.

DB-06 (một quote/một đơn hoặc request key) và DB-09 (storage điểm) đã được ghi rõ
là chưa chốt trong tài liệu quyết định. Không coi việc chưa có unique quote hoặc
bảng điểm là lỗi cần tự sửa trong lần kiểm tra này.

## Bằng chứng và chạy lại

- Harness: `../../tmp/sql-review-20261019/run-review.ps1`.
- SQL tái hiện: `../../tmp/sql-review-20261019/reproduce.sql`.
- Log tái hiện: `../../tmp/sql-review-20261019/reproductions.log`.
- Các log cài mới/seed/migration/verify: cùng thư mục trên.

Harness tạo ba database riêng với tên `AgriSustain_SqlReview_<GUID>`, kiểm tra bằng
Windows Authentication, rồi DROP đúng ba tên do nó tạo trong khối finally. Cả ba
lần dọn database đều thành công. Các thao tác gây lỗi/dữ liệu bất hợp lệ chỉ chạy
trong database thử và được rollback.
