# AgriSustain - Client (Giao diện Web Thuần)

Thư mục `Client` là phiên bản giao diện thuần HTML/CSS/JavaScript (Vanilla JS) được xây dựng dựa trên kiến trúc và phong cách tổ chức code của **EquipmentBorrowingManagementSystem/Client**, đồng thời kế thừa chính xác 1:1 toàn bộ giao diện, luồng màn hình và chức năng từ `AgriSustain/frontend`.

---

## 1. Cấu trúc thư mục

```text
AgriSustain/Client/
├── css/
│   ├── style.css             # Định kiểu toàn bộ hệ thống (App Shell, Typography, Forms, Tables, Alerts)
│   └── components.css        # Định kiểu Marketplace, Giỏ hàng, Chi tiết sản phẩm, v.v.
├── js/
│   ├── config.js             # Cấu hình API Base URL (tự động proxy qua cổng 5173 đến 5179)
│   ├── utils.js              # Tiện ích định dạng tiền VND, ngày tháng, sanitize HTML, Toasts
│   ├── api.js                # Wrapper API fetch có tự động gắn JWT Bearer Token
│   ├── auth.js               # Quản lý phiên đăng nhập JWT, kiểm tra quyền và điều hướng vai trò
│   └── components.js         # Khởi tạo App Shell Sidebar & Header cho các trang quản trị
├── Guest/                    # Màn hình công khai cho Khách vãng lai
│   ├── login.html            # Màn hình Đăng nhập (Khớp 1:1 LoginScreen)
│   ├── register.html         # Màn hình Đăng ký tài khoản & Xác thực OTP Email
│   └── forgot-password.html  # Màn hình Quên mật khẩu & Đặt lại mật khẩu 3 bước
├── RetailCustomer/           # Các màn hình cho Khách mua lẻ (Khớp 1:1 Frontend)
│   ├── index.html            # Sàn giao dịch Nông Sản Việt (Marketplace)
│   ├── product-detail.html   # Chi tiết sản phẩm & Thêm vào giỏ / Mua buôn
│   ├── cart.html             # Giỏ hàng nông sản
│   ├── checkout.html         # Thanh toán & Đặt hàng
│   ├── orders.html           # Theo dõi trạng thái đơn hàng
│   ├── profile.html          # Hồ sơ khách hàng cá nhân
│   └── feedback.html         # Đánh giá đơn hàng
├── Administrator/            # Các màn hình cho Quản trị viên (Dashboard, Quản lý người dùng, Danh mục)
├── Farmer/                   # Các màn hình cho Nông dân / Hợp tác xã (Sản phẩm, Đơn hàng, Hồ sơ)
├── ContentModerator/         # Kiểm duyệt sản phẩm
├── CustomerSupport/          # Hỗ trợ & xử lý khiếu nại
├── FinanceStaff/             # Quản lý tài chính & Hoàn tiền
├── PromotionStaff/           # Chiến dịch khuyến mại & Voucher
├── VerificationOfficer/      # Thẩm định chất lượng VietGAP
├── WholesaleBuyer/           # Mua buôn B2B
├── PaymentGateway/           # Giám sát cổng thanh toán PayOS / VNPay
├── GmailService/             # Giám sát dịch vụ gửi email OTP
├── index.html                # Trang gốc chuyển hướng nhanh vào RetailCustomer/index.html
├── package.json              # Khai báo Vite server
└── vite.config.js            # Cấu hình Proxy tự động chuyển request /api sang Backend (5179)
```

---

## 2. Hướng dẫn chạy Client

### Bước 1: Mở terminal tại thư mục `Client`
```powershell
cd d:\SEP\Git_Xum\SEP490_V1.0\AgriSustain\Client
```

### Bước 2: Cài đặt thư viện Vite (chỉ cần làm lần đầu)
```powershell
npm.cmd install
```

### Bước 3: Khởi chạy giao diện Client
```powershell
npm.cmd run dev
```

Terminal sẽ hiển thị:
```text
  VITE v5.4.x ready in ... ms
  ➜  Local:   http://127.0.0.1:5173/
```

Mở trình duyệt truy cập: `http://127.0.0.1:5173`

---

## 3. Luồng điều hướng các màn hình

1. **Khách vãng lai / Trang chủ**:
   - Truy cập `http://127.0.0.1:5173/` hiển thị **Chợ Nông Sản Việt (`index.html`)** với danh mục, bộ lọc giá, xuất xứ, chứng nhận VietGAP và danh sách sản phẩm.
   - Nhấp vào bất kỳ sản phẩm nào sẽ dẫn đến trang **Chi tiết sản phẩm (`product-detail.html?id=...`)** có ảnh, thông số kỹ thuật, thông tin lô hàng, chứng nhận, đánh giá và nút thêm vào giỏ hàng.
2. **Đăng nhập (`login.html`)**:
   - Giao diện chuẩn với biểu tượng chiếc lá, nhánh cây xanh và form đăng nhập.
   - Sau khi nhập tài khoản hợp lệ, hệ thống gọi API backend `/api/auth/login`, lưu JWT token và tự động chuyển về đúng khu vực làm việc theo vai trò:
     - **Administrator** ➔ `Administrator/dashboard.html`
     - **Farmer** ➔ `Farmer/products.html`
     - **RetailCustomer** ➔ `index.html` (hoặc `RetailCustomer/orders.html`)
     - **ContentModerator** ➔ `ContentModerator/moderation.html`
     - **FinanceStaff** ➔ `FinanceStaff/dashboard.html`
     - v.v.
3. **Đăng ký tài khoản (`register.html`)**:
   - Chọn vai trò `RetailCustomer` hoặc `Farmer`.
   - Nhập họ tên, số điện thoại (10 số), email, mật khẩu đạt chuẩn.
   - Gửi yêu cầu đăng ký ➔ Hệ thống mở popup nhập mã OTP 6 chữ số gửi qua email (có đếm ngược 60 giây gửi lại mã).
4. **Quên mật khẩu (`forgot-password.html`)**:
   - Quy trình 3 bước chuẩn bảo mật:
     - Bước 1: Nhập email đăng ký để nhận mã OTP.
     - Bước 2: Nhập mã OTP 6 số để nhận quyền đặt lại.
     - Bước 3: Tạo mật khẩu mới và tự động chuyển về trang đăng nhập.
