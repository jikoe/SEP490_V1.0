# Quyết định database đợt 3

**Bổ sung sau review code:** các quy tắc đã chốt nay đã được đồng bộ trong ba UC; xem
[code/comment hiện tại](three-uc-code-review.md). Các mô tả runtime cũ bên dưới là lịch sử review;
persistence vẫn giả lập, chưa nối SQL.

**Cập nhật thực thi:** phần schema đã chốt đã được áp dụng ngày 18/10/2026 lên
`(localdb)\MSSQLLocalDB` / `AgriSustain` (47 bảng). Xem
[bộ SQL và kiểm tra](../database/README.md). DB-06/09 còn chờ chốt, API vẫn dùng mock.
Các dòng “chưa chạy SQL” phía dưới ghi trạng thái tại thời điểm review, trước yêu cầu áp DB.

Ngày 18/10/2026. Đây là phản hồi mới nhất, thay thế trạng thái tương ứng trong tài liệu
đợt 2. Chỉ cập nhật tài liệu và script đề xuất, chưa đổi code runtime hoặc chạy SQL thật.

## 1. Trạng thái mới nhất

| Mã / nội dung | Bảng liên quan | Quyết định |
| --- | --- | --- |
| DB-01 | `dbo.Category` đã có | Không thêm cột ảnh. Không tạo ImageStorageKey hoặc bảng ảnh danh mục. Các field ngày tạo là quyết định độc lập, chưa tự coi là được duyệt |
| DB-02 | `dbo.Category` đã có | Người dùng không yêu cầu phương án này; bỏ đề xuất NormalizedName và unique index cha/tên khỏi script |
| DB-03 | `dbo.Category` đã có | Người dùng không yêu cầu phương án này; bỏ đề xuất bổ sung CHECK self-parent/khóa cây khỏi kế hoạch DB; giữ FK gốc |
| DB-04 | `dbo.[User]`, `dbo.Category` đã có | Người dùng không yêu cầu phương án này; không thêm Version/rowversion vào hai bảng |
| DB-05 | `dbo.Cart`, `dbo.CartItem`, `dbo.Product`, `dbo.HarvestBatch`, `dbo.OrderItem`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement` đã có | Chọn B: giỏ chọn sản phẩm/số lượng, checkout phân bổ nhiều lô FEFO; không thêm HarvestBatchId vào CartItem |
| DB-06 | `dbo.[Order]` đã có; `dbo.RetailCheckoutQuote` mới được đồng ý ở DB-08 | Người dùng đặt lại vấn đề cần thiết; chưa duyệt request key riêng. Đề xuất một quote chỉ tạo một đơn để đơn giản hóa, còn chờ chốt |
| DB-07 | `dbo.[Order]`, `dbo.SellerOrder`, `dbo.OrderItem` đã có | Giữ đồng ý snapshot checkout |
| DB-08 | Mới: `dbo.RetailCheckoutQuote`; liên kết `dbo.[User]`, `dbo.[Order]` đã có | Đã đồng ý bảng quote phía server |
| DB-09 | `dbo.[User]`, `dbo.[Order]` đã có; `dbo.RewardAccount`, `dbo.RewardPointReservation` mới chỉ là phương án | Đang xem xét lại tính cần thiết; không tự tạo hai bảng mới hoặc bỏ tính năng điểm |
| DB-10 | `dbo.Voucher`, `dbo.VoucherRedemption`, `dbo.[Order]` đã có | Đã xác nhận giữ nguyên quota voucher theo phương án đồng ý trước: reserve/apply/release, tính cả reservation còn hiệu lực và thêm ExpiresAt cho VoucherRedemption |
| DB-11 | `dbo.HarvestBatch`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement` đã có | Giữ đồng ý chống bán vượt tồn; áp dụng cho tổng phân bổ nhiều lô |
| DB-12 | `dbo.[Order]`, `dbo.OrderItemBatchAllocation`, `dbo.HarvestBatch`, `dbo.InventoryMovement`, `dbo.VoucherRedemption`, `dbo.Voucher` đã có | Giữ đồng ý lifecycle giữ/áp dụng/giải phóng; phần điểm theo thiết kế DB-09 sau khi chốt, không bắt buộc bảng reservation điểm riêng |
| DB-13 | `dbo.AuditLog`; target `dbo.[User]`, `dbo.Category`, `dbo.[Order]` đã có | Giữ đồng ý audit target/trace và commit nguyên tử |
| DB-14 | `dbo.[User]` đã có; storage session/token chưa chốt tên/schema | Giữ đồng ý kiểm tra trạng thái và thu hồi phiên |
| DB-15 | Các bảng timestamp liên quan ba UC: `dbo.[User]`, `dbo.Product`, `dbo.HarvestBatch`, `dbo.Cart`, `dbo.CartItem`, `dbo.[Order]`, `dbo.SellerOrder`, `dbo.OrderItemBatchAllocation`, `dbo.InventoryMovement`, `dbo.Voucher`, `dbo.VoucherRedemption`, `dbo.AuditLog`; mới: `dbo.RetailCheckoutQuote` | Giữ đồng ý UTC cho dữ liệu mới; không tự chuyển dữ liệu cũ. Timestamp điểm theo schema DB-09 nếu sau này chốt |
| DB-16 | `dbo.[User]`, `dbo.[Order]` đã có; role/permission tách riêng chưa chốt | RetailCustomer; tách SystemAdmin, CustomerSupport,...; ma trận quyền vẫn chưa chốt |
| Xóa danh mục (đã xác nhận riêng, không phải DB-10) | Chính: `dbo.Category`; kiểm tra `dbo.Product`; các FK khác: `dbo.Category` con, `dbo.RegionalPriceBenchmark`, `dbo.CommissionPolicy` | Đã xác nhận: chưa có product thì cho xóa nếu không vướng FK; có product thì không xóa, chỉ ẩn/khóa |

## 2. DB-05: phương án B đã chọn

Giỏ lưu ProductId + Quantity, không buộc khách chọn HarvestBatchId. Checkout tìm lô
cùng sản phẩm, đang đủ điều kiện bán, còn khả dụng, rồi phân bổ FEFO theo hạn sử dụng gần
nhất. Khi cùng hạn dùng thứ tự ổn định theo HarvestBatchId. Lô không có hạn cần policy riêng;
đề xuất xếp sau lô có hạn, chưa xem là quy tắc người dùng đã duyệt.

Ví dụ mua 5 kg cải: lô A còn 3 kg, hết hạn sớm; lô B còn 20 kg, hết hạn muộn.
Quote ghi A=3 kg + B=2 kg. Khi đặt đơn, kiểm tra lại chính phân bổ đã hiển thị, giữ tổng
số lượng từng batch trong một transaction. Một dòng sản phẩm có thể tham chiếu hai hàng
`dbo.OrderItemBatchAllocation` tới hai `dbo.HarvestBatch`; schema gốc đã hỗ trợ quan hệ này.

Nếu A không còn đủ hàng lúc xác nhận, không tự đổi sang lô khác rồi đặt đơn theo quote cũ.
Tính lại quote/phân bổ và yêu cầu xác nhận lại theo cơ chế DB-08. Quote chưa giữ tồn.
Không cộng/trừ tồn hai lần nếu một lô được nhiều dòng giỏ dùng; tổng hợp theo BatchId trước
khi giữ. Không thêm FK CartItem-batch theo phương án A đã bỏ.

Code demo hiện vẫn có một BatchId trên CartLine và một batch trên dòng snapshot; cần đổi
read model, repository đọc danh sách lô, quote allocation, service đặt và test khi triển khai.
Việc bỏ migration CartItem-batch không đồng nghĩa runtime đã chuyển sang FEFO.

## 3. DB-06: hai đơn không đồng nghĩa tự thu tiền hai lần

Người dùng đúng: tạo hai PendingPayment không tự động thanh toán cả hai. Chống trùng đơn
không được mô tả như cơ chế chống tự trừ tiền hai lần; payment có hợp đồng riêng.

Điểm cần cân nhắc là **giữ tài nguyên trước thanh toán**. Nếu hai đơn đều tạo thành công,
mỗi đơn có thể giữ hàng/voucher/điểm dù khách chỉ định mua một lần. Ví dụ một lô có 10 kg,
hai đơn cùng giữ 4 kg làm chỉ còn 2 kg bán được tới khi đơn thừa hết hạn. Một đơn cố ý mua
thêm là hợp lệ; vấn đề cần phân biệt là retry do timeout với một ý định đặt hàng mới.

Đề xuất gọn vì DB-08 đã đồng ý: một quote chỉ gắn tối đa một Order. Retry quote cũ trả đơn
cũ; muốn đặt đơn tiếp thì tạo quote mới. Có thể dùng `dbo.[Order].RetailQuoteId` với unique
filtered index và giao dịch để xử lý hai request cạnh tranh; không cần RequestKey/RequestHash
riêng nếu contract một-quote-một-đơn được nhóm đồng ý. Phải kiểm tra buyer của quote/đơn,
tìm đơn đã tạo trước khi kiểm tra quote hết hạn, không dùng cờ consumed ngoài transaction.

Đây là đề xuất thay thế đang review, chưa tự thêm unique index quote vào script. Script
không còn tự thêm RetailRequestKey/RetailRequestHash. Việc gắn FK RetailQuoteId ở DB-08
tự nó chưa bảo đảm một quote chỉ tạo một đơn. Giữ chống trùng ở UI hoặc chờ đơn thừa hết
hạn là lựa chọn đơn giản hơn nhưng có giới hạn như ví dụ trên; không gọi đó là chống trùng
bền vững. Không bỏ cơ chế chống bán vượt tồn của DB-11.

## 4. DB-09: cần cơ chế điểm nếu dùng điểm, không bắt buộc hai bảng mới

Có hai câu hỏi khác nhau:

1. Checkout còn cho dùng điểm không? Nếu còn, phải đọc được số dư từ nguồn có thẩm quyền
   và ngăn dùng vượt số dư khi nhiều đơn chờ thanh toán. Nếu bỏ tính năng điểm cần quyết
   định riêng; chưa tự bỏ chỉ vì người dùng hỏi về bảng.
2. Có cần tạo riêng `dbo.RewardAccount` và `dbo.RewardPointReservation` không? Không bắt
   buộc. Nếu module loyalty đã có storage thì dùng contract của module đó. Nếu chưa có,
   có thể chọn thiết kế đơn giản hơn sau khi thống nhất với nhóm.

Ví dụ có 100 điểm. Đơn A giữ 80 điểm thì chỉ còn 20 điểm khả dụng cho đơn B. Đọc 100 điểm
cho cả A và B mà không giữ/cập nhật có điều kiện có thể cho dùng tổng cộng 160 điểm.
Đợi thanh toán rồi mới kiểm tra điểm sẽ phải giải quyết đơn đã nhận mức giảm nhưng không
còn đủ điểm; muốn chọn hướng đó phải có quy tắc xác nhận lại/hủy/đối soát cụ thể.

Phương án tối giản để review: nguồn số dư và số điểm đang giữ có thể là hai cột trên
`dbo.[User]` thay vì bảng RewardAccount riêng; số điểm theo đơn dùng snapshot trên
`dbo.[Order]` và trạng thái/hạn đơn, hoặc contract reservation của loyalty. Điểm khả dụng
= số dư - đang giữ. Apply/release phải idempotent theo đơn và dùng cùng transaction;
snapshot giảm tiền một mình không thay được nguồn số dư hoặc sự kiện đã apply/release.
Phải chốt các marker/trạng thái cần thiết trước khi viết, không khẳng định chỉ hai cột là
đủ cho mọi tình huống hoàn/hủy/hoàn một phần. Ledger đầy đủ để module loyalty thiết kế,
không bắt viết toàn bộ trong ba UC này.

Đề xuất hiện tại: chưa tạo hai bảng điểm chỉ để phục vụ mock. Demo tiếp tục ghi rõ nguồn
điểm là giả lập cho tới khi chốt có dùng điểm và storage/contract tối thiểu. Script không
tự tạo RewardAccount/RewardPointReservation trong lúc quyết định này đang xem xét lại.

## 5. Xóa danh mục: quy tắc mới thay phương án archive mặc định

**Bảng chính `dbo.Category`; bảng kiểm tra product `dbo.Product`.**

- Có product gắn với danh mục: chặn DELETE, kể cả product inactive/chưa publish; chỉ cho
  ẩn/khóa danh mục. Không chỉ kiểm tra active products như demo cũ.
- Chưa có product: cho xóa vật lý nếu không còn liên kết FK khác. Không tự cascade hoặc
  xóa dữ liệu nghiệp vụ khác để làm cho danh mục xóa được.
- Có thể dùng `Category.IsActive=false` sẵn có cho một trạng thái ngừng sử dụng. Nếu “ẩn”
  và “khóa” phải có hai tác dụng khác nhau thì cần mô tả riêng trước khi thêm trạng thái.
- Không cần mặc định thêm IsArchived/ArchivedAt cho quy tắc mới. Audit vẫn giữ target ID
  và thông tin trước xóa trong `dbo.AuditLog`, ghi cùng transaction DELETE.

Đối chiếu SQL gốc thấy FK tới Category từ `dbo.Product`, `dbo.Category` (con),
`dbo.RegionalPriceBenchmark`, `dbo.CommissionPolicy`. Vì thế “chưa có product” là điều
kiện cần; nếu còn danh mục con hoặc policy/benchmark tham chiếu thì thông báo cụ thể
và chặn xóa, không tự làm hỏng FK. Kiểm tra và xóa phải cùng transaction; FK là bảo vệ
cuối cùng nếu product mới được gán đồng thời.

Nếu yêu cầu “đã có product” nghĩa là *đã từng có trong lịch sử* ngay cả sau khi product
được chuyển sang category khác, cần thêm nguồn lịch sử để kiểm tra; kiểm tra FK hiện tại
chỉ trả lời “đang có product tham chiếu”. Chưa tự thêm cột lịch sử hoặc thay nghiệp vụ đó.

Người dùng đã xác nhận: ý này thuộc quản lý danh mục; DB-10 quota voucher giữ nguyên
phương án đã đồng ý trước. Không còn chờ làm rõ lệch mã và không đổi số mã DB-10.

## 6. Trạng thái thực thi

- Đã sửa báo cáo, trạng thái quyết định và script đề xuất để bỏ các thay đổi không chọn.
- Script chỉ còn phần schema đã đồng ý liên quan: quote, snapshot, tồn kho, voucher và
  audit. Mặc định vẫn dừng, chưa chạy lên database thật; chưa là migration production.
- Runtime vẫn là demo cũ: còn ảnh, unique/cycle/version, chọn batch và archive. Chưa tuyên
  bố các yêu cầu mới đã được thực thi hoặc test trên ứng dụng.
- Role, địa chỉ JSON, phone, commission 2%/gói giữ các quyết định trước; không tự thêm UC.
