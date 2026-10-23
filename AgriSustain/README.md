# AgriSustain Code Structure

ASP.NET Core Web API 8.0 giữ bốn project backend theo Report 4. Hiện có code cho đúng ba UC:
UC-ADM-03 (trạng thái tài khoản), UC-ADM-05 (danh mục) và UC-RC-02 (checkout bán lẻ),
cùng giao diện React. Ba UC đọc/ghi SQL Server trong Development; auth, điểm, phí giao và retry key vẫn giả lập theo yêu cầu.

Cập nhật EF Core Database First: [cấu hình, lệnh scaffold và cách chạy](docs/ef-core-database-first.md).
Đã lấy 47 entity từ database `AgriSustain`; [đối chiếu giao diện Report 3](docs/three-uc-srs-screen-review.md).

| Project | Package chính |
| --- | --- |
| `src/AgriSustain.Api` | `Controllers`, `Security`, `Middlewares` |
| `src/AgriSustain.Application` | `Services`, `Interfaces`, `DTOs`, `Mappings`, `Validators`, `Common` |
| `src/AgriSustain.Domain` | `Entities` |
| `src/AgriSustain.Infrastructure` | `Data`, `Repositories`, `UnitOfWork`, `Payments`, `FileStorage`, `Email`, `Auditing`, `BackgroundWorkers` |

`Program.cs` là điểm khởi động và đăng ký DI. Các lớp mở rộng đăng ký Application/Infrastructure đặt ở cấp gốc của project tương ứng; không tạo package `CompositionRoot` hoặc `DependencyInjection` riêng. `Properties` là cấu hình chạy của Visual Studio, không phải package nghiệp vụ.

Các nhóm feature đặt trong Services, DTOs và Validators. Domain có Entities, ValueObjects
và Exceptions thực sự được dùng bởi ba UC.

## Mở và chạy backend

Mở `AgriSustain.sln` trong Visual Studio và chọn startup project `AgriSustain.Api`. Cài .NET SDK phù hợp với `global.json`.

```powershell
dotnet restore AgriSustain.sln
npm --prefix frontend ci
npm --prefix frontend run build
dotnet run --project src/AgriSustain.Api --configuration Release
```

Mở http://localhost:5179 để dùng giao diện đã build. Health: http://localhost:5179/health.
Visual Studio: chọn AgriSustain.Api làm startup project, profile Development và F5.

## Trạng thái triển khai

Logic ba UC đã có trong Domain/Application/API. Có xUnit UT, API/demo integration,
architecture tests và E2E cho ba màn hình. Selector phiên là fixture, không phải đăng nhập.
Development mặc định SqlServer qua EF Core: restart giữ dữ liệu SQL. Auth/điểm/retry-key cache vẫn reset
theo mock; Demo chỉ chạy khi Development và Demo:Enabled bật; chưa có JWT/PayOS thật.
Xem [seed và chạy SQL](database/demo-data-and-web.md), [luồng theo file](docs/three-uc-file-flows.md)
và [hướng dẫn/đối chiếu test](docs/three-uc-testing-guide-and-report-review.md).

Theo yêu cầu mới, bản EF/UI chỉ build và kiểm tra nhanh trong lúc chờ duyệt code;
chưa chạy lại bộ unit/integration/E2E chi tiết. Kết quả test cũ không xác nhận bản này.

- [Hướng dẫn chạy, API, comment và thay từng mock](docs/three-uc-demo-and-integration.md).
- [16 vấn đề database và giải pháp](docs/database-issues-and-solutions.md).
- [Database đã cập nhật và bộ SQL](database/README.md).
- [Kiểm tra code, comment và điểm tích hợp](docs/three-uc-code-review.md).
- [Tests](tests/README.md).

Xem [docs/package-map.md](docs/package-map.md) để đối chiếu 18 package, hướng phụ thuộc và quy ước namespace.
