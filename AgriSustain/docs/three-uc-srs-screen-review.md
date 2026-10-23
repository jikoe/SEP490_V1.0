# Đối chiếu ba UC với màn hình Report 3

Đọc lại [Report 3](https://docs.google.com/document/d/1nPGNTGhL7M8G2MKMOsU-jz2NDxBCFiZMBoz82Z7ScMU/edit?tab=t.0)
ngày 19/10/2026, phần User List/User Details, Product Category List/Details và Retail
Checkout, kèm UC-ADM-03/ADM-05/RC-02. Chỉ triển khai ba UC được giao; trang chi tiết là
một phần của UC hiện tại. Nội dung tài liệu là cơ sở đối chiếu; quyết định sửa yêu cầu
của người dùng trong cuộc trao đổi được ưu tiên khi khác tài liệu.

## Các điểm đã sửa

| UC / màn hình trong SRS | Giao diện và đường đi hiện tại |
| --- | --- |
| ADM-03 / User List | Sidebar quản trị; từ khóa tối đa 100, role/status và nút Tìm kiếm; bảng name/organization, email, phone, role, status, KYC, created date; 10 bản ghi/trang, số trang và range; Xem chi tiết và action theo trạng thái |
| ADM-03 / User Details | Trang riêng `#/administration/users/{id}`, đọc lại GET theo ID; avatar initials, tên đơn vị/người dùng, mã, role, joined date, email/phone/status/KYC; Back và action; confirmation + reason trước thay đổi |
| ADM-05 / Product Category List | Từ khóa, status, parent/root và Tìm kiếm; mã/tên/cha/priority/status/product count/actions; phân trang; thêm/sửa mở trang riêng; ẩn/kích hoạt/xóa có xác nhận |
| ADM-05 / Product Category Details | `#/administration/categories/new` hoặc `/{id}`; name 2–100, parent, description 500, priority không âm, trạng thái; Save/Cancel; không upload ảnh |
| RC-02 / Retail Checkout | `#/checkout`; bảng sản phẩm ở đầu gồm thumbnail/tên/farmer/batch/quantity/price/subtotal; địa chỉ lưu hoặc nhập mới, tỉnh–xã liên kết, textarea detail 255; delivery radio + phí, payment radio, note 300; summary bên phải với voucher/points/discount/total; Tính lại, Đặt hàng, Trở về giỏ |

Tìm kiếm/lọc/phân trang chạy trên SQL qua EF, không lọc một mảng demo tải sẵn.
Direct category detail dùng GET `/api/categories/{id}`, không tìm trong 100 dòng đầu.
Parent dropdown tải hết các trang khi cần. Service vẫn kiểm tra parent/cycle/reference
và quyền trên server; ẩn nút không thay thế kiểm tra nghiệp vụ.

Checkout giữ bảng sản phẩm khi draft địa chỉ chưa hoàn chỉnh; chỉnh draft hủy quote cũ
và cần server tính lại. Frontend không tự quyết định giá/stock/discount. Số lượng chỉ đọc
theo bảng mô tả field của SRS; chỉnh giỏ thuộc UC-RC-01. Ảnh đọc từ `dbo.ProductImage`;
seed chưa có ảnh thì hiển thị “Chưa có ảnh”, không gán ảnh giả hoặc thêm cột Category.

## Các khác biệt có chủ đích theo quyết định đã chốt

| Nội dung SRS cũ/minh họa | Cách xử lý đã thống nhất |
| --- | --- |
| Category image, unique sibling name, archive | Không thêm ảnh/unique/archive. Danh mục không có mọi product reference và không còn FK mới xóa hẳn; có product kể cả unpublished chỉ ẩn/khóa |
| Consumer, Administrator tổng quát | Actor là RetailCustomer và staff roles tách. Chỉ SystemAdmin có policy Administration hiện tại. Tên feature **Consumer Ordering** giữ theo Project Tracking, không phải role Consumer |
| District trong địa chỉ | Dùng tỉnh–xã theo JSON địa giới đã thống nhất; không đưa District trở lại UI |
| Created Date của Category | Schema Category không có cột đó; không bịa ngày tạo hoặc tự thêm schema ngoài quyết định |
| Hình minh họa có chỉnh qty/QR | Field qty chỉ đọc; QR/payment là UC tiếp theo, giữ handoff chờ module thay vì triển khai UC khác |

## Các điểm còn chờ nhóm tích hợp

- Auth/session: selector local chỉ phục vụ review, chưa phải đăng nhập thật. SQL User
  Active/lock được kiểm tra; durable JWT revocation chưa có adapter.
- Điểm, policy phạm vi/phí giao, request key vẫn là mock có comment nơi thay thế.
- Delivery ETA: chưa có contract trả thời gian; UI ghi chờ vận chuyển xác nhận,
  không cam kết số ngày giả. Chọn method mới cần tính lại phí.
- Trở về giỏ hiện thông báo handoff chờ UC-RC-01. Đặt hàng tạo PendingPayment, hiển thị
  mã/tổng/hạn giữ; chưa tự mở QR hoặc khẳng định thanh toán thành công.

## Kiểm tra trong lần sửa này

Build backend/frontend thành công; kiểm tra comment không thiếu ở các declaration/hàm
được audit. Readiness trả SQL Server EF provider, database AgriSustain, 47 mappings;
GET trả 10 user, 6 category, 2 selected product. Browser mở đủ list/detail/category form/
checkout và tự preview quote qua EF. Không bấm lưu/xóa/suspend/xác nhận tạo đơn trên
database đang dùng trong lượt kiểm tra này.

Ảnh local: `../../outputs/three-uc-srs-review/` gồm users, user-details, categories,
category-details, checkout. Đây là bằng chứng xem nhanh, không phải bộ System Test đã duyệt.
**Chưa chạy test chi tiết** cho bản EF/UI mới theo yêu cầu người dùng. Các report test
trước đó thuộc revision cũ; sau duyệt mới chạy lại và cập nhật actual result/evidence.
