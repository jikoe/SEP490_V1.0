# Báo cáo đánh giá độ phức tạp Use Case – AgriSustain

24/10/2026 · Hoàng Anh Minh

## 1. Mục đích và phạm vi

Trong 10 UC được đánh giá lại, 7 UC giữ nguyên mức. Hai UC cần nâng từ Simple lên Medium là UC-ADM-03 và UC-ADM-05. UC-ADM-01 chỉ giữ được mức Complex nếu bổ sung field vào bảng mô tả màn hình.

Báo cáo đối chiếu mức Level đã gán trong bảng Screen/Function Inventory với đặc tả của Report 3 – Software Requirement Specification, bản PDF ngày 24/10/2026. Căn cứ gồm đặc tả UC ở mục II và các bảng Field Description ở mục III. 10 UC được đánh giá: UC-ADM-03, UC-ADM-05, UC-RC-02, UC-AUTH-08, UC-AUTH-07, UC-CS-01, UC-FIN-03, UC-PS-01, UC-ADM-01 và UC-ADM-09.

## 2. Tiêu chí đánh giá

Mỗi UC được xếp theo hai tiêu chí: số field và số transaction. Mức cuối cùng là mức cao hơn giữa hai tiêu chí.

| Mức | Số field | Số transaction |
| --- | --- | --- |
| Simple | ≤ 7 | ≤ 3 |
| Medium | 8 – 15 | 4 – 7 |
| Complex | > 15 | > 7 |

- **Field:** ô nhập, bộ lọc, cột hiển thị và nút thao tác trên màn hình lớn nhất của UC, đếm theo bảng Field Description ở mục III.
- **Transaction:** mỗi thao tác giao diện dẫn tới đọc/ghi cơ sở dữ liệu hoặc gọi dịch vụ ngoài, lấy từ Normal Flow, Alternative Flows và Postconditions ở mục II.
- Ghi audit log cùng giao dịch với thao tác chính không tính thành transaction riêng.

## 3. Bảng tổng hợp

Hai UC bị xếp thấp hơn thực tế, cả hai đều vượt ngưỡng Simple ở cả hai tiêu chí.

| UC | Tên | Mức hiện tại | Field | Transaction | Mức đề xuất | Kết luận |
| --- | --- | --- | --- | --- | --- | --- |
| UC-ADM-03 | Manage User Account Status | Simple | 14 | 6 | **Medium** | Đổi |
| UC-ADM-05 | Manage Product Categories | Simple | 13 | 6 | **Medium** | Đổi |
| UC-ADM-01 | View Platform Dashboard and Pending Work | Complex | 8 (17 theo postcondition) | 6 | Complex | Có điều kiện |
| UC-FIN-03 | Process Refund | Medium | Không có đặc tả | ~6 (ước tính) | Medium | Chưa kiểm chứng |
| UC-RC-02 | Checkout and Place Retail Order | Complex | 20 | 10 | Complex | Giữ |
| UC-CS-01 | Resolve Complaint | Complex | 10 | 9 | Complex | Giữ |
| UC-AUTH-07 | Reset Password | Medium | 13 | 4 | Medium | Giữ |
| UC-PS-01 | Manage Promotions and Vouchers | Medium | 13 | 5 | Medium | Giữ |
| UC-ADM-09 | Manage Subscription Tiers | Medium | 12 | 6 | Medium | Giữ |
| UC-AUTH-08 | Verify Email OTP | Simple | 3 | 3 | Simple | Giữ |

## 4. Phân tích từng UC

Mỗi UC ghi căn cứ trong Report 3, danh sách field và transaction đã đếm, rồi nhận xét.

### 4.1 UC-ADM-03 – Manage User Account Status (Simple → Medium)

- **Căn cứ:** II.4.2; III.4.1.1 User List; III.4.1.2 User Details.
- **Field (14, User List):** Search Keyword, Role Filter, Account Status Filter, User/Entity Name, Email, Phone Number, Role, Account Status, KYC Status, Created Date, View Details, Suspend/Activate, Unlock Account, Pagination. User Details có thêm 12 field.
- **Transaction (6):** tìm kiếm/lọc, phân trang, xem chi tiết, đình chỉ, kích hoạt lại, mở khóa (xóa LockedUntil và đặt lại FailedLoginCount).
- **Nhận xét:** vượt ngưỡng Simple ở cả hai tiêu chí. UC còn có ràng buộc chặn tự đình chỉ, bắt buộc lý do và ghi audit cùng giao dịch.

### 4.2 UC-ADM-05 – Manage Product Categories (Simple → Medium)

- **Căn cứ:** II.4.3; III.4.2.1 Product Category List; III.4.2.2 Product Category Details; GBR-10.
- **Field (13, Category List):** Search Keyword, Status Filter, Parent Category Filter, Category ID, Category Name, Parent Category, Priority, Status, Product Count, New Category, Edit, Activate/Deactivate, Pagination. Category Details có thêm 7 field.
- **Transaction (6):** tìm kiếm/lọc, tạo, cập nhật, bật/tắt, xóa hoặc lưu trữ theo GBR-10, kiểm tra trùng tên trong cùng danh mục cha.
- **Nhận xét:** vượt ngưỡng Simple ở cả hai tiêu chí. Quy tắc xóa/lưu trữ và chống vòng lặp danh mục cha làm UC phức tạp hơn một màn CRUD đơn giản.

### 4.3 UC-ADM-01 – View Platform Dashboard and Pending Work (Complex, có điều kiện)

- **Căn cứ:** II.4.4; III.4.3.1 Platform Dashboard.
- **Field:** bảng III.4.3.1 chỉ có 8 mục (Date Range, Refresh, 4 Summary Cards, Pending Work, View Details). Postcondition của II.4.4 nêu 17 field: Date Range, Refresh, GMV, Total Orders, tỷ lệ giao thành công, tỷ lệ hủy, User Growth theo Consumer/Farmer/B2B Buyer, Total Pending Work và 7 cột Pending Work List.
- **Transaction (6):** tải tóm tắt nền tảng, tải danh sách việc chờ, lọc theo khoảng ngày, làm mới thủ công, tự làm mới mỗi 60 giây, điều hướng tới UC-ADM-02/UC-ADM-04/UC-CS-01.
- **Nhận xét:** nếu chỉ đếm theo bảng field hiện tại, UC chỉ đạt Medium. Mức Complex đứng được khi bảng III.4.3.1 liệt kê đủ 17 field.

### 4.4 UC-FIN-03 – Process Refund (Medium, chưa kiểm chứng)

- **Căn cứ:** chỉ có mô tả trong UC catalog (trang 52–53) và postcondition của UC-CS-01.
- **Field:** không đếm được vì Report 3 không có đặc tả UC và không có màn hình ở mục III.
- **Transaction (~6, ước tính từ catalog):** xem danh sách yêu cầu hoàn tiền, xem chi tiết, ghi loại hoàn tiền, cập nhật PaymentTransaction sang Refunded, thông báo khách hàng, xử lý chuyển tiền thất bại.
- **Nhận xét:** Medium là hợp lý theo ước tính, nhưng chưa có tài liệu để kiểm chứng.

### 4.5 UC-RC-02 – Checkout and Place Retail Order (Complex, giữ)

- **Căn cứ:** II.6.2; III.6.1.1 Retail Checkout Screen; MSG88, MSG116–MSG119.
- **Field (20):** Order Item List, Saved Address Selector, Recipient Name, Recipient Phone Number, Province/City, District, Ward, Address Detail, Delivery Method, Voucher Code, Reward Points Redemption, Payment Method, Order Notes, Subtotal, Delivery Fee, Voucher Discount, Reward Points Discount, Total Amount, Return to Cart, Place Order.
- **Transaction (10):** tải dữ liệu checkout; kiểm tra vùng phục vụ và phí giao; kiểm tra voucher; kiểm tra số dư điểm; kiểm tra tồn kho và giá hiện hành; tạo đơn PendingPayment và mã đơn; giữ tồn kho theo lô; giữ điểm và voucher; xóa mục giỏ đã đặt; chặn đặt trùng (MSG119).
- **Nhận xét:** vượt ngưỡng Complex ở cả hai tiêu chí. Đây là UC phức tạp nhất trong nhóm.

### 4.6 UC-CS-01 – Resolve Complaint (Complex, giữ)

- **Căn cứ:** II.14.1; III.16.1.1 Complaint Resolution Screen.
- **Field (10):** Status, Type, Search, Complaint List, View, Complaint Details and Resolution, Resolution, Reason, Request Information, Confirm Resolution.
- **Transaction (9):** tìm/lọc khiếu nại; xem chi tiết, bằng chứng và đơn liên quan; yêu cầu bổ sung thông tin; Full Refund tạo Refund Request; Partial Refund theo Refund Amount; Reject Claim và giải ngân cho nông hộ; thông báo các bên; tự xử lý khi quá hạn 48 giờ; cập nhật Refunded sau khi Finance xác nhận.
- **Nhận xét:** số field ở mức Medium, nhưng số transaction vượt ngưỡng Complex nên giữ Complex.

### 4.7 UC-AUTH-07 – Reset Password (Medium, giữ)

- **Căn cứ:** II.1.7; III.1.4 Reset Password.
- **Field (13, qua 3 bước):** Registered Email, Send Verification Code, Back to Sign In; OTP Verification Code, Verify, Resend Code, Back to Sign In; Verified Email, New Password, Confirm New Password, nút hiện mật khẩu, hướng dẫn chính sách mật khẩu, Reset Password.
- **Transaction (4):** gửi mã, xác minh mã, gửi lại mã (ba thao tác qua UC-AUTH-08), cập nhật mật khẩu và vô hiệu mã.
- **Nhận xét:** Medium phù hợp với cả hai tiêu chí.

### 4.8 UC-PS-01 – Manage Promotions and Vouchers (Medium, giữ)

- **Căn cứ:** II.15.1; III.17.1.1 Promotion and Voucher List.
- **Field (13):** New Promotion/Voucher, Status, Item Type, Search, Edit, Activate/Deactivate, Save, cùng 6 field chi tiết: tên hoặc mã, quy tắc giảm giá, điều kiện áp dụng, giới hạn sử dụng, ngày bắt đầu, ngày kết thúc.
- **Transaction (5):** tìm/lọc, tạo, cập nhật, bật/tắt, xóa hoặc lưu trữ.
- **Nhận xét:** Medium phù hợp. Nếu bổ sung chi tiết quy tắc giảm giá (loại, giá trị, mức trần), số field có thể vượt 15.

### 4.9 UC-ADM-09 – Manage Subscription Tiers (Medium, giữ)

- **Căn cứ:** II.4.5; III.4.4.1 Subscription Tier List.
- **Field (12):** New Tier, Status, Search, Edit, Activate/Deactivate, Save, cùng 6 field chi tiết: tên gói, giá, chu kỳ thanh toán, quyền lợi, giới hạn sử dụng, trạng thái.
- **Transaction (6):** tìm/lọc, tạo, cập nhật, bật/tắt, xóa vĩnh viễn khi không có thuê bao, lưu trữ khi đã có thuê bao.
- **Nhận xét:** Medium phù hợp với cả hai tiêu chí.

### 4.10 UC-AUTH-08 – Verify Email OTP (Simple, giữ)

- **Căn cứ:** II.1.8; bước 2 của III.1.4.
- **Field (3):** OTP Verification Code, Verify, Resend Code.
- **Transaction (3):** tạo và gửi OTP qua Gmail Notification Service API; xác minh OTP, gồm khóa sau 5 lần sai; gửi lại sau 60 giây.
- **Nhận xét:** Simple nhưng sát ngưỡng. Nếu nhóm tính việc khóa sau 5 lần sai là transaction riêng, UC chuyển thành Medium.

## 5. Thiếu sót tài liệu phát hiện được

Bảy điểm không nhất quán trong Report 3 ảnh hưởng trực tiếp tới việc đếm field và transaction.

| UC | Vị trí | Vấn đề | Ảnh hưởng |
| --- | --- | --- | --- |
| UC-FIN-03 | UC catalog, trang 52–53 | Không có đặc tả UC ở mục II và không có màn hình ở mục III. Catalog chỉ nêu hoàn tiền toàn phần, trong khi UC-CS-01 có cả Partial Refund. | Không kiểm chứng được mức Medium. |
| UC-ADM-01 | III.4.3.1 | Bảng field chỉ có 8 mục, trong khi postcondition II.4.4 nêu 4 nhóm chỉ số và 7 cột Pending Work List. | Theo bảng field, UC chỉ đạt Medium. |
| UC-ADM-05 | II.4.3 bước 7; III.4.2.1–4.2.2 | UC kiểm tra loại và dung lượng ảnh nhưng màn chi tiết không có field ảnh. Status Filter thiếu giá trị Archived. | Thiếu field; mô tả không khớp với quy tắc lưu trữ. |
| UC-ADM-03 | II.4.2 bước 4; III.4.1.1 | UC yêu cầu nhập lý do khi xác nhận nhưng User List không có field Reason; chỉ User Details nhắc tới. | Thiếu field trên hộp thoại xác nhận. |
| UC-CS-01 | II.14.1; III.16.1.1 | Actor chính là Dispute Officer nhưng Normal Flow ghi "Administrator". Bảng field thiếu Refund Amount và Fault Party dù postcondition có dùng. | Sai actor; thiếu 2 field. |
| UC-PS-01 | II.15.1 | Actor chính là Marketing/Promotion Staff nhưng Normal Flow ghi "Administrator". | Sai actor. |
| UC-RC-02 | II.6.2; III.6.1.1 | Màn hình có Payment Method nhưng các bước UC không nhắc tới. UC gọi actor là "Consumer", còn mã nguồn dùng RetailCustomer. | Luồng UC và màn hình không khớp; tên actor không thống nhất. |

## 6. Kiến nghị

Nhóm nên sửa Level trong bảng inventory trước, sau đó vá các chỗ thiếu trong Report 3 theo thứ tự ưu tiên dưới đây.

- [ ] Cập nhật Level của UC-ADM-03 và UC-ADM-05 từ Simple thành Medium trong bảng Screen/Function Inventory.
- [ ] Bổ sung đặc tả UC-FIN-03 ở mục II và màn hình xử lý hoàn tiền ở mục III, gồm cả trường hợp Partial Refund.
- [ ] Bổ sung đủ 17 field vào bảng III.4.3.1 của UC-ADM-01; nếu không, hạ Level xuống Medium.
- [ ] Sửa actor trong Normal Flow của UC-CS-01 (Dispute Officer) và UC-PS-01 (Marketing/Promotion Staff).
- [ ] Thêm field Refund Amount và Fault Party vào bảng III.16.1.1 của UC-CS-01.
- [ ] Thêm field ảnh danh mục và giá trị Archived cho UC-ADM-05; thêm field Reason vào hộp thoại xác nhận của UC-ADM-03.
- [ ] Thêm bước chọn Payment Method vào Normal Flow của UC-RC-02 và thống nhất tên actor Consumer/RetailCustomer.
- [ ] Nhóm chốt cách đếm transaction cho quy tắc khóa OTP để cố định mức của UC-AUTH-08.

## 7. Giới hạn

- Số liệu đếm theo Report 3 bản PDF ngày 24/10/2026. Nếu tài liệu thay đổi, cần đếm lại.
- Số transaction của UC-FIN-03 là ước tính từ UC catalog, không phải từ đặc tả đầy đủ.
- Ngưỡng Simple/Medium/Complex là quy ước nhóm đang dùng; đổi ngưỡng thì kết luận có thể thay đổi.
- Báo cáo chỉ đánh giá tài liệu, không đánh giá mức độ triển khai trong mã nguồn.
