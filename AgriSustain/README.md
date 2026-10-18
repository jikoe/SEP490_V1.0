# AgriSustain

Bộ khung ASP.NET Core Web API .NET 8 gồm bốn layer theo [Package_Diagram.drawio](https://app.diagrams.net/#G1mJnzsACg-bGQhl9gV-hjsKAF_qAYStPE). Application chia theo loại thành phần: Services, Interfaces, DTOs, Mappings, Validators và Common.

## Cấu trúc thư mục

```text
AgriSustain/
├── AgriSustain.sln
├── Directory.Build.props
├── global.json
├── src/
│   ├── AgriSustain.Api/
│   │   ├── Controllers/
│   │   ├── Security/
│   │   ├── Middlewares/
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json
│   │   ├── Program.cs
│   │   └── AgriSustain.Api.csproj
│   ├── AgriSustain.Application/
│   │   ├── Services/
│   │   ├── Interfaces/
│   │   ├── DTOs/
│   │   ├── Mappings/
│   │   ├── Validators/
│   │   ├── Common/
│   │   ├── ApplicationAssemblyMarker.cs
│   │   └── AgriSustain.Application.csproj
│   ├── AgriSustain.Domain/
│   │   ├── Entities/
│   │   ├── DomainAssemblyMarker.cs
│   │   └── AgriSustain.Domain.csproj
│   └── AgriSustain.Infrastructure/
│       ├── Data/
│       ├── Repositories/
│       ├── UnitOfWork/
│       ├── Payments/
│       ├── FileStorage/
│       ├── Email/
│       ├── Auditing/
│       ├── BackgroundWorkers/
│       ├── DependencyInjection.cs
│       └── AgriSustain.Infrastructure.csproj
├── frontend/
├── tests/
│   ├── Unit/
│   ├── Integration/
│   └── Architecture/
└── docs/
    └── package-map.md
```

Các package chưa có implementation chứa .gitkeep để Git lưu được thư mục. Properties và các file startup/project là thành phần hỗ trợ của .NET. Frontend và tests nằm ngoài bốn layer backend.

## Hướng phụ thuộc

```text
Api            -> Application, Infrastructure
Application    -> Domain
Infrastructure -> Application, Domain
Domain         -> không tham chiếu project khác
```

API tham chiếu Infrastructure để đăng ký DI trong Program.cs. Controller gọi application service thông qua interface, không thao tác trực tiếp repository implementation hoặc DbContext. Application chỉ phụ thuộc interface và Domain; Infrastructure triển khai các interface đó.

React SPA giao tiếp với API qua HTTPS/JSON và Bearer JWT. SQL Server, PayOS/VietQR và Google Gmail API là tài nguyên/dịch vụ ngoài, được truy cập qua Infrastructure.

## Đặt code theo sơ đồ

| Thư mục | Trách nhiệm |
| --- | --- |
| Api/Controllers | HTTP endpoint và payment webhook |
| Api/Security | Cấu hình authentication, authorization, policy và handler |
| Api/Middlewares | Xử lý exception và các bước trong HTTP pipeline |
| Application/Services | Điều phối use case và transaction nghiệp vụ |
| Application/Interfaces | Hợp đồng application service, repository, Unit of Work và tích hợp |
| Application/DTOs | Request và response cho use case |
| Application/Mappings | Chuyển đổi entity và DTO |
| Application/Validators | Kiểm tra đầu vào trước khi xử lý |
| Application/Common | Kiểu kết quả, phân trang và thành phần dùng chung |
| Domain/Entities | Thực thể và hành vi bảo vệ quy tắc nghiệp vụ |
| Infrastructure/Data | DbContext, EF Core configuration, migrations và seed khi triển khai |
| Infrastructure/Repositories | Triển khai repository interface |
| Infrastructure/UnitOfWork | Triển khai Unit of Work và quản lý transaction |
| Infrastructure/Payments | Adapter PayOS/VietQR |
| Infrastructure/FileStorage | Lưu ảnh và tài liệu |
| Infrastructure/Email | Adapter Google Gmail API |
| Infrastructure/Auditing | Ghi lịch sử hành động |
| Infrastructure/BackgroundWorkers | Lập lịch và gọi lại application service qua DI scope |

Ví dụ vị trí file khi triển khai đặt hàng:

```text
Api/Controllers/OrdersController.cs
Application/Services/OrderService.cs
Application/Interfaces/IOrderService.cs
Application/Interfaces/IOrderRepository.cs
Application/Interfaces/IUnitOfWork.cs
Application/DTOs/CreateOrderRequest.cs
Application/DTOs/OrderResponse.cs
Application/Mappings/OrderMappings.cs
Application/Validators/CreateOrderRequestValidator.cs
Domain/Entities/Order.cs
Infrastructure/Data/AppDbContext.cs
Infrastructure/Repositories/OrderRepository.cs
Infrastructure/UnitOfWork/UnitOfWork.cs
```

Các file ví dụ trên chưa được triển khai. Khi số lượng file tăng, có thể nhóm theo nghiệp vụ bên trong từng package mà vẫn giữ các package cấp đầu của sơ đồ.

## Mở và chạy

Mở AgriSustain.sln trong Visual Studio, chọn AgriSustain.Api làm startup project. TargetFramework net8.0 được khai báo chung trong Directory.Build.props.

```powershell
dotnet build AgriSustain.sln
dotnet run --project src/AgriSustain.Api
```

Endpoint kiểm tra: http://localhost:5179/health.

Program.cs là composition root đăng ký service, mapping, validator và gọi AddInfrastructure. Hiện chưa có application service cần đăng ký; AddInfrastructure vẫn là điểm mở rộng trống. Chưa triển khai EF Core, JWT, PayOS, Gmail hoặc nghiệp vụ. Frontend và tests vẫn là bộ khung.

Xem [package-map.md](docs/package-map.md) để đối chiếu từng package với sơ đồ.
