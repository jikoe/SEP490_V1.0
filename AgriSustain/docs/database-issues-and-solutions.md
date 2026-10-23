# Database cho UC-ADM-03, UC-ADM-05 và UC-RC-02

Ngày: 18/10/2026. Đối chiếu với `AgriSustain.sql` do người dùng cung cấp, SRS và SDS.

**Đã áp dụng DB ngày 18/10/2026:** `(localdb)\MSSQLLocalDB` / `AgriSustain`, 47 bảng.
Đọc [bộ SQL và kết quả kiểm tra](../database/README.md). Đã backup và áp migration cho
schema 46 bảng đang có; không tạo lại dữ liệu và không sửa `AgriSustainDB` 29 bảng.
Code API vẫn dùng mock; trạng thái DB và trạng thái nối persistence là hai việc riêng.

**Trạng thái mới nhất:** xem [quyết định đợt 3](database-decisions-round-3.md).
DB-01 không thêm cột ảnh; DB-02/03/04 không thực hiện các phương án DB đã đề xuất;
DB-05 chọn B (checkout phân bổ FEFO, không thêm batch vào CartItem); DB-08 đã đồng ý.
DB-06 đang xem xét sự cần thiết; DB-09 đang xem xét lại thiết kế điểm, không mặc định
bắt buộc hai bảng mới. Người dùng đã xác nhận quy tắc xóa/ẩn thuộc `dbo.Category`;
DB-10 vẫn là quota voucher và giữ nguyên phương án đã đồng ý trước đó.
DB-07/11/12/13/14/15 giữ các đồng ý trước; số phút giữ hàng chưa chốt.

Role đúng là RetailCustomer; Admin tách SystemAdmin/CustomerSupport,...; địa giới theo
adminvsrm/vndata_hanhchinh; phone nội địa đã đồng ý; commission thường 2%, theo gói do
Admin đặt, khác phí vận chuyển. Code ba UC đã được đồng bộ các quy tắc đã chốt, có XML/JSDoc đầy đủ; adapter persistence
vẫn là mock. Xem [review code](three-uc-code-review.md). Migration schema đã chạy theo yêu cầu tiếp theo của người dùng; API chưa nối SQL.

## 1. Kết luận và trạng thái hiện tại

Schema đã có 46 bảng, gồm User, Category, Product, HarvestBatch, Cart, CartItem, Order,
SellerOrder, OrderItem, OrderItemBatchAllocation, Voucher, VoucherRedemption và AuditLog.
Không cần tạo lại các bảng đó. Các vấn đề chính là thiếu một số field/constraint và chưa
có hợp đồng transaction giữa các module.

Code hiện chạy bằng **adapter giả lập trong bộ nhớ**, theo yêu cầu chỉ giả lập phần phụ
thuộc chưa có. Logic ba UC là code thực trong Domain/Application/API và React. Chưa có
EF/SQL repository hoặc kết nối tới database thật. Audit, reservation, quote và request key
trong demo mất khi restart; semaphore chỉ bảo vệ một process. Các test demo không chứng
minh tính bền vững hoặc khả năng chạy nhiều instance trên SQL Server.

File đề xuất cũ đã được thay bằng thông báo dẫn tới
[migration chính thức](../database/migrations/20261018_approved_three_uc.sql).
Đã áp dụng migration bằng sqlcmd trên database đích sau kiểm tra riêng và backup.
Không có migration tự động khi app khởi động. DB-06/09 vẫn được để lại chưa áp.

## 2. Các vấn đề và giải pháp cụ thể

Tên bảng đối chiếu từ `AgriSustain.sql` gốc. “Hiện có” không có nghĩa là các cột/constraint
đề xuất đã tồn tại; “đề xuất mới” nghĩa là bảng chưa có trong SQL gốc và chưa được tạo thật.
DB-15 nêu phạm vi các bảng liên quan ba UC; quy ước UTC có thể áp dụng cho timestamp của
module khác khi nhóm đó triển khai, không có nghĩa sửa toàn bộ database trong đợt này.

| Mã | Bảng liên quan và trạng thái | Hiện trạng | Ảnh hưởng | Giải pháp đề xuất |
| --- | --- | --- | --- | --- |
| DB-01 | Hiện có: `dbo.Category` | Không cần ảnh danh mục theo quyết định mới | Không bổ sung chức năng lưu ảnh danh mục | Đã chốt không thêm ImageStorageKey/cột ảnh. CreatedAt là đề xuất độc lập chưa chốt; xóa/ẩn dùng quy tắc danh mục tại đợt 3, không mặc định thêm IsArchived/ArchivedAt |
| DB-02 | Hiện có: `dbo.Category` | Đề xuất unique theo cha/tên không được chọn | Không có bảo vệ unique bổ sung theo đề xuất này | Bỏ NormalizedName và unique index cha/tên khỏi script; không trình bày như yêu cầu bắt buộc. Code đã bỏ kiểm tra trùng tên theo quyết định này |
| DB-03 | Hiện có: `dbo.Category` (tự tham chiếu cha/con) | Đề xuất chống chu kỳ DB không được chọn | Giữ schema FK cha/con gốc | Không thêm CHECK self-parent hoặc cơ chế khóa cây trong kế hoạch DB này; code đã bỏ write guard; read giữ visited-set để không lặp vô hạn |
| DB-04 | Hiện có: `dbo.[User]`, `dbo.Category` | Đề xuất concurrency token không được chọn | Không bổ sung cơ chế Version vào SQL | Không thêm Version/rowversion vào hai bảng; đã bỏ Version khỏi entity/DTO; SessionVersion chỉ còn là mô phỏng auth riêng |
| DB-05 | Hiện có: `dbo.Cart`, `dbo.CartItem`, `dbo.Product`, `dbo.HarvestBatch`, `dbo.OrderItem`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement` | Đã chọn B: giỏ chọn product, checkout phân bổ lô | Một dòng sản phẩm có thể lấy hàng từ nhiều batch | Không thêm HarvestBatchId/FK chọn batch vào CartItem. FEFO, lưu allocation từng lô trong quote và OrderItemBatchAllocation, kiểm tra/giữ tổng từng lô trong transaction. Code đã phân bổ FEFO nhiều batch cho một dòng product |
| DB-06 | Hiện có: `dbo.[Order]`; quote mới đã đồng ý: `dbo.RetailCheckoutQuote` | Người dùng hỏi lại tính cần thiết của chống tạo đơn trùng | Hai đơn PendingPayment không tự thu tiền hai lần, nhưng nếu cùng được tạo có thể giữ hàng/voucher/điểm dư | Chưa duyệt request key riêng, bỏ DDL RetailRequestKey/RetailRequestHash khỏi script. Đề xuất tối giản một quote chỉ tạo một đơn bằng unique RetailQuoteId, còn chờ chốt; FK quote riêng chưa đủ chống trùng |
| DB-07 | Sửa chính: `dbo.[Order]`; đối chiếu tổng/phân bổ: `dbo.SellerOrder`, `dbo.OrderItem` (đều đã có) | Order chỉ có tổng discount, địa chỉ gộp và snapshot người nhận | Không tái hiện được delivery/payment method, notes, voucher/points đã xác nhận | Thêm field snapshot còn thiếu; giữ TotalAmount là tiền hàng, DiscountAmount là tổng giảm, FinalAmount là phải trả. Tách VoucherDiscount và PointDiscount. Dữ liệu lịch sử chưa biết để NULL, không suy đoán |
| DB-08 | Đề xuất mới đã đồng ý: `dbo.RetailCheckoutQuote`; liên kết `dbo.[User]`, `dbo.[Order]` đã có | Cần lưu bản tính tiền do server lập | Phát hiện thay đổi giá/địa chỉ/benefit/phân bổ trước khi đặt | Đã đồng ý bảng quote chứa buyer, payload snapshot, fingerprint, CreatedAt/ExpiresAt. Tái kiểm tra khi đặt; quote chưa giữ tồn và không mặc nhiên khóa giá. Bao gồm phân bổ nhiều lô theo DB-05 B |
| DB-09 | Có sẵn: `dbo.[User]`, `dbo.[Order]`; `dbo.RewardAccount`, `dbo.RewardPointReservation` chỉ là phương án mới, đang xem xét lại | Người dùng hỏi có thực sự cần hai bảng điểm không | Nếu checkout dùng điểm thì vẫn cần nguồn số dư và chống dùng vượt, nhưng không bắt buộc thiết kế hai bảng này | Không tự tạo hai bảng trong script. Có thể dùng module loyalty hoặc thiết kế tối giản trên User/Order sau khi chốt. Không tự bỏ tính năng điểm; tỷ lệ/cap/earn/expiry/refund chưa chốt |
| DB-10 | Hiện có: `dbo.Voucher`, `dbo.VoucherRedemption`, `dbo.[Order]` | Quota voucher phải tính cả lượt đã dùng và reservation còn hiệu lực | Chặn nhiều đơn PendingPayment giữ vượt quota | Người dùng đã xác nhận giữ nguyên phương án đã đồng ý: reserve quota cùng transaction đặt đơn; payment apply, hủy/hết hạn release đúng một lần; thêm ExpiresAt cho VoucherRedemption. Quy tắc xóa/ẩn danh mục thuộc Category, không áp vào voucher |
| DB-11 | Sửa tồn chính: `dbo.HarvestBatch`; ghi nhận giữ/biến động: `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement` (đều đã có) | AvailableQuantity/ReservedQuantity chưa có CHECK | Có thể âm hoặc oversell khi dùng read-then-write | Thêm CHECK không âm và UPDATE có điều kiện AvailableQuantity>=@quantity, kiểm tra @@ROWCOUNT. Khóa các batch theo ID tăng dần. Không trừ stock hai lần khi thanh toán: checkout đã chuyển available sang reserved |
| DB-12 | Hiện có: `dbo.[Order]`, `dbo.OrderItemBatchAllocation`, `dbo.HarvestBatch`, `dbo.InventoryMovement`, `dbo.VoucherRedemption`, `dbo.Voucher`; phần điểm theo DB-09 sau khi chốt | Lifecycle giữ/áp dụng/giải phóng đã đồng ý | Đơn bỏ thanh toán phải trả tài nguyên đúng một lần | Dùng ReservationExpiresAt của Order và ExpiresAt allocations/redemptions. Payment/worker xử lý nguyên tử, không release hai lần; không bắt buộc bảng RewardPointReservation riêng; số phút vẫn chưa chốt |
| DB-13 | Sửa: `dbo.AuditLog`; target có sẵn: `dbo.[User]`, `dbo.Category`, `dbo.[Order]` | AuditLog không có target ID riêng | Khó truy vết chính xác tài khoản/danh mục/đơn bị tác động | Thêm EntityId và TraceId nullable cho dữ liệu mới; giữ OldValue/NewValue JSON cùng Reason/PerformedByUserId. Không backfill target tùy tiện từ chuỗi cũ. Audit bắt buộc commit cùng thay đổi nghiệp vụ |
| DB-14 | Hiện có: `dbo.[User]`; bảng session/refresh token chưa có và chưa chốt tên/schema với nhóm auth | User.Status/LockedUntil có, không có session/refresh-token storage | Sửa Status không đồng nghĩa đã thu hồi refresh token hoặc cache phân quyền | Team auth bổ sung session/token-hash/revocation theo thiết kế; middleware kiểm tra trạng thái hiện tại. Demo chỉ mô phỏng version của phiên seed, không phát hành/refresh token thật |
| DB-15 | Timestamp ba UC ở `dbo.[User]`, `dbo.Product`, `dbo.HarvestBatch`, `dbo.Cart`, `dbo.CartItem`, `dbo.[Order]`, `dbo.SellerOrder`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement`, `dbo.Voucher`, `dbo.VoucherRedemption`, `dbo.AuditLog`; mới: `dbo.RetailCheckoutQuote`; phần điểm nếu DB-09 được chốt | DATETIME2 không có offset | Cần thống nhất timestamp mới là UTC | Đã đồng ý dữ liệu mới UTC bằng SYSUTCDATETIME(), không tự chuyển dữ liệu cũ; không mặc định thêm timestamp Category hoặc bảng điểm chưa chốt |
| DB-16 | Hiện có: `dbo.[User]` (Role/Status), `dbo.[Order]` (OrderType/OrderStatus); bảng role/permission tách riêng chưa chốt, không có sẵn trong SQL gốc | Role/status VARCHAR chưa có danh mục giá trị chuẩn; default order Pending | Consumer và Admin chung trong demo không khớp yêu cầu mới | Đã chốt RetailCustomer thay Consumer; tách SystemAdmin, CustomerSupport,... Cần chốt ma trận permission/target trước khi thay policy Admin. Checkout mới ghi rõ Retail/PendingPayment; không đổi dữ liệu cũ hàng loạt |

## 3. Giao dịch đặt đơn đề xuất

Một transaction SQL phải bao trùm toàn bộ kết quả đặt hàng:

1. Kiểm tra actor/ownership, quote và đơn đã tạo nếu chọn phương án chống trùng DB-06. Khi
   được chốt, dùng unique index/transaction làm bảo vệ cuối, không chỉ SELECT trước INSERT.
2. Khóa cart/selected rows và đọc quote của buyer. Tái kiểm tra sản phẩm, farmer, category,
   batch, giá, địa chỉ, voucher và điểm. Các policy ngoại vi cần được lấy/kiểm tra trước,
   không giữ transaction khi gọi mạng/storage/provider.
3. Nếu summary thay đổi, trả lỗi để người dùng xác nhận lại. Không tự đặt đơn theo giá mới.
4. Theo DB-05 B, kiểm tra lại phân bổ FEFO trong quote, khóa batch theo HarvestBatchId tăng
   dần và giữ bằng conditional UPDATE. Cộng tổng quantity của cùng batch trước khi kiểm tra.
5. Tạo Order PendingPayment, SellerOrder theo FarmerProfileId, OrderItem snapshot,
   OrderItemBatchAllocation Reserved và InventoryMovement theo policy đã chốt.
6. Reserve voucher quota; phần điểm theo contract DB-09 sau khi chốt. Lưu snapshot benefit. Các thao
   tác phải cùng DB transaction hoặc có thiết kế reservation/outbox tương đương.
7. Xóa đúng các CartItem đã đặt thuộc buyer, ghi AuditLog, rồi commit. Bất kỳ bước nào lỗi
   thì rollback cả stock/đơn/benefit/cart/audit.
8. Sau commit, trả order code và handoff sang UC-RC-03. Gọi PayOS/VietQR, tạo QR, webhook,
   chuyển Paid và giải phóng đơn hết hạn là module khác.

Ví dụ conditional update, là mẫu để team repository triển khai và test trên SQL Server:

```sql
UPDATE dbo.HarvestBatch
SET AvailableQuantity = AvailableQuantity - @Quantity,
    ReservedQuantity = ReservedQuantity + @Quantity
WHERE HarvestBatchId = @BatchId
  AND ProductId = @ProductId
  AND [Status] = 'Available'
  AND AvailableQuantity >= @Quantity;
-- Application phải kiểm tra @@ROWCOUNT = 1; nếu không, rollback toàn transaction.
-- Điều kiện ngày hết hạn/eligibility khác vẫn phải được kiểm tra trong cùng phạm vi khóa.
```

Nếu chốt DB-06, khi gặp unique-key conflict chống trùng, rollback transaction thua, đọc lại order đã
commit theo buyer/quote hoặc key đã chọn, kiểm tra intent rồi trả lại. Không retry thao tác có
side effect ngoài DB hoặc trả order của buyer khác.

## 4. Giá tiền và phân bổ nhiều người bán

- Quantity dùng DECIMAL(18,3); tiền dùng DECIMAL(18,2), không float/double.
- Chốt làm tròn ở unit price/line/discount trước production. Demo làm tròn từng line và
  discount tới 2 chữ số bằng AwayFromZero; đây là giả định được comment trong code.
- Voucher seller chỉ giảm subtotal của seller đó. Voucher sàn phân bổ theo subtotal;
  điểm phân bổ theo subtotal sau voucher; phí giao phân bổ theo số seller trong demo.
- Chia phần làm tròn và dồn dư cho seller đủ điều kiện cuối cùng theo thứ tự ID ổn định.
  Tổng FinalAmount các SellerOrder phải bằng FinalAmount Order; không để tiền giảm của
  seller khác bị trừ nhầm.
- Redemption chỉ có một voucher trong UC này. Chưa chốt policy về kết hợp voucher/điểm,
  phí miễn giảm, các đơn 0 đồng hoặc thay đổi tỷ giá điểm; không xem fixture là quyết định đó.
- Tiền giảm từng loại và dòng allocation phải bất biến sau khi đặt; thay giá sản phẩm sau
  này không được sửa snapshot đơn cũ.

## 5. Trạng thái quyết định nghiệp vụ sau phản hồi

| Quyết định | Giả lập hiện dùng | Quyết định / phần còn cần làm rõ |
| --- | --- | --- |
| Role | Code dùng SystemAdmin, RetailCustomer, Farmer, WholesaleBuyer; CustomerSupport fixture mặc định không được cấp quyền quản trị | Đã chốt tên RetailCustomer; tách các role Admin. Chưa chốt quyền cụ thể của CustomerSupport và quyền tác động tài khoản nội bộ |
| Danh mục xóa | Code đã bỏ archive, kiểm tra mọi product và các references khác | Quy tắc mới: không có product thì xóa vật lý nếu không vướng FK; có product bất kỳ thì chỉ ẩn/khóa. Bảng: dbo.Category/dbo.Product; FK khác: Category con, RegionalPriceBenchmark, CommissionPolicy |
| Vùng giao | Code dùng catalog JSON tỉnh/xã và chuẩn hóa bằng code; coverage vận chuyển vẫn giả lập mã 11/12 | Đã chọn JSON repo adminvsrm/vndata_hanhchinh. File thực là tỉnh/xã hai cấp; DTO/UI/backend đã đổi sang tỉnh–xã. Địa chỉ hợp lệ không tự xác nhận carrier giao được |
| Phone | Mobile nội địa 10 chữ số bắt đầu 03/05/07/08/09 | Đã đồng ý quy tắc này; +84/số cố định chưa được bổ sung |
| Phí vận chuyển | Standard 30.000; Express 60.000; thêm seller +10.000 | Vẫn chỉ là fixture, chưa chốt cách tính thực tế |
| Phí giao dịch | Chưa có logic commission trong ba UC demo | Đã chốt giao dịch thường 2%; theo gói subscription do Admin đặt. Cần làm rõ bên chịu phí, cơ sở tính, chủ thể có gói và thời điểm chốt rate |
| Điểm | 1 điểm = 100 VND; tối đa 20% subtotal sau voucher | DB-09 đang xem xét lại; không bắt buộc hai bảng mới, chưa tự bỏ tính năng điểm; policy còn chờ chốt |
| Thời hạn | Quote 5 phút; giữ hàng 15 phút | Đồng ý lifecycle DB-12; 5/15 phút vẫn là đề xuất, cần đồng bộ payment/worker |
| Batch | Code đã bỏ BatchId ở CartLine và phân bổ FEFO | Đã chọn B: checkout phân bổ FEFO, có thể nhiều lô cho một dòng product; đã có test API cho nhiều lô và chống cấp tồn hai lần |

## 6. Đã áp dụng và bước tích hợp tiếp theo

1. Đã tạo migration theo schema gốc 46 bảng; không thêm ảnh/Version/unique tên/selected
   batch hoặc các bảng điểm chưa chốt. Cột snapshot mới nullable, không backfill suy đoán.
2. Đã kiểm tra migration, chạy lại, dữ liệu legacy, cài mới và hành vi SQL trên database
   thử riêng; sau kiểm tra đã dọn database thử, giữ log trong tmp.
3. Đã backup COPY_ONLY/CHECKSUM, VERIFYONLY và áp migration lên AgriSustain đúng LocalDB
   người dùng chỉ định. So sánh SHA-256 các cột cũ của 46 bảng trước/sau trong transaction
   đạt; kiểm tra schema chỉ đọc sau commit đạt. Không sửa AgriSustainDB 29 bảng.
4. Tiếp theo cần repository/UoW SQL cho ba UC và mapping read model, FEFO nhiều lô, quy
   tắc danh mục mới, RetailCustomer/permission, địa chỉ JSON và các adapter của nhóm.
5. Khi có persistence thật mới chạy integration API/SQL: hai process cạnh tranh stock/quota,
   crash/rollback, audit failure và lifecycle payment. DB-06/09 phải chốt trước khi nối
   các cơ chế chống trùng/điểm tương ứng; không gọi kiểm tra schema là test đầy đủ checkout.
6. Giữ guard Development/demo tới khi auth, persistence và policy thật đã được đăng ký.

## 7. Tham khảo kỹ thuật

- [SQL Server unique indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-unique-indexes).
- [SQL Server transaction locking](https://learn.microsoft.com/en-us/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide).
- [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

Schema thuộc phần đã chốt đã được sửa và kiểm tra trên SQL Server LocalDB; các mô tả về
runtime demo và phần tích hợp còn thiếu vẫn áp dụng. Xem database/README.md để phân biệt
kiểm tra schema/SQL với integration test của API dùng persistence thật.
