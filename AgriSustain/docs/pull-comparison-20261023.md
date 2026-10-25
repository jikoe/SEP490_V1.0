# Đối chiếu code sau khi pull ngày 23/10/2026

## Repository được cập nhật

- Thư mục: `C:\Users\huyng_jgxkbi0\Downloads\SEP490\SEP490_V1.0`.
- Remote: `https://github.com/jikoe/SEP490_V1.0.git`.
- Nhánh: `main`, cập nhật bằng `git pull --ff-only origin main`.
- Trước pull: `ae4d9e79c256e2689142e6fd1eb8caff09126060`.
- Sau pull: `d2b64f71e37022e642067a0871cde1793bf7e920`.
- Nhận thêm 8 commit; chênh lệch giữa hai commit gồm 106 file, 10.051 dòng thêm,
  125 dòng xóa theo Git. Đây là thống kê code trên GitHub, chưa tính code cục bộ.
- HEAD và origin/main không có commit lệch nhau tại thời điểm kiểm tra.
- Thư mục repository gốc `C:\Users\huyng_jgxkbi0\Downloads\SEP490` chỉ được fetch
  để đối chiếu; không pull hoặc ghép code vào working tree của repository gốc.

## Các phần mới nhận từ GitHub

| Nhóm | Thay đổi chính |
| --- | --- |
| Xác thực, hồ sơ | Thêm service/repository, validator và giao diện đăng ký, hồ sơ, địa chỉ. |
| Sản phẩm | Thêm API, nghiệp vụ và màn hình quản lý sản phẩm, nhiều ảnh sản phẩm. |
| Giỏ hàng, đơn hàng | Thêm giỏ hàng, tính giá, xử lý đơn phía người bán và theo dõi đơn. |
| Giao diện | Thêm bố cục customer portal, header/footer và điều hướng giỏ hàng. |
| Hạ tầng | Di chuyển DbContext vào Infrastructure/Data, thêm cấu hình concurrency và UnitOfWork. |
| SQL | Thêm AgriSustain_For_SSMS.sql; chưa thực thi SQL trong lần cập nhật này. |

## Bảo toàn và ghép code cục bộ

Trước khi pull, working tree có 10 file tracked thay đổi và 78 file untracked.
Đã lưu cả hai nhóm bằng `git stash push --include-untracked`.

- Tên stash: `codex-before-pull-20261023-SEP490_V1.0`.
- Stash commit: `52eea93d3d37ead41eb5a48c1ccece1362f87968`.
- Stash vẫn được giữ sau khi áp dụng lại, không drop.
- Kiểm tra đủ 78 đường dẫn untracked ban đầu: không thiếu file nào.
- Trong nhóm này, 68 file giữ nguyên nội dung theo Git hash; 10 file được ghép với
  code upstream hoặc điều chỉnh tương thích. Interface cũ của code cục bộ được
  bảo toàn dưới tên IUseCaseUnitOfWork; upstream giữ IUnitOfWork hiện có.
- Các chỉnh sửa hiện tại vẫn chưa commit và chưa push.

| Vị trí xung đột/tương thích | Cách xử lý |
| --- | --- |
| AgriSustain.csproj | Giữ các package upstream, tránh khai báo FluentValidation trùng. |
| Program.cs | Ghép đăng ký ba UC cục bộ với service, validator, middleware và endpoint upstream. |
| AgriSustainContext.cs | Giữ bản đã di chuyển và cập nhật từ upstream; gọi thêm cấu hình model của ba UC. |
| Error.cs | Giữ kiểu chung và bổ sung Fields tùy chọn cho lỗi validation cục bộ. |
| IUnitOfWork | Tách hợp đồng giao dịch cục bộ thành IUseCaseUnitOfWork, cập nhật DI và các service sử dụng. |
| CheckoutScreen.jsx | Giữ giao diện/hợp đồng API checkout cục bộ và ghép điều hướng sang giỏ hàng. |
| vite.config.js | Giữ proxy API/health và bổ sung proxy media từ upstream. |
| ProductImages | Đổi truy cập ảnh đơn của checkout sang collection ProductImages và chọn ảnh IsPrimary. |
| HarvestBatch | Đổi tên quy tắc cục bộ thành IsReservableForCheckout để tránh trùng IsSaleable của upstream. |

Hai quy tắc lô hàng hiện vẫn giữ hành vi riêng trước khi ghép: checkout cục bộ chỉ
chấp nhận trạng thái Available/InStock; quy tắc upstream còn chấp nhận Harvested và
kiểm tra ngày thu hoạch/hết hạn khác. Lần pull này không tự thay đổi chính sách đó.

## Kết quả kiểm tra

- `dotnet restore AgriSustain.sln`: thành công.
- `dotnet build AgriSustain.sln --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false`:
  thành công, 0 lỗi; còn 1 cảnh báo #warning có sẵn trong DbContext scaffold.
- `npm run build` trong frontend: thành công.
- Unit tests: 25/25 đạt, gồm kiểm tra domain, validator và tạo script từ EF model.
- Architecture tests: 3/3 đạt.
- Không còn unmerged paths hoặc dấu conflict trong source; `git diff --check` đạt.
- Chưa chạy SQL integration tests, E2E trình duyệt hoặc xác minh nghiệp vụ toàn hệ thống.
  Kết quả build và các test trên không xác nhận schema database đang chạy đã tương thích.
