# Kết nối SQL bằng EF Core Database First

Cập nhật 19/10/2026. Phạm vi logic/màn hình chỉ UC-ADM-03, UC-ADM-05, UC-RC-02.
Đã scaffold metadata từ `(localdb)\MSSQLLocalDB`, database `AgriSustain`: 47 entity
(46 bảng gốc đã review + `RetailCheckoutQuote`). Không chạy migration hay đổi schema
trong lần chuyển EF này; database `AgriSustainDB` không được chọn.

## 1. File cấu hình và đăng ký

Tên chuẩn ASP.NET Core là **appsettings.json**, có chữ **s**. File:
`src/AgriSustain.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AgriSustain": "Server=(localdb)\\MSSQLLocalDB;Database=AgriSustain;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

Đây là phần connection của file; giữ nguyên các mục Logging/AllowedHosts hiện có.
Windows Authentication không cần username/password SQL. LocalDB chạy theo tài khoản
Windows hiện tại. Nếu nhóm chuyển sang SQL Authentication, đặt bí mật bằng user-secrets
hoặc biến môi trường `ConnectionStrings__AgriSustain`, không commit password.

### Khi Visual Studio báo LocalDB startup failed

Đã ghi nhận host Debug ở 5179 báo SQL error `-1983577846` / `0x89C5010A`, Windows
`WaitForMultipleObjects: 575`, trong khi cùng binary chạy riêng và instance hiện tại vẫn
đọc được database. Restart Debug chưa khắc phục. Đây là lỗi discovery/startup LocalDB
trong ngữ cảnh host đó; nguyên nhân sâu hơn của Windows runtime chưa được khẳng định.

Khắc phục local bằng named pipe của **instance đang chạy**, vẫn Windows Authentication
và đúng database AgriSustain:

```powershell
powershell -ExecutionPolicy Bypass -File tools/use-running-localdb.ps1
# Chỉ khi instance đang dừng và muốn khởi động nó:
powershell -ExecutionPolicy Bypass -File tools/use-running-localdb.ps1 -StartIfStopped
```

Script lấy pipe từ `sqllocaldb info`, SELECT kiểm tra đúng database/schema rồi ghi
`src/AgriSustain.Api/appsettings.Local.json`. File bị Git ignore; không đưa pipe của máy
mình lên repository. Program chỉ nạp file này trong Development; environment/CLI vẫn
có ưu tiên cao hơn. Lần đầu cần Stop/Start Debug để nạp Program mới. Sau đó JSON có
reloadOnChange. Khi LocalDB khởi động lại, chạy lại script vì suffix pipe thay đổi.
Không sửa appsettings.json chung sang một pipe cố định, không xóa/tạo lại instance
hoặc attach/drop database để xử lý lỗi này.

Microsoft mô tả cách kết nối pipe và tính tạm thời của suffix tại
[SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-ver17#start-localdb-and-connect-to-localdb).
Lỗi kết nối SQL nghiêm trọng hiện trả HTTP 503 `DATABASE_UNAVAILABLE` với thông báo
an toàn; chi tiết kỹ thuật chỉ ở log server.

`appsettings.Development.json` chọn `Persistence:Mode=SqlServer`, `Demo:Enabled=true`.
Chữ Demo chỉ các phụ thuộc chưa có: đăng nhập/thu hồi phiên, điểm, policy giao và retry key.
Repository mặc định **đọc/ghi SQL bằng EF**, không tự chuyển sang bộ nhớ nếu SQL lỗi.
`InMemory` chỉ dành cho fixture test cũ khi chủ động thay UoW/identity.

`Program.cs` gọi `AddDbContext<AgriSustainDbContext>(options => options.UseSqlServer(...))`
qua overload nhận DI provider. Context scoped, connection lấy từ configuration. API dùng
controller/service/DTO; không trả trực tiếp graph entity hoặc PasswordHash ra trình duyệt.

## 2. Lệnh đã chạy để lấy entity

Từ thư mục `AgriSustain`:

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef dbcontext scaffold Name=ConnectionStrings:AgriSustain Microsoft.EntityFrameworkCore.SqlServer --project src/AgriSustain.Infrastructure --startup-project src/AgriSustain.Api --context AgriSustainDbContext --context-dir Data/Contexts --output-dir Data/Entities --use-database-names --no-pluralize --no-onconfiguring --configuration Release
powershell -ExecutionPolicy Bypass -File tools/document-scaffold.ps1
```

Local tool manifest `.config/dotnet-tools.json` pin `dotnet-ef` **8.0.26** để khớp
provider và package Design **8.0.26** trên .NET 8. Không đổi tool global của máy.
Package Design đặt ở startup project API; provider SqlServer đặt ở Infrastructure.

Lệnh scaffold trên đã chạy thành công. Entity nằm trong `Infrastructure/Data/Entities/`;
context trong `Infrastructure/Data/Contexts/AgriSustainDbContext.cs`. Không có
`OnConfiguring` hoặc connection string nhúng trong source sinh tự động. Giữ nguyên tên
bảng/cột, khóa, quan hệ, precision/default/index của database hiện có.

Khi cần scaffold lại sau thay đổi DB: lưu phần chỉnh tay trước, thêm `--force` vào lệnh
để ghi đè file generated, chạy lại helper comment và review diff. Viết phần mở rộng trong
file partial riêng; không đặt business logic vào entity generated. Không chạy
`database update`/`EnsureCreated` cho database đang dùng theo Database First.

Tham chiếu: [Microsoft — Reverse engineering](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/),
[CLI EF Core](https://learn.microsoft.com/ef/core/miscellaneous/cli/dotnet).

## 3. Luồng logic thực tế

`Controller → Application Service → IThreeUcUnitOfWork → EfThreeUcUnitOfWork`
`→ EfThreeUcSession → AgriSustainDbContext → SQL Server`.

| Thành phần | Trách nhiệm |
| --- | --- |
| `Repositories/EfCore/EfThreeUcSession.Administration.cs` | LINQ search/count/page, projection KYC, cập nhật User status/lock; CRUD Category và kiểm tra mọi product/FK reference |
| `Repositories/EfCore/EfThreeUcSession.Checkout.cs` | Đọc giỏ/địa chỉ/product/batch/voucher, lưu quote, graph Order/SellerOrder/OrderItem/Allocation, voucher reservation, xóa selected cart |
| `Repositories/EfCore/EfThreeUcSession.cs` | Dùng chung context; stage AuditLog và detached mock projections |
| `UnitOfWork/EfThreeUcUnitOfWork.cs` | Serializable transaction, final SaveChanges/commit hoặc rollback, publish mock sau commit; không commit từng repository |
| `Repositories/EfCore/EfDemoIdentityReader.cs` | Chỉ map selector local sang username seed rồi đọc ID/Role/Active/lock từ SQL; không thực hiện đăng nhập |

Các adapter raw SQL cũ `Repositories/Sql/*` và `SqlThreeUcUnitOfWork` đã được thay thế.
Business query/CRUD dùng EF LINQ, Add/Remove/SaveChanges. Tồn kho dùng
`ExecuteUpdateAsync` với điều kiện counter/status/expiry để tránh ghi đè stock mới;
selected cart dùng `ExecuteDeleteAsync` theo đúng chủ giỏ. Một câu SQL cố định gọi
`sp_getapplock` chỉ dùng điều phối transaction giữa các host local; cần review throughput
trước triển khai thực tế.

Category/Order lấy IDENTITY qua insert placeholder **trong transaction chưa commit**
để giữ contract service hiện tại. Chỉ sau khi snapshot/stock/cart/benefit/audit hợp lệ mới
commit. Lỗi phải rollback cả placeholder. Không dùng MAX(id)+1.

## 4. Chạy và xem kết nối

```powershell
dotnet build AgriSustain.sln --configuration Release
npm --prefix frontend run build
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --no-build --project src/AgriSustain.Api --configuration Release --no-launch-profile -- --urls http://127.0.0.1:5181
```

- Web: http://127.0.0.1:5181/
- Swagger Development: http://127.0.0.1:5181/swagger
- Liveness/config: `/health`.
- Kết nối thực: `/health/database` mở SQL, đọc Category và trả provider/database/47 mapping.
  Sidebar chỉ báo đã kết nối sau readiness thành công.
- Swagger Authorize: nhập `Demo admin` hoặc `Demo retail-customer` khi chạy local.
  Đây là selector review được phép giả lập, không phải tài khoản/mật khẩu hay JWT.

Phiên preview lần này dùng 5181; profile Visual Studio vẫn là 5179. Không chạy hai host
cùng cổng; xem đúng URL/profile trước khi kết luận giao diện vẫn là bản cũ.

## 5. Phần đã kiểm tra và giới hạn

Đã build solution và frontend; kiểm tra tĩnh comment: 289 C# declaration có tài liệu,
34 hàm JS có JSDoc ở thời điểm chuyển EF. Đã gọi GET readiness, users/categories,
checkout context và Swagger; browser mở list/detail/category form/checkout. Preview
quote ghi `RetailCheckoutQuote` bằng EF mà không đặt đơn/giữ tồn.

Theo yêu cầu mới của người dùng, **chưa chạy lại unit/integration/E2E chi tiết** cho
bản EF/UI này và không dùng kết quả Passed cũ để xác nhận bản mới. Test fixture SQL
đã đổi DI sang EF và connection riêng để solution biên dịch; cần chạy sau duyệt code.
Chưa smoke thao tác suspend, CRUD danh mục hoặc tạo đơn lên DB người dùng lần này.

Các module chưa tích hợp: auth thật, durable points/retry, quản lý giỏ, payment/QR,
ETA và chính sách phí giao chính thức, worker release/expiry. Comment MOCK/DEMO tại
adapter chỉ rõ đầu mối cần thay. Đọc SQL thật không đồng nghĩa các module này đã hoàn tất.
