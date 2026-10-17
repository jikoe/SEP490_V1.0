# AgriSustain Frontend

Bộ khung thư mục cho React SPA theo Report 1, Report 2 và Report 4.

- Công nghệ đã chọn: React 19.1.0 và Tailwind CSS.
- `src/app/router`: định tuyến trang.
- `src/app/providers`: các context/provider dùng chung.
- `src/api`: HTTP client và ánh xạ request/response API.
- `src/session`: phiên đăng nhập; access token được giữ trong bộ nhớ.
- `src/access`: điều hướng và kiểm soát giao diện theo quyền.
- `src/features`: các màn hình theo nhóm nghiệp vụ.
- `src/components/ui`, `layouts`, `hooks`, `types`, `assets`, `styles`: thành phần dùng chung.

Thư mục này chưa khởi tạo ứng dụng React hoặc cài dependency. Backend API là startup project của solution Visual Studio.
