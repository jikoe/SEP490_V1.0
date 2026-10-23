# Phạm vi triển khai ba UC được giao

**Trạng thái code mới nhất:** [review code và comment](three-uc-code-review.md). Nội dung đối chiếu
SRS/SDS bên dưới là bản nguồn trước các quyết định DB; không dùng các yêu cầu ảnh/archive/
Version hoặc chọn batch trong giỏ để ghi đè quyết định người dùng. Adapter SQL vẫn chưa có.

**Quyết định mới nhất:** [đợt 3](database-decisions-round-3.md) thay các mô tả cũ về ảnh,
DB-02/03/04, batch chọn ở giỏ và archive. DB-05 chọn phân bổ FEFO; DB-08 đã đồng ý;
DB-06/09 đang xem xét. Chưa cập nhật runtime; không coi mô tả demo dưới đây là code đã
đáp ứng các thay đổi này.

Ngày đối chiếu: 18/10/2026.

**Phản hồi mới của người dùng:** dùng RetailCustomer thay Consumer, tách các role Admin;
địa chỉ mới theo JSON tỉnh/xã, commission thường 2% và gói do Admin cấu hình. Đây là yêu
cầu cập nhật, chưa áp vào code demo. Các đoạn mô tả phía dưới phản ánh phiên bản triển khai
trước phản hồi. Xem [quyết định đợt 2](database-decisions-round-2.md) để có trạng thái chính xác.

## 1. Nguồn và phạm vi

- [SRS trực tuyến](https://docs.google.com/document/d/1nPGNTGhL7M8G2MKMOsU-jz2NDxBCFiZMBoz82Z7ScMU/edit?tab=t.0):
  II.4.2 UC-ADM-03, II.4.3 UC-ADM-05, II.6.2 UC-RC-02;
  các màn hình User List/User Details, Product Category List/Details, Retail Checkout;
  GBR-01–10, GBR-13–17 và Other Requirements liên quan.
- [Thiết kế trực tuyến](https://docs.google.com/document/d/1gRUQwJAMfWAywcQd9lA82P-DaHC4E1s9/edit):
  II.4.2, II.4.3, II.6.2 và III.1 Security Design.
- `AgriSustain.sql` do người dùng cung cấp: 46 bảng SQL Server.
- `coding-standards.md`, `testing-standards.md` và solution hiện tại.

Chỉ triển khai UC-ADM-03, UC-ADM-05 và UC-RC-02. Các phụ thuộc cần dùng lại hoặc thống nhất
hợp đồng tích hợp trước. Không tự triển khai nghiệp vụ của UC khác để hoàn thiện ba UC này.

## 2. Công việc nằm trong phạm vi

### UC-ADM-03 — Manage User Account Status

- Danh sách, tìm kiếm, lọc role/status, phân trang mặc định 10; xem chi tiết được phép.
- Suspend khi tài khoản đang Active, activate khi Suspended, unlock khi đang khóa tạm.
- Xác nhận thao tác; bắt buộc lý do cho thay đổi trạng thái.
- Kiểm tra Administrator đang Active và không thao tác chính tài khoản của mình theo GBR-06.
- Unlock xóa khóa tạm và reset bộ đếm đăng nhập thất bại.
- Thay đổi và AuditLog phải nhất quán, có actor, target, before/after, thời điểm và lý do.
- Hủy xác nhận không cập nhật dữ liệu và không ghi audit thay đổi.
- Tích hợp thu hồi phiên/kiểm tra trạng thái hiện tại qua module xác thực sẵn có.

Không triển khai đăng ký, đăng nhập, refresh token, reset password, KYC review hoặc quản lý role.

### UC-ADM-05 — Manage Product Categories

- Danh sách/tìm kiếm/lọc theo parent và status, phân trang mặc định 10; chi tiết/create/edit.
- Tên 2–100 ký tự; duy nhất không phân biệt hoa thường trong cùng parent.
- Parent tùy chọn, chọn từ danh mục Active; ngăn self-parent và chu kỳ khi cập nhật cây.
- Description tối đa 500 ký tự; priority nguyên không âm, mặc định 0.
- Ảnh tùy chọn JPG/JPEG/PNG, tối đa 2 MB; kiểm tra nội dung và dùng storage abstraction.
- Active/Inactive, xác nhận thao tác có ảnh hưởng; audit thay đổi.
- Kiểm tra active products theo GBR-10 trước khi xóa; bảo toàn references/historical display
  theo mô tả archive trong SRS và chính sách lưu trữ được thống nhất.

Không triển khai tạo/sửa/duyệt Product, chuyển danh mục sản phẩm hàng loạt hoặc marketplace.
Việc đọc product references để bảo vệ danh mục là một phần của UC-ADM-05.

### UC-RC-02 — Checkout and Place Retail Order

- Đọc các cart item đã chọn thuộc Consumer đang Active; hiển thị sản phẩm, batch, số lượng,
  giá hiện tại, địa chỉ, lựa chọn giao hàng, voucher và điểm có thể dùng.
- Địa chỉ đã lưu hoặc nhập mới; recipient 2–100 ký tự; điện thoại theo cấu hình Việt Nam;
  province/district/ward bắt buộc; address detail tối đa 255 ký tự.
- Delivery method: Standard Delivery hoặc Fresh Produce Express Delivery theo SRS.
- Payment method: VietQR hoặc PayOS; notes tối đa 300 ký tự.
- Tính giá trên server theo dữ liệu hiện tại và policy đã thống nhất. Tái kiểm tra stock,
  giá, address, voucher và điểm khi xác nhận Place Order.
- Voucher không hợp lệ: MSG116 và summary bỏ giảm giá; địa chỉ không phục vụ: MSG117;
  stock không đủ: MSG88; giá đổi: MSG118 và yêu cầu người dùng xác nhận summary mới.
- Tạo Order/SellerOrder/OrderItem và allocation/reservation cần thiết; trạng thái PendingPayment;
  chỉ xóa các cart item đã đặt khi transaction thành công.
- Request trùng theo consumer và request key trả lại order tương ứng, không giữ hàng lần hai;
  kiểm tra bằng cơ chế bền vững, không chỉ kiểm tra in-memory.
- Khi thất bại, không để order/reservation/cart removal dở dang.
- Trả order code và điểm chuyển tiếp để module thanh toán tiếp nhận.

Không triển khai UC-RC-01 add/update/remove cart; UC-RC-03 tạo QR, PayOS hoặc webhook;
UC-RC-04 theo dõi đơn; quản lý voucher/điểm hoặc worker hủy đơn hết hạn.
Phần tiêu thụ voucher/điểm và thời hạn giữ hàng vẫn cần hợp đồng phối hợp với các module đó.

## 3. Code/phần tích hợp cần dùng lại

| Phụ thuộc | Ba UC sử dụng thế nào | Thông tin/code cần nhận |
| --- | --- | --- |
| UC-AUTH-04 và cơ chế phiên | Xác định actor, role, account Active; chặn sau suspension | Claims/role mapping, current-user abstraction, JWT middleware, refresh/session revocation, cơ chế kiểm tra trạng thái hiện tại |
| User và profile | User list/details, thông tin farm/company/KYC được phép; xác thực actor | Entity mapping, DbContext, repository và policy truy cập nếu đã có |
| UC-RC-01 / UC-CON-01 trong một số phần SDS | Selected cart, batch, quantity; xóa ordered items khi commit | Entity Cart/CartItem, selected-batch mapping, repository/service và transaction boundary |
| Product/HarvestBatch | Tái kiểm tra bán được, giá/tiers và giữ stock an toàn | Entity mapping, enum/status, quy tắc giá, allocation và inventory reservation hiện có |
| Voucher | Kiểm tra điều kiện và giữ/lưu redemption | Contract, discount types, quota, scope seller/platform, thời điểm reserve/apply/release |
| RewardAccount | Đọc số điểm và redeem/reserve trong checkout | Schema/entity, tỷ lệ đổi, rounding/cap, thứ tự với voucher, giữ/hoàn điểm và chống dùng vượt số dư |
| Địa chỉ và delivery policy | Kiểm tra phục vụ, tính phí/ETA theo method | Danh mục địa giới, vùng phục vụ, fee/ETA policy và cách xử lý địa chỉ nhập mới |
| UC-RC-03 / UC-CON-03 trong một số phần SRS | Chuyển tiếp sau khi tạo order | Route/contract nhận OrderId/OrderCode; không cần triển khai gateway trong phạm vi này |
| Audit/storage/UnitOfWork dùng chung | Lưu audit, ảnh danh mục và commit nhất quán | Contract và implementation sẵn có nếu đã được phát triển |

Các phần này chưa có implementation trong checkout hiện tại: chỉ thấy bốn project backend
với marker/DI trống; frontend là bộ khung. Cần biết nhánh/thư mục chứa code nếu thành viên
khác đã thực hiện. Không nên tạo bản sao model/service trước khi kiểm tra code đó.

## 4. Chênh lệch giữa SRS/SDS và SQL cần giải quyết

| Nội dung | SQL hiện tại | Điểm cần thống nhất |
| --- | --- | --- |
| Category image và created date | Category chưa có cột tương ứng | Bổ sung field/mapping tối thiểu hoặc xác định nơi lưu hiện có |
| Category archive | Chỉ có IsActive | Phân biệt inactive/archived và delete khi có references; tránh mất lịch sử |
| Sibling name uniqueness | Chưa có unique constraint phù hợp | Chuẩn hóa/collation và unique index bao gồm root category; chống hai request đồng thời |
| Batch được chọn trong cart | CartItem có ProductId, không có HarvestBatchId | Dùng batch đã chọn như SRS/SDS, hay allocation ở checkout theo policy đã thống nhất |
| Order request key | Chưa có cột/bảng idempotency | Cơ chế lưu bền vững và unique theo buyer/request key; không dùng OrderCode thay request key |
| Checkout snapshots | Có tổng tiền, địa chỉ và một số snapshot; chưa có delivery/payment method, notes và tách point discount | Mapping/bổ sung field cần thiết để lưu đúng summary được xác nhận |
| RewardAccount | Chưa có bảng dữ liệu điểm | Nhận schema/module hiện có hoặc chốt hợp đồng trước khi code phần redeem |
| Token/session revocation | User có Status, FailedLoginCount, LockedUntil; chưa có bảng refresh/session | Dùng module auth hiện có; không giả vờ đã thu hồi phiên chỉ bằng sửa User.Status |
| Audit target | AuditLog không có TargetId riêng | Chốt biểu diễn target trong record/snapshot hoặc mapping sẵn có, không tự mở rộng schema toàn hệ thống |
| Tên role và trạng thái | Các cột VARCHAR không có enum/check constraint | Chốt giá trị thực giữa Administrator/System Administrator, Consumer/RetailCustomer và status/code trong nguồn |

SQL **đã có** Cart, CartItem, Voucher và VoucherRedemption. Thiếu implementation của các
module không có nghĩa là thiếu các bảng này.

Phải giải quyết việc lưu dữ liệu của chính ba UC; không thêm lại toàn bộ schema hoặc chạy
script CREATE DATABASE/CREATE TABLE lên môi trường đang có. Nếu cần đổi schema, chuẩn bị
migration/script nhỏ, có giải thích và chỉ áp dụng trong môi trường được xác định.

## 5. Trình tự triển khai sau khi đủ thông tin

1. Đọc code phụ thuộc hoặc thống nhất hợp đồng tích hợp và các schema delta phía trên.
2. Chốt route/DTO/enum, boundary với UC trước/sau và các policy còn thiếu.
3. Triển khai Domain/Application/Infrastructure/API theo coding standards, cùng đúng ba màn hình.
4. Viết test cho self-action, auth/ownership, audit atomicity, category uniqueness/cycle/delete,
   checkout stale price, insufficient stock, duplicate/concurrent request và rollback.
5. Build/test và báo rõ phần được xác minh, phần cần module/hạ tầng để chạy end-to-end.

Người dùng sau đó cho phép giả lập phụ thuộc chưa có, yêu cầu comment chi tiết và giải pháp
database. Ba UC đã có code và demo adapters; xem
[hướng dẫn chạy/tích hợp](three-uc-demo-and-integration.md) và
[database report](database-issues-and-solutions.md). Chưa thực thi thao tác trên database thật.
