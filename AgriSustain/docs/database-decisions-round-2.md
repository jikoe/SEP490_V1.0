# Giải thích database và quyết định đợt 2

**Bản ghi lịch sử:** các lựa chọn DB-01–06, DB-08, DB-09 và xóa danh mục đã có phản hồi
mới. Đọc [quyết định đợt 3](database-decisions-round-3.md) và báo cáo tổng hợp để có trạng
thái hiện hành. Các đoạn đề xuất bên dưới không còn được xem là yêu cầu đang được chọn.

Ngày 18/10/2026. Chỉ phục vụ UC-ADM-03, UC-ADM-05, UC-RC-02 và hợp đồng tích hợp.
Tài liệu này ghi nhận phản hồi của người dùng; các đề xuất chưa được đồng ý được ghi rõ.
Đợt này cập nhật tài liệu, không thay schema thật hoặc code runtime.

## 1. Bảng liên quan và trạng thái từng mã DB

“Đã có” là bảng trong SQL gốc, không khẳng định cột đề xuất đã tồn tại. “Mới” là bảng
đề xuất, chưa tạo trên database thật. Bảng ledger, session/token và role/permission chưa
chốt tên/schema; không trình bày như bảng có sẵn. Các bảng của module phụ thuộc chỉ được
nêu để làm rõ hợp đồng phối hợp, không mở rộng phạm vi triển khai sang UC khác.

| Mã | Bảng liên quan | Trạng thái quyết định |
| --- | --- | --- |
| DB-01 | Đã có: `dbo.Category` | Ảnh tùy chọn, đang cân nhắc giữ/bỏ |
| DB-02 | Đã có: `dbo.Category` | Đang review unique tên theo cha |
| DB-03 | Đã có: `dbo.Category` (tự tham chiếu cha/con) | Đang review chống chu kỳ |
| DB-04 | Đã có: `dbo.[User]`, `dbo.Category` | Đang review Version chống ghi đè |
| DB-05 | Đã có: `dbo.CartItem`, `dbo.HarvestBatch`, `dbo.Product`, `dbo.Cart` | Đang review chọn batch trong giỏ hoặc phân bổ tại checkout |
| DB-06 | Sửa bảng đã có: `dbo.[Order]`; quote mới liên quan: `dbo.RetailCheckoutQuote` | Đang review request key chống tạo đơn trùng |
| DB-07 | Sửa bảng đã có: `dbo.[Order]`; đối chiếu: `dbo.SellerOrder`, `dbo.OrderItem` | Đồng ý snapshot checkout còn thiếu |
| DB-08 | Mới: `dbo.RetailCheckoutQuote`; liên kết bảng đã có: `dbo.[User]`, `dbo.[Order]` | Đang review lưu bản tính tiền server |
| DB-09 | Mới: `dbo.RewardAccount`, `dbo.RewardPointReservation`; liên kết: `dbo.[User]`, `dbo.[Order]` đã có; ledger chưa chốt tên | Đồng ý cơ chế giữ điểm; chưa chốt tỷ lệ/cap/earn/expiry/refund |
| DB-10 | Đã có: `dbo.Voucher`, `dbo.VoucherRedemption`, `dbo.[Order]` | Đồng ý giữ quota voucher cho đơn chờ thanh toán |
| DB-11 | Sửa tồn: `dbo.HarvestBatch`; ghi allocation/biến động: `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement` (đã có) | Đồng ý constraint tồn và cập nhật có điều kiện |
| DB-12 | Đã có: `dbo.[Order]`, `dbo.OrderItemBatchAllocation`, `dbo.HarvestBatch`, `dbo.InventoryMovement`, `dbo.VoucherRedemption`, `dbo.Voucher`; mới: `dbo.RewardPointReservation`, `dbo.RewardAccount` | Đồng ý lifecycle giữ/áp dụng/giải phóng; chưa chốt số phút |
| DB-13 | Sửa: `dbo.AuditLog`; target: `dbo.[User]`, `dbo.Category`, `dbo.[Order]` (đã có) | Đồng ý EntityId/TraceId và audit cùng transaction |
| DB-14 | Đã có: `dbo.[User]`; bảng session/refresh token chưa có, chưa chốt tên/schema | Đồng ý thu hồi phiên và kiểm tra trạng thái |
| DB-15 | Đã có: `dbo.[User]`, `dbo.Product`, `dbo.HarvestBatch`, `dbo.Cart`, `dbo.CartItem`, `dbo.[Order]`, `dbo.SellerOrder`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement`, `dbo.Voucher`, `dbo.VoucherRedemption`, `dbo.AuditLog`; thêm timestamp ở `dbo.Category`; mới: `dbo.RetailCheckoutQuote`, `dbo.RewardPointReservation` | Đồng ý UTC cho timestamp mới liên quan ba UC; không tự đổi dữ liệu cũ |
| DB-16 | Đã có: `dbo.[User]` (Role/Status), `dbo.[Order]` (OrderType/OrderStatus); bảng role/permission riêng chưa chốt | RetailCustomer thay Consumer; tách SystemAdmin, CustomerSupport,...; chờ ma trận quyền |

Các quyết định bổ sung:

| Nội dung | Quyết định |
| --- | --- |
| Phone | Giữ mobile nội địa 10 số, đầu 03/05/07/08/09; không tự thêm +84/số cố định. Bảng đã có: `dbo.[User]`, `dbo.DeliveryAddress`, `dbo.[Order]` (snapshot người nhận) |
| Địa giới | Dùng JSON của https://github.com/adminvsrm/vndata_hanhchinh. Bảng đã có liên quan địa chỉ: `dbo.DeliveryAddress`, `dbo.[Order]` (snapshot); chưa chốt tạo bảng catalog địa giới hay đọc JSON/cache |
| Phí giao dịch | Mặc định 2%; gói theo Admin. Bảng đã có cần đối chiếu: `dbo.CommissionPolicy`, `dbo.B2BSubscriptionPackage`, `dbo.B2BSubscription`, `dbo.PayoutItem`; chưa chốt mapping policy theo gói |

DB-01: đang hỏi có cần ảnh. DB-02, DB-03, DB-04, DB-05, DB-06, DB-08: yêu cầu giải thích,
chưa xem là đồng ý triển khai. Danh mục xóa, policy điểm, thời hạn và chọn batch vẫn đang review.

## 2. DB-01: bảng dbo.Category — danh mục sản phẩm có cần ảnh không?

**Bảng liên quan: `dbo.Category` (danh mục sản phẩm), không phải bảng `Product`.**
Các cột đề xuất `ImageStorageKey`, `CreatedAt`, `IsArchived`, `ArchivedAt` đều thuộc
`dbo.Category`. `ImageStorageKey` là ảnh minh họa cho danh mục, không phải ảnh của từng
sản phẩm. Phương án thêm cột hiện chưa được áp dụng lên database thật.

Không bắt buộc về nghiệp vụ phân loại. Danh mục tên “Rau củ” vẫn tạo/sửa, phân loại sản phẩm
và dùng trong checkout bình thường khi không có ảnh. Ảnh hữu ích nếu marketplace có ô danh mục
minh họa; đó là yêu cầu trình bày UI, không phải điều kiện toàn vẹn dữ liệu.

Demo hiện đã coi ảnh là tùy chọn. Đề xuất giữ nullable nếu nhóm còn muốn dùng hình minh họa;
nếu quyết định bỏ thì bỏ UI upload, storage adapter và field đề xuất này cùng nhau. Không cần
thêm bảng ảnh riêng cho một ảnh danh mục. Không gắn việc bỏ ảnh với bỏ CreatedAt/IsArchived/
ArchivedAt: các field sau phục vụ lịch sử và xóa mềm, độc lập với hình ảnh.

## 3. DB-02: chống trùng tên trong cùng danh mục cha

**Bảng kiểm tra/sửa: `dbo.Category`; cột liên quan: `CategoryName`, `ParentCategoryId`
và `NormalizedName` đề xuất mới.**

Ví dụ đã có `Rau củ / Rau ăn lá`. Một người nhập `rau ăn lá`, người khác nhập
` Rau ăn lá ` trong cùng cha. Đề xuất xem đó là cùng tên sau trim đầu/cuối và so sánh không
phân biệt hoa thường. CI/AS vẫn phân biệt dấu; không tự bỏ dấu hoặc gộp mọi khoảng trắng giữa tên.
`Hữu cơ` ở hai nhánh `Rau củ` và `Trái cây` có thể tồn tại theo quy tắc “duy nhất trong cùng cha”.
Hai danh mục gốc cùng tên cũng bị chặn; ParentCategoryId NULL không là lối bỏ qua unique.

Tại sao kiểm tra API chưa đủ: request A và B cùng kiểm tra khi tên chưa có, cả hai đều thấy
hợp lệ rồi cùng INSERT. Unique index `(ParentCategoryId, NormalizedName)` là bảo vệ cuối:
một request thành công, request còn lại nhận lỗi tên trùng được chuyển thành thông báo nghiệp vụ.
Phải xử lý dữ liệu trùng cũ trước khi tạo index. Đây là đề xuất, chưa chốt có cho dùng lại tên
đã archive không; script cũ hiện giữ cả tên archive trong unique index.

Nguồn: [Microsoft, unique indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-unique-indexes).

## 4. DB-03: ngăn cây danh mục tạo vòng

**Bảng kiểm tra/sửa: `dbo.Category`; quan hệ `ParentCategoryId` → `CategoryId`
trong chính bảng này.**

Cây hợp lệ: `Nông sản → Rau củ → Rau ăn lá`. Nếu chuyển “Nông sản” xuống làm con của
“Rau ăn lá”, cây thành vòng: đi theo cha sẽ quay lại điểm bắt đầu. Hậu quả là breadcrumb,
lọc theo nhánh và truy vấn đệ quy không có ý nghĩa. Self-parent là trường hợp đơn giản:
CategoryId=5 có ParentCategoryId=5. FK chỉ xác nhận ID cha tồn tại nên không đủ bảo vệ.

Đề xuất CHECK chặn tự làm cha; service đọc chuỗi tổ tiên để chặn cha mới là hậu duệ.
Kiểm tra và cập nhật phải được bảo vệ cùng transaction. Ví dụ A chuyển X dưới Y trong khi
B chuyển Y dưới X: cả hai đọc cây cũ có thể cùng vượt qua kiểm tra. Một cách triển khai SQL
đơn giản là lấy cùng application lock cho mọi thay đổi cấu trúc cây trong transaction,
đọc lại rồi cập nhật; cách khóa khác phải được chứng minh không tạo vòng khi chạy đồng thời.
Chưa triển khai SQL lock trong demo; semaphore demo chỉ dùng được trong một process.

## 5. DB-04: phát hiện màn hình đang sửa dữ liệu cũ

**Bảng kiểm tra/sửa: `dbo.[User]`, `dbo.Category`; đề xuất cột `Version` trên cả hai.**

Admin A và B cùng mở danh mục tại Version=7. A đổi tên và lưu: Version lên 8.
B còn màn hình cũ, đổi description rồi gửi toàn bộ form với tên cũ và Version=7.
Nếu không kiểm tra phiên bản, request B có thể ghi đè tên vừa sửa của A.

Đề xuất cập nhật theo `WHERE CategoryId=@id AND Version=7`, đồng thời tăng lên 8.
Nếu bản ghi hiện đã là 8, không cập nhật; API trả HTTP 409 và yêu cầu tải lại để xem/chọn
thay đổi cần lưu. Không tự ghi đè hoặc tự gửi lại thao tác quản trị theo dữ liệu mới.
Tương tự với trạng thái User. Version này khác với SessionVersion thu hồi phiên đăng nhập:
một cái bảo vệ ghi dữ liệu, cái kia vô hiệu hóa phiên. BIGINT hiện khớp contract demo;
nếu đổi sang SQL rowversion thì cần đổi biểu diễn API sang base64, không cast thành số.

Nguồn: [Microsoft, optimistic concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## 6. DB-05: biết người mua đang chọn đúng lô nào

**Bảng sửa theo hướng chọn lô: `dbo.CartItem` (thêm `HarvestBatchId`). Bảng đối chiếu:
`dbo.HarvestBatch`, `dbo.Product`; quyền sở hữu qua `dbo.Cart`. Các bảng này đã có.**

Product “Cải ngọt” có lô L01 còn 3 kg, hết hạn sớm và lô L02 còn 20 kg, hết hạn muộn.
Giỏ chỉ lưu ProductId + 5 kg thì chưa biết 5 kg thuộc lô nào. Nếu UI đã cho chọn L01,
checkout không được tự chuyển sang L02 hoặc lấy tồn tổng 23 kg rồi báo L01 đủ hàng.

Đề xuất A (khớp fixture hiện tại): giỏ lưu HarvestBatchId. Đặt 5 kg L01 thất bại vì chỉ còn
3 kg; khách đổi lựa chọn qua module giỏ. Composite FK `(HarvestBatchId, ProductId)` bảo vệ
không gán lô cam cho sản phẩm cải. Nếu muốn mua 2 lô, contract giỏ cần biểu diễn hai dòng/
allocation riêng và review lại unique cart item hiện có; không chỉ thêm một cột rồi kết luận đủ.

Đề xuất B: giỏ chỉ chọn product; checkout phân bổ FEFO theo hạn sử dụng gần nhất.
5 kg có thể lấy 3 kg L01 + 2 kg L02. Quote phải hiển thị/lưu phân bổ, order lưu các batch
allocation, giữ tồn từng lô trong cùng transaction. Hướng B không cần bắt người mua chọn lô
trong CartItem và có thể cần sửa SRS/UI/DTO hiện tại. Không áp đồng thời hai hướng âm thầm.
Cart legacy thiếu batch phải được xử lý theo hướng đã chọn, không gán batch đầu tiên.

## 7. DB-06: chống tạo hai đơn vì một lần đặt hàng

**Bảng kiểm tra/sửa: `dbo.[Order]`; thêm request key/hash, quote ID và unique index.
`dbo.RetailCheckoutQuote` là bảng mới đề xuất ở DB-08, không phải bảng có sẵn.**

Khách nhấn Đặt hàng. Server đã tạo đơn DH001 và giữ 5 kg, nhưng mạng đứt trước khi trình
duyệt nhận response. Khách bấm lại. Nếu mỗi request sinh order code mới thì có thể tạo DH002
và giữ thêm 5 kg. Cấm double click trên UI không xử lý được tình huống mất response này.

Frontend sinh requestKey K cho một ý định đặt hàng và giữ K khi retry. Server lưu unique
`(BuyerUserId, RetailRequestKey)` cùng request hash. Cùng người mua + K + cùng nội dung đã
xác nhận: trả DH001, không giữ hàng/trừ điểm/xóa giỏ lần hai. Cùng K nhưng nội dung khác:
409; yêu cầu xác nhận lại và dùng key mới. Khách cố ý đặt một đơn mới phải có key mới.
Key do client cung cấp không được dùng để đọc đơn người khác; luôn ràng buộc buyer từ phiên.

Request key phải lưu cùng transaction với đơn, bền vững sau restart. Hai request đồng thời
vẫn cần unique constraint và xử lý transaction thua; không chỉ SELECT trước INSERT.
Hash mô tả ý định đã xác nhận, không tính lại theo một giỏ đã bị xóa sau đơn thành công.
Đường retry phải đọc đơn cũ trước khi từ chối quote đã quá hạn; DB-06 bảo vệ tạo đơn,
không thay idempotency hoặc đối soát giao dịch thanh toán của UC-RC-03.

## 8. DB-08: quote là bản tính tiền phía server để khách xác nhận

**Bảng đề xuất tạo mới: `dbo.RetailCheckoutQuote`; liên kết người mua tới `dbo.[User]`
và liên kết đơn từ `dbo.[Order]`. Hai bảng User/Order đã có.**

Ví dụ đơn vị VND: 2 kg cam × 40.000 = 80.000; giao hàng 30.000; voucher giảm 10.000;
khách thấy phải trả 100.000. Server lưu quote Q với các dòng, địa chỉ, phương thức, giảm giá,
phân bổ seller/batch và hạn dùng. Client gửi Q khi Đặt hàng thay vì tự khai tổng tiền 100.000.

Nếu giá cam thành 45.000/kg trước khi đặt, server tính lại ra 110.000. Đề xuất trả thông báo
giá thay đổi và yêu cầu khách xác nhận quote mới; không tự thu 110.000. Tương tự khi thay
địa chỉ, voucher, điểm hoặc phương thức giao. Quote ràng buộc người mua, có hạn dùng và
fingerprint; có quote không đồng nghĩa đã giữ tồn hoặc bảo đảm giá trong toàn bộ hạn quote.

Đề xuất bảng quote là một cách lưu trạng thái, không phải mọi hệ thống bắt buộc có bảng này.
Có thể dùng payload ký hoặc cache dùng chung nếu thiết kế đầy đủ về xác thực, expiry, retry
và tính bền vững liên quan; không chấp nhận JSON/tổng tiền do client tự sửa. Với checkout
có nhiều seller/voucher/điểm, bảng quote giúp nhóm truy vết dễ hơn. Chưa chốt DB-08.

## 9. Điểm: tách số dư, giữ tạm, tiêu dùng và lịch sử

**DB-09: bảng đề xuất mới `dbo.RewardAccount`, `dbo.RewardPointReservation`, liên kết
`dbo.[User]`/`dbo.[Order]` đã có. Bảng ledger còn chờ thiết kế, chưa chốt tên.**

DB-09 đã đồng ý về cấu trúc. Ví dụ Balance=1.000 điểm, ReservedPoints=0.
Đơn A giữ 300 điểm: Balance vẫn 1.000, ReservedPoints=300, khả dụng=700. Đơn B muốn dùng
800 điểm phải bị chặn. Khi A được xác nhận thanh toán, apply đúng một lần: Balance=700,
ReservedPoints=0. Nếu A hết hạn trước khi apply: Balance=1.000, ReservedPoints=0.
Reservation theo OrderId xác định lần giữ thuộc đơn nào; ledger ghi sự kiện earn/redeem/
expire/refund để giải thích số dư. Bảng balance và reservation trong script hiện tại chưa
thay thế một ledger hoàn chỉnh do module loyalty sở hữu.

**Đề xuất để thảo luận, chưa duyệt:** 1 điểm=100 VND, dùng tối đa 20% tiền hàng sau voucher,
không giảm tiền vận chuyển. Ví dụ tiền hàng 500.000, voucher 50.000, cơ sở còn 450.000;
cap 90.000 = 900 điểm. Dùng 300 điểm giảm 30.000; ship 30.000 thì khách trả 450.000.
Các con số 100/20% hiện chỉ là fixture, không suy ra từ việc đồng ý DB-09.

Cần chốt riêng: nguồn/tỷ lệ tích điểm và lúc tích, tỷ lệ đổi, cap, kết hợp voucher,
hạn điểm, điểm bị giữ khi tới ngày hết hạn, hoàn/hủy/hoàn một phần. Điểm earn chỉ nên được
module loyalty ghi khi đạt mốc nghiệp vụ đã chọn, không tự cộng ngay khi tạo PendingPayment.
Ba UC chỉ đọc và giữ điểm theo contract; không triển khai UC tích/hoàn điểm.

## 10. Danh mục xóa: inactive, archive, xóa vật lý

**Bảng chính: `dbo.Category`; kiểm tra sản phẩm tham chiếu ở `dbo.Product` và danh mục
con trong `dbo.Category`. Tất cả đều là bảng có sẵn.**

| Cách xử lý | Ví dụ và hệ quả |
| --- | --- |
| Inactive | Tạm ẩn “Trái cây mùa vụ”; vẫn giữ danh mục, có thể bật lại theo quy tắc cha/con |
| Archive / xóa mềm | Bỏ dùng “Danh mục thử”; giữ ID và references, ẩn khỏi danh sách mặc định, không nhận sản phẩm mới |
| Xóa vật lý | DELETE hàng; chỉ phù hợp nếu hoàn toàn không có references và nhóm cho phép; không cascade xóa sản phẩm/đơn |

Đề xuất cho ba UC: nút Xóa thực hiện archive và phải xác nhận. Chặn khi có product active
hoặc child chưa archive như demo hiện tại. Có sản phẩm inactive vẫn cần giữ references;
admin chuyển danh mục sản phẩm bằng UC khác trước nếu muốn dọn cây. Không tự di chuyển
product/child khi xóa. Demo chưa có chức năng khôi phục archive; đừng mặc định rằng archive
cũng bật lại được như inactive. Việc inactive có làm ẩn mọi sản phẩm con hay không cần
thống nhất contract với catalog; không thay trạng thái product hàng loạt ở UC này.

Ví dụ Product P từng thuộc CategoryId=8 “Rau hữu cơ”. Archive ID=8 giữ được liên kết và audit.
Nếu sau này cần danh mục cùng tên, hai hướng là giữ tên cũ hoặc cho dùng lại bằng unique index
lọc archive. Đề xuất hiện tại giữ tên để giảm nhầm lẫn; người dùng chưa chốt lựa chọn này.
Nếu cần tên danh mục đúng tại thời điểm mua, cần snapshot tên trên đơn riêng; chỉ giữ FK
không ngăn tên lịch sử đổi khi danh mục được đổi tên.

## 11. Role: tên vai trò và quyền thao tác là hai quyết định

**DB-16: role/status tài khoản nằm ở `dbo.[User]`; order type/status nằm ở `dbo.[Order]`.
Bảng role/permission tách riêng chưa được chốt hoặc tạo.**

Đã chốt `RetailCustomer`, không dùng `Consumer` cho dữ liệu/contract mới. Các vai trò nội bộ
tách `SystemAdmin`, `CustomerSupport`,... Danh sách còn lại cần lấy đúng từ thiết kế nhóm;
không tự thêm CatalogAdmin/FinanceAdmin hoặc cấp quyền cho mọi role có chữ Admin.

Đề xuất kiểm tra permission cho đúng thao tác: `users.read`, `users.status.manage`,
`categories.read`, `categories.manage`; checkout yêu cầu RetailCustomer Active và ownership.
Tên permission chỉ là đề xuất contract, chưa phải ma trận quyền đã duyệt.

Ví dụ để review: SystemAdmin được quản lý trạng thái; CustomerSupport được xem/tra cứu và
có thể được phép unlock RetailCustomer nếu nhóm đồng ý, nhưng không tự có quyền suspend
SystemAdmin hoặc sửa danh mục. API phải kiểm tra quyền và phạm vi target; ẩn nút UI chưa đủ.
Vẫn cấm self-action. Nếu SystemAdmin được suspend SystemAdmin khác, cần quy tắc tránh khóa
nốt tài khoản quản trị hoạt động cuối cùng; đây là phần cần chốt, không phải code đã có.

Schema hiện có một User.Role: đủ nếu mỗi user đúng một role. Nếu cần nhiều role/user,
đề xuất Role/UserRole và RolePermission (hoặc contract tương đương của auth); chưa có yêu cầu
thì không tự mở rộng schema theo hướng nhiều role. Không chạy UPDATE đổi mọi Administrator
thành SystemAdmin vì có thể gồm nhân viên hỗ trợ; cần mapping theo từng tài khoản.

**Khoảng cách code hiện tại:** Program.cs, CurrentUserAccess, demo seed và UI vẫn dùng
Consumer/Administrator; hai Admin policy vẫn đang gộp. Cần thay RetailCustomer đồng bộ API,
fixture, filter/UI/test; thay policy quản trị sau khi có ma trận quyền. Header Demo consumer
chỉ là selector fixture cũ, không phải tên role đã chốt. Không coi báo cáo này là code đã sửa.

## 12. Thời hạn: quote, reservation, voucher, batch và subscription khác nhau

**DB-12: bảng có sẵn `dbo.[Order]`, `dbo.OrderItemBatchAllocation`, `dbo.HarvestBatch`,
`dbo.InventoryMovement`, `dbo.VoucherRedemption`, `dbo.Voucher`; bảng điểm mới đề xuất
`dbo.RewardPointReservation`, `dbo.RewardAccount`. Quote mới là `dbo.RetailCheckoutQuote`
(DB-08). Hạn gói nằm ở `dbo.B2BSubscription` đã có, chỉ nêu để phân biệt với hạn giữ hàng.**

| Thời hạn | Ý nghĩa |
| --- | --- |
| Quote | Thời gian được gửi xác nhận một bản tính tiền; vẫn tái kiểm tra giá/tồn |
| Reservation | Thời gian đơn PendingPayment giữ hàng, điểm và voucher |
| Voucher | Chương trình có thời điểm bắt đầu/kết thúc; khác hạn giữ quota cho một đơn |
| Batch | Hạn sử dụng thực phẩm, có thể thêm điều kiện còn đủ thời gian vận chuyển |
| Subscription | Khoảng quyền lợi gói còn hiệu lực; không dùng thay hạn giữ hàng |

Ví dụ giả định 5/15 phút: tạo quote 10:00, hết hiệu lực nhận mới lúc 10:05.
Đặt thành công 10:03 thì reservation tới 10:18, không phải 10:15. Quote hết hạn sau đó
không hủy đơn đã tạo. Retry đúng key vẫn trả đơn cũ. Tới 10:18 chưa thanh toán thì payment/
worker phối hợp chuyển trạng thái và giải phóng đúng một lần. Thời điểm `now >= ExpiresAt`
được coi là hết hạn theo đề xuất; dùng giờ server UTC.

DB-12 đã đồng ý cơ chế; 5/15 phút chưa được chốt thành policy chính thức. Thanh toán sát
hạn hoặc webhook tới muộn phải được module payment/worker đối soát trong transaction;
không vừa trả hàng lại kho vừa giao một đơn đã thu tiền. Không triển khai worker/webhook ở đây.

## 13. Batch: tồn của một lô thu hoạch, không chỉ là mã sản phẩm

**DB-05/DB-11/DB-12: bảng chính `dbo.HarvestBatch`; liên quan `dbo.Product`,
`dbo.CartItem`, `dbo.OrderItem`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement`
(đã có). `dbo.TraceabilityQRCode` đã có, chỉ tham chiếu để giải thích truy xuất lô.**

Một product “Cam sành” có nhiều lô với mã lô, ngày thu hoạch, hạn, tồn và khả năng bán riêng.
FEFO là ưu tiên hạn sử dụng gần nhất; FIFO là ưu tiên nhập trước; hai quy tắc có thể chọn
khác lô. Với thực phẩm có hạn, đề xuất FEFO nếu nhóm chọn hướng tự phân bổ tại DB-05.

Ví dụ lô A available=10 kg, reserved=0. Đơn giữ 4 kg: available=6, reserved=4.
Thanh toán: xác nhận phân bổ, giảm reserved theo contract inventory; không giảm available
thêm 4 lần nữa. Hủy/hết hạn: available trở lại 10, reserved=0 đúng một lần. Nếu lô đã hết
hạn/không bán được lúc release, phải chuyển số lượng vào trạng thái tồn không bán được theo
inventory, không tự đưa hàng hết hạn ra bán. DB-11/12 đã chốt cơ chế an toàn, không chốt
thay cách chọn batch. OrderItemBatchAllocation ghi nguồn lô thực để truy vết/thu hồi hàng.

## 14. Địa chỉ và vùng giao từ repository đã chỉ định

Đã đọc trực tiếp hai file và LICENSE từ nhánh main ngày 18/10/2026:

- [province.json](https://github.com/adminvsrm/vndata_hanhchinh/blob/main/province.json):
  34 phần tử; ví dụ Hồ Chí Minh có `code="12"` trong bộ này.
- [ward.json](https://github.com/adminvsrm/vndata_hanhchinh/blob/main/ward.json):
  3.321 phần tử; ví dụ Thạnh An có `code="268"`, `parent_code="12"`.

Đây là mã của **bộ dữ liệu đã chọn**, không tự đồng nhất với mã của carrier/bộ dữ liệu khác.
Giữ code là string, không dùng tên/slug làm khóa và không bỏ số 0 đầu nếu có.
Contract đề xuất: ProvinceCode, WardCode, AddressDetail; lưu thêm tên snapshot và phiên bản
catalog trên đơn để vẫn hiểu địa chỉ cũ sau cập nhật. Các bảng địa chỉ đọc bằng catalog code;
đơn cũ không bị thay hàng loạt theo tên mới.

Luồng chọn: tỉnh/thành → xã/phường thuộc tỉnh đó → số nhà/đường. Backend kiểm tra
`ward.parent_code == province.code`. Bộ JSON hiện không có tầng district, do đó không bắt
khách nhập quận/huyện mới để lấp field District bắt buộc của demo. Cần đổi DTO/validator/UI/
quote fingerprint và schema snapshot tương ứng; dữ liệu legacy ba cấp phải được giữ và map
có nguồn riêng, không đoán bằng tên.

Địa giới là danh mục địa chỉ; vùng giao là tập con dựa trên policy giao. “Thạnh An thuộc
Hồ Chí Minh” xác nhận cặp địa chỉ, không chứng minh giao Standard/Express được. Đề xuất
ServiceArea/adapter cấu hình theo mã tỉnh/xã và method, kiểm tra cho từng seller. Nếu nghiệp
vụ muốn phục vụ toàn bộ địa chỉ JSON thì cần chốt rõ đó là policy; không suy ra chỉ từ file.

Khi tích hợp: đóng gói một phiên bản dữ liệu xác định, ghi commit/hash, đọc local/cache,
không gọi GitHub cho mỗi checkout; giữ LICENSE đi kèm dữ liệu được đưa vào project và xem
điều kiện phân phối trong [LICENSE](https://github.com/adminvsrm/vndata_hanhchinh/blob/main/LICENSE).
Đợt này mới khảo sát và ghi contract, chưa đóng gói JSON hoặc sửa địa chỉ runtime.

## 15. Phí giao dịch 2% và gói subscription

**Đã chốt:** giao dịch thường 2%, gói subscription theo cấu hình Admin. Đây là phí giao
dịch/commission; phí vận chuyển Standard/Express vẫn là policy khác. Fixture 30.000/60.000
và thêm seller 10.000 trước đây chỉ mô phỏng vận chuyển, không phải mức phí giao dịch.

Ví dụ chưa chốt cơ sở tính/bên chịu phí: nếu phí tính trên 500.000 tiền hàng và seller chịu,
commission thường=10.000; seller còn 490.000 trước các khoản khác. Nếu một gói được Admin
đặt 1% thì cùng cơ sở phí=5.000. Mức 1% chỉ minh họa, không là gói được tạo/được duyệt.
Không tự cộng 2% lên tiền RetailCustomer phải trả khi chưa biết bên chịu phí.

Các điểm phải chốt để có công thức thực thi:

1. Người chịu phí: seller, buyer hay chia; cơ sở là trước/sau giảm, có tính ship/thuế không.
2. Chủ thể có subscription: schema hiện B2BSubscription chỉ gắn WholesaleBuyerProfile,
   chưa có gói seller/RetailCustomer. Không áp nhầm gói của wholesale vào đơn retail.
3. Rate theo gói là phần trăm, tiền cố định, miễn phí hay kết hợp; có quota/giới hạn không.
4. Rate chốt lúc đặt, thanh toán hay settlement; gói đổi/hết hạn giữa hai thời điểm xử lý ra sao.
5. Hoàn/hủy/hoàn một phần phân bổ và hoàn phí thế nào, đơn nhiều seller áp rate nào.

Schema có CommissionPolicy theo CategoryId/OrderType, CommissionRate, EffectiveDate và
PayoutItem lưu snapshot; B2BSubscriptionPackage chưa có commission field. Sau khi rõ chủ
thể/cơ sở mới quyết định nối policy theo gói hoặc thêm field nào. Đề xuất định nghĩa rõ đơn
vị rate: DB lưu 2.00 là 2% thì tính /100, không nhầm với fraction 0.02 trong code.
Giữ policy/version/rate/base/amount snapshot tại mốc đã chốt để đổi gói không sửa phí cũ.
Ba UC chỉ tiêu thụ kết quả policy nếu cần; không viết UC quản lý gói hoặc chi trả.

## 16. Việc cần chốt tiếp theo

- Có bỏ hẳn ảnh danh mục hay giữ tùy chọn; unique có giữ tên archive hay cho dùng lại.
- Role/permission và target của từng loại Admin; không dùng một quyền Admin chung.
- Chọn lô ở giỏ hay tự FEFO; có cho một dòng hàng lấy nhiều lô không.
- Tỷ lệ/cap và vòng đời điểm; số phút quote/reservation.
- JSON dùng làm danh mục toàn quốc; toàn quốc giao được hay cần vùng phục vụ cấu hình riêng.
- Bên chịu phí/cơ sở tính/chủ thể gói và mốc snapshot phí.

Chưa chạy SQL, chưa nối DB, chưa bổ sung UC khác. Script đề xuất cũ vẫn mặc định dừng;
không coi việc đồng ý một số mục là duyệt toàn bộ script hoặc hoàn thành migration.
