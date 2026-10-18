# Ánh xạ package diagram sang thư mục

Nguồn: [Package_Diagram.drawio](https://app.diagrams.net/#G1mJnzsACg-bGQhl9gV-hjsKAF_qAYStPE), trang Page-1, ID 4hAH4Tu0OPYzQcTq1O-Q. Cấu trúc backend phản ánh 18 package con nhìn thấy trong sơ đồ.

## API Layer

| Package | Thư mục từ src/ | Nội dung dự kiến |
| --- | --- | --- |
| Controllers | AgriSustain.Api/Controllers | REST controller, endpoint webhook |
| Security | AgriSustain.Api/Security | JWT authentication, policy/role authorization, resource access handler |
| Middlewares | AgriSustain.Api/Middlewares | Exception handling, request logging và HTTP pipeline |

Program.cs là composition root, đúng chú thích DI registration (Program.cs) của sơ đồ. API gọi AddInfrastructure; các service Application, mapping và validator được đăng ký tại đây khi có implementation. Properties/launchSettings.json là cấu hình chạy của .NET.

## Application Layer

| Package | Thư mục từ src/ | Nội dung dự kiến |
| --- | --- | --- |
| Services | AgriSustain.Application/Services | OrderService, ContractService, PaymentService và các use case khác |
| Interfaces | AgriSustain.Application/Interfaces | IOrderService, IOrderRepository, IUnitOfWork, IPaymentGateway, IEmailService, IFileStorageService |
| DTOs | AgriSustain.Application/DTOs | CreateOrderRequest, OrderResponse |
| Mappings | AgriSustain.Application/Mappings | OrderMappings và các phép chuyển đổi entity/DTO |
| Validators | AgriSustain.Application/Validators | CreateOrderRequestValidator |
| Common | AgriSustain.Application/Common | Kiểu kết quả xử lý, phân trang và thành phần dùng chung |

Services điều phối nghiệp vụ, gọi Interfaces và Domain. Application không dùng DbContext, HTTP context hoặc SDK của nhà cung cấp. Mappings không phải nơi truy vấn database hoặc gọi dịch vụ ngoài. Validators kiểm tra đầu vào; các ràng buộc cốt lõi vẫn được bảo vệ trong Domain và transaction nghiệp vụ.

Thư mục được chia theo loại thành phần, thay cho Features/Abstractions của phiên bản trước. Interface application service và interface tích hợp đều thuộc Interfaces. Khi có nhiều file, có thể thêm nhóm nghiệp vụ bên trong Services, DTOs, Mappings hoặc Validators.

## Domain Layer

| Package | Thư mục từ src/ | Nội dung dự kiến |
| --- | --- | --- |
| Entities | AgriSustain.Domain/Entities | User, Product, CropSeason, HarvestBatch, Order, SellerOrder, B2BContract, PaymentTransaction và các thực thể nghiệp vụ |

Domain không tham chiếu API, Application hay Infrastructure. Các entity giữ hành vi bảo vệ quy tắc nghiệp vụ và dùng kiểu C# thuần. EF Core configuration nằm trong Infrastructure/Data.

## Infrastructure Layer

| Package | Thư mục từ src/ | Nội dung dự kiến |
| --- | --- | --- |
| Data | AgriSustain.Infrastructure/Data | AppDbContext, entity configuration, migrations, seed |
| Repositories | AgriSustain.Infrastructure/Repositories | OrderRepository, HarvestBatchRepository |
| UnitOfWork | AgriSustain.Infrastructure/UnitOfWork | Triển khai IUnitOfWork và transaction |
| Payments | AgriSustain.Infrastructure/Payments | PayOsPaymentGateway, xác minh dữ liệu webhook theo nhà cung cấp |
| FileStorage | AgriSustain.Infrastructure/FileStorage | Lưu ảnh, hồ sơ KYC và chứng chỉ theo quyền truy cập |
| Email | AgriSustain.Infrastructure/Email | GmailEmailService |
| Auditing | AgriSustain.Infrastructure/Auditing | AuditService |
| BackgroundWorkers | AgriSustain.Infrastructure/BackgroundWorkers | PaymentReconciliationWorker, OrderExpiryWorker, NotificationWorker |

Tên lớp trong bảng là ví dụ, chưa phải code có sẵn. DependencyInjection.cs chứa extension AddInfrastructure để API đăng ký implementation. Các package Infrastructure phụ thuộc interface do Application định nghĩa và dùng entity Domain.

BackgroundWorkers tạo DI scope khi gọi application service. Quy tắc hết hạn đơn, xử lý thanh toán và giải phóng giữ chỗ cần dùng chung với luồng HTTP.

## Các mũi tên trong sơ đồ

| Quan hệ | Ánh xạ trong code |
| --- | --- |
| React SPA -> API | HTTPS/JSON và Bearer JWT khi triển khai |
| API -> Application (uses) | Controller gọi service qua interface |
| API -> Infrastructure (DI registration) | Program.cs gọi AddInfrastructure |
| Application -> Domain (uses) | Services xử lý entity và quy tắc nghiệp vụ |
| Infrastructure -> Application (depends on interfaces) | Implementation thực hiện interface |
| Infrastructure -> Domain (uses) | Data/repository làm việc với entity |
| Data -> SQL Server | EF Core/SQL Server khi triển khai |
| Payments -> PayOS/VietQR | Adapter thanh toán khi triển khai |
| Email -> Google Gmail API | Adapter gửi email khi triển khai |

Các quan hệ giao tiếp ngoài không phải ProjectReference. Các tham chiếu project vẫn là:

```text
Api            -> Application, Infrastructure
Application    -> Domain
Infrastructure -> Application, Domain
Domain         -> không project nào khác
```

## Tình trạng bộ khung

18 thư mục package có .gitkeep để xuất hiện trong Git. Chưa có controller nghiệp vụ, entity, service, mapping, validator, repository, Unit of Work hay tích hợp thật. Không thêm thư viện chỉ để tạo thư mục.

Frontend và tests nằm ngoài bốn layer backend. Thay đổi này cập nhật mã nguồn và tài liệu cấu trúc trong workspace, không chỉnh sửa sơ đồ gốc trên Google Drive.
