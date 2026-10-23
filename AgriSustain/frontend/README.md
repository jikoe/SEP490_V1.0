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
giao diện phát triển ở http://127.0.0.1:5173. Trước khi chạy API, cấu hình
`ConnectionStrings:MyCnn` và `JWT:Secret` bằng User Secrets hoặc biến môi trường;
JWT secret cần tối thiểu 32 byte UTF-8. Ví dụ chạy từ thư mục frontend:

```powershell
dotnet user-secrets set "ConnectionStrings:MyCnn" "<your-sql-server-connection-string>" --project ..\AgriSustain\AgriSustain.csproj
dotnet user-secrets set "JWT:Secret" "<random-secret-at-least-32-bytes>" --project ..\AgriSustain\AgriSustain.csproj
```

Issuer và audience dành cho phát triển được đặt trong `appsettings.json`. Trang đầu là đăng
nhập bằng username/email và mật khẩu BCrypt trong bảng `User`. API cấp JWT tại
`POST /api/auth/login`. Token chỉ được giữ trong bộ nhớ trình duyệt và bị xóa khi tải lại trang
hoặc đăng xuất. BCrypt được xác thực rồi nâng cấp sang ASP.NET Identity hash sau lần đăng nhập
thành công đầu tiên. Tài khoản bị khóa 15 phút sau 5 lần nhập sai liên tiếp.
Nút “Tiếp tục với bản demo” giữ lại bộ chọn fixture cục bộ, không phải đăng nhập thật.
`npm run build` tạo dist để API cũng phục vụ
giao diện tại http://localhost:5179. `npm run test:e2e` chạy Edge headless với server riêng.

Các màn hình nghiệp vụ ngoài login vẫn còn một số phụ thuộc/mock theo demo; không có
giỏ hàng CRUD hoặc QR payment.
Xem [hướng dẫn chạy/tích hợp](../docs/three-uc-demo-and-integration.md).

Sau review DB: xem [code/comment và kết quả kiểm tra](../docs/three-uc-code-review.md).
Các hàm đã có XML/JSDoc; contract mới bỏ ảnh/archive/Version, dùng RetailCustomer và FEFO.

Development API mặc định SqlDemo: các màn nghiệp vụ đọc dữ liệu SQL đã seed. App hiển thị mode lấy
qua /health, danh mục/tài khoản/checkout giữ dữ liệu sau restart; points/policy/retry
vẫn chưa hoàn chỉnh. Feature tracking: Administration và Consumer Ordering; actor RetailCustomer.
Login đã dùng session adapter tại `src/session` và token JWT trong bộ nhớ; demo fixture vẫn là
chế độ riêng cho phát triển.
