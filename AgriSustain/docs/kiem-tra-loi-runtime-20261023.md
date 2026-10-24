# Kiểm tra lỗi runtime ngày 23/10/2026

## Phạm vi

Repository: C:\Users\huyng_jgxkbi0\Downloads\SEP490\SEP490_V1.0.

Kiểm tra thông báo không tải được danh sách người dùng trên frontend và stack trace
OperationCanceledException tại ReservationExpiryWorker trong ảnh người dùng gửi.

## 1. Lỗi tải danh sách người dùng

Các bằng chứng trước khi khởi động lại:

- Frontend cổng 5173 do tiến trình Vite PID 10732 phục vụ từ thư mục cũ
  C:\Users\huyng_jgxkbi0\Downloads\SEP490\AgriSustain\frontend.
- Không có tiến trình lắng nghe cổng backend 5179.
- GET http://127.0.0.1:5179/health bị connection refused.
- GET http://127.0.0.1:5173/health và GET /api/users qua Vite trả 502, body rỗng.
- Giao diện cũ hiển thị Phiên giả lập và thông báo lỗi chung vì không nhận được
  ProblemDetails từ backend.

Đã dừng đúng tiến trình Vite cũ sau khi kiểm tra đường dẫn executable/command line.
Đã chạy frontend và backend từ repository con theo lựa chọn trước đó của người dùng.
Frontend mới dùng đăng nhập JWT cho ba UC, không cấp quyền bằng demo selector.

## 2. Lỗi cancellation trong ReservationExpiryWorker

File sửa:
[ReservationExpiryWorker.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/BackgroundWorkers/ReservationExpiryWorker.cs).

Trước sửa, khối catch OperationCanceledException chỉ bao quanh thao tác xử lý một
batch. Lời gọi timer.WaitForNextTickAsync(stoppingToken) tại điều kiện while nằm
ngoài khối đó. Khi host dừng hoặc token khởi động bị hủy trong lúc chờ timer,
ExecuteAsync kết thúc bằng OperationCanceledException không được xử lý.

Đã thêm khối try/catch bao quanh vòng lặp chờ timer. Catch chỉ nhận
OperationCanceledException khi stoppingToken.IsCancellationRequested. Việc xử lý
hủy trong batch và ghi log lỗi batch vẫn được giữ nguyên, chu kỳ vẫn là 30 giây.
Không đổi HostOptions.BackgroundServiceExceptionBehavior sang Ignore và không tắt
worker trong cấu hình dự án để né lỗi.

Stack trace trong ảnh xuất hiện trong quá trình dispose/dừng host. Nó xác nhận
lỗi cancellation của worker, nhưng không đủ để xác định sự kiện khiến host bắt đầu
dừng. Nếu có lỗi khởi động khác ở các dòng log phía trước, cần chẩn đoán riêng.
Cổng 5179 hiện đã có một phiên backend do Codex khởi động; khởi động thêm một phiên
Visual Studio/dotnet ở cùng địa chỉ có thể tạo lỗi trùng cổng.

## 3. Test tái hiện và xác minh bản sửa

File test:
[ReservationExpiryWorkerTests.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/tests/Unit/ReservationExpiryWorkerTests.cs).

Thêm hai test thực thi worker và kiểm tra ExecuteTask hoàn tất thành công:

1. StopBeforeFirstTick_CompletesWorkerSuccessfully: start worker rồi gọi StopAsync
   khi timer chưa có nhịp đầu tiên.
2. CancelStartupTokenBeforeFirstTick_CompletesWorkerSuccessfully: hủy token truyền
   vào StartAsync trong lúc worker đang chờ timer.

Hai test không đợi 30 giây và không truy cập database. Trước sửa, cả hai fail bằng
OperationCanceledException từ PeriodicTimer và trỏ về dòng 14 của worker, khớp ảnh.
Sau sửa, chạy toàn bộ unit suite đạt 27/27 test, 0 fail, 0 skip. Build project ứng
dụng và project UnitTests thành công; còn cảnh báo scaffold DbContext có sẵn.

Lệnh đã chạy từ thư mục solution:

```powershell
dotnet test tests/Unit/AgriSustain.UnitTests.csproj --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false
```

## 4. Kiểm tra runtime sau khi sửa

Đã khởi động backend bản mới tại http://127.0.0.1:5179 với môi trường Development.
Worker bật theo appsettings.Checkout.json. Phiên chẩn đoán trước đó tạm tắt worker
đã được dừng trước khi build; phiên đang chạy sau sửa không dùng override tắt worker.

| Kiểm tra | Kết quả quan sát |
| --- | --- |
| Backend khởi động | Có log Now listening on http://127.0.0.1:5179. |
| GET /health qua Vite 5173 | Nhận JSON persistence=SqlServer. |
| GET /health/database trên backend | Nhận JSON connected=true; log SELECT 1 thành công. |
| Truy vấn định kỳ của worker | Đã quan sát hai lượt SELECT đơn hết hạn thực thi thành công. |
| GET /api/users chưa đăng nhập qua Vite | HTTP 401; không còn 502. |
| Assertion Postman cho API chưa đăng nhập | 1 passed: Unauthenticated users receive 401. |
| Đăng nhập bằng tài khoản demo trong tài liệu | Bị từ chối Invalid email or password; chưa xác nhận tài khoản thực tế. |

Postman request trả exit code 1 khi nhận HTTP 401, dù assertion kỳ vọng 401 báo
1 passed. Báo cáo giữ riêng mã HTTP, kết quả assertion và exit code để tránh coi
401 là thành công truy cập dữ liệu hoặc hiểu nhầm nó là lỗi proxy.

Các lượt truy vấn worker thành công chỉ xác nhận truy vấn đã chạy trên database
hiện tại; chưa chứng minh xử lý một đơn hết hạn thực tế và toàn bộ side effect.
Không chạy script nâng cấp/seed database, không reset mật khẩu và không tự tạo token
để bỏ qua cơ chế đăng nhập.

## 5. Phần cần tiếp tục xác minh

- Cần đăng nhập bằng tài khoản quản trị hợp lệ của database hiện tại để kiểm tra
  việc tải danh sách, bộ lọc và chi tiết người dùng qua giao diện.
- Chưa chạy lại SQL integration hoặc browser E2E cho toàn bộ nghiệp vụ.
- Cần log trước stack trace nếu tiếp tục gặp host dừng ngay khi F5.
- Backend hiện được giữ chạy trên 5179, frontend trên 5173. Khi chuyển sang debug
  bằng Visual Studio, cần dừng phiên backend hiện tại trước khi chiếm cùng cổng.

Code sửa worker và test hiện chưa commit, chưa push.
