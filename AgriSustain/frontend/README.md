# AgriSustain Frontend

React SPA tiếng Việt cho UC-ADM-03, UC-ADM-05 và UC-RC-02 theo Report 3/4.

- Công nghệ đã chọn: React 19.1.0 và Tailwind CSS.
- `src/app/router`: định tuyến trang.
- `src/app/providers`: các context/provider dùng chung.
- `src/api`: HTTP client và ánh xạ request/response API.
- `src/session`: phiên đăng nhập; access token được giữ trong bộ nhớ.
- `src/access`: điều hướng và kiểm soát giao diện theo quyền.
- `src/features`: các màn hình theo nhóm nghiệp vụ.
- `src/components/ui`, `layouts`, `hooks`, `types`, `assets`, `styles`: thành phần dùng chung.

Tại thư mục frontend: `npm ci`, rồi `npm run dev`. API cần chạy tại localhost:5179;
giao diện phát triển ở http://127.0.0.1:5173. `npm run build` tạo dist để API cũng phục vụ
giao diện tại http://localhost:5179. `npm run test:e2e` chạy Edge headless với server riêng.

Phiên đăng nhập và các phụ thuộc chưa có được giả lập, có comment DEMO-*; không có màn hình
đăng nhập, giỏ hàng CRUD hoặc QR payment.
Xem [hướng dẫn chạy/tích hợp](../docs/three-uc-demo-and-integration.md).

Sau review DB: xem [code/comment và kết quả kiểm tra](../docs/three-uc-code-review.md).
Các hàm đã có XML/JSDoc; contract mới bỏ ảnh/archive/Version, dùng RetailCustomer và FEFO.

Development API mặc định SqlDemo: ba màn đọc dữ liệu SQL đã seed. App hiển thị mode lấy
qua /health, danh mục/tài khoản/checkout giữ dữ liệu sau restart; auth/points/policy/retry
vẫn là mock. Feature tracking: Administration và Consumer Ordering; actor RetailCustomer.
Các thư mục router/providers/session/access ở trên là điểm tích hợp dự kiến; SPA hiện
chọn trực tiếp ba màn qua App.jsx, chưa có session provider hoặc JWT thật.
