# AgriSustain

Bộ khung backend ASP.NET Core Web API .NET 8 theo Clean Architecture với 4 layer. Application gom code theo chức năng để service, request, response và validator của cùng nghiệp vụ nằm gần nhau.

## Cấu trúc hiện tại

```text
AgriSustain/
├── AgriSustain.sln
├── Directory.Build.props
├── global.json
├── src/
│   ├── AgriSustain.Api/                 # Layer 1: HTTP và startup
│   │   ├── Controllers/
│   │   ├── Middleware/
│   │   ├── Authorization/
│   │   ├── Extensions/
│   │   │   └── ApplicationServiceCollectionExtensions.cs
│   │   ├── Properties/launchSettings.json
│   │   ├── appsettings.json
│   │   └── Program.cs
│   ├── AgriSustain.Application/         # Layer 2: điều phối nghiệp vụ
│   │   ├── Features/
│   │   │   ├── Authentication/
│   │   │   ├── Profiles/
│   │   │   ├── Catalog/
│   │   │   ├── Farming/
│   │   │   ├── Traceability/
│   │   │   ├── Orders/
│   │   │   │   ├── Requests/
│   │   │   │   ├── Responses/
│   │   │   │   ├── Validators/
│   │   │   │   └── README.md
│   │   │   ├── PreOrders/
│   │   │   ├── Contracts/
│   │   │   ├── Payments/
│   │   │   ├── Subscriptions/
│   │   │   ├── Verification/
│   │   │   ├── Promotions/
│   │   │   ├── Disputes/
│   │   │   ├── Finance/
│   │   │   ├── PriceBenchmarks/
│   │   │   └── Notifications/
│   │   ├── Abstractions/
│   │   │   ├── Persistence/
│   │   │   ├── Identity/
│   │   │   ├── Payments/
│   │   │   ├── Notifications/
│   │   │   ├── Storage/
│   │   │   ├── Caching/
│   │   │   ├── Auditing/
│   │   │   └── Reporting/
│   │   ├── Common/
│   │   └── ApplicationAssemblyMarker.cs
│   ├── AgriSustain.Domain/              # Layer 3: mô hình và quy tắc cốt lõi
│   │   ├── Entities/
│   │   │   ├── Accounts/
│   │   │   ├── Catalog/
│   │   │   ├── Farming/
│   │   │   ├── Orders/
│   │   │   ├── Contracts/
│   │   │   ├── Billing/
│   │   │   └── Engagement/
│   │   ├── Enums/
│   │   ├── Exceptions/
│   │   ├── ValueObjects/
│   │   └── DomainAssemblyMarker.cs
│   └── AgriSustain.Infrastructure/      # Layer 4: triển khai kỹ thuật
│       ├── Persistence/
│       │   ├── Configurations/
│       │   ├── Repositories/
│       │   └── Migrations/
│       ├── Identity/
│       ├── Payments/PayOS/
│       ├── Email/Gmail/
│       ├── Storage/
│       ├── Caching/
│       ├── Auditing/
│       ├── BackgroundJobs/
│       └── DependencyInjection.cs
├── frontend/                           # Bộ khung React, chưa khởi tạo app
├── tests/                              # Chưa có test project
│   ├── Unit/
│   ├── Integration/
│   └── Architecture/
└── docs/
    └── package-map.md
```

Thư mục chưa có triển khai chứa .gitkeep để lưu trong Git. Orders minh họa cách nhóm file trong một feature; các feature khác chỉ thêm Requests, Responses, Validators khi có code tương ứng.

## Quy tắc phụ thuộc

```text
Api            -> Application, Infrastructure
Application    -> Domain
Infrastructure -> Application, Domain
Domain         -> không tham chiếu project khác
```

- Controller gọi service của Application; không thao tác trực tiếp DbContext, repository implementation hoặc SDK của PayOS/Gmail.
- Application điều phối use case và gọi interface trong Abstractions. Request/response/validator nằm trong feature tương ứng.
- Domain giữ entity, value object và quy tắc nghiệp vụ; không dùng HTTP, EF Core hay SDK tích hợp.
- Infrastructure triển khai interface do Application định nghĩa. DbContext, EF Core mapping, migrations và repository implementation thuộc Persistence.
- API tham chiếu Infrastructure để gọi AddInfrastructure tại startup. AddApplication đang đặt tại Api/Extensions để Application không cần thêm dependency DI/hosting chỉ cho bộ khung.
- BackgroundJobs tạo DI scope và gọi Application service; dùng chung quy tắc hết hạn đơn, giải phóng giữ chỗ và xử lý thanh toán với luồng HTTP.

## Quy ước triển khai

Ví dụ đặt hàng, các file sau sẽ được tạo khi triển khai:

```text
Api/Controllers/OrdersController.cs
Application/Features/Orders/IOrderService.cs
Application/Features/Orders/OrderService.cs
Application/Features/Orders/Requests/CreateOrderRequest.cs
Application/Features/Orders/Responses/OrderResponse.cs
Application/Features/Orders/Validators/CreateOrderRequestValidator.cs
Application/Abstractions/Persistence/IOrderRepository.cs
Domain/Entities/Orders/Order.cs
Infrastructure/Persistence/Repositories/OrderRepository.cs
Infrastructure/Persistence/Configurations/OrderConfiguration.cs
```

Interface của application service như IOrderService nằm cùng feature. Interface truy cập tài nguyên ngoài như IOrderRepository, IPaymentGateway, IEmailSender nằm trong Abstractions. Không bắt buộc CQRS, MediatR hoặc một service cho từng bảng dữ liệu.

Quy tắc của một entity nằm cùng entity/value object liên quan. Chỉ thêm Domain Services/Events khi có nghiệp vụ cần dùng. Common chứa kiểu dùng chung như kết quả xử lý hoặc phân trang, không gom nghiệp vụ của các feature vào đây.

## Mở và chạy

Mở AgriSustain.sln trong Visual Studio và chọn AgriSustain.Api làm startup project. TargetFramework net8.0 được cấu hình chung trong Directory.Build.props.

```powershell
dotnet build AgriSustain.sln
dotnet run --project src/AgriSustain.Api
```

Endpoint kiểm tra bộ khung: http://localhost:5179/health.

API hiện có endpoint health; các hàm đăng ký Application và Infrastructure vẫn là điểm mở rộng trống. Chưa triển khai database, EF Core, JWT, PayOS, Gmail, Redis, background jobs hoặc nghiệp vụ. Frontend và tests vẫn là bộ khung, không thuộc bốn layer backend.

Xem [package-map.md](docs/package-map.md) để phân chia nghiệp vụ và đối chiếu package Report 4. Workspace hiện giữ frontend cùng backend; việc tách hai repository theo Report 2 là một thay đổi riêng.
