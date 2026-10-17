# Ánh xạ Clean Architecture với Report 4

Backend có bốn project API, Application, Domain và Infrastructure. Cấu trúc giữ hướng phụ thuộc của package diagram và gom Application theo nghiệp vụ. Report 4 chưa được sửa trong thay đổi bộ khung này.

## Ánh xạ package

Các đường dẫn tính từ src/.

| Package trong Report 4 | Vị trí trong cấu trúc mới |
| --- | --- |
| API Controllers | AgriSustain.Api/Controllers |
| API Security | Api/Authorization; cấu hình authentication tại startup; triển khai token/hash tại Infrastructure/Identity |
| API Request Pipeline | AgriSustain.Api/Middleware |
| API Composition Root | AgriSustain.Api/Program.cs và Extensions |
| Application UseCase | AgriSustain.Application/Features/{Feature} |
| Application Ports/Interfaces | Application/Abstractions; interface application service nằm cùng feature |
| Application DTOs & Validation | Requests, Responses, Validators bên trong từng feature |
| Domain Entities & Value Objects | AgriSustain.Domain/Entities và ValueObjects |
| Domain Rules & Types | Hành vi entity/value object, Enums, Exceptions; thêm domain service khi cần |
| Infrastructure Persistence | AgriSustain.Infrastructure/Persistence |
| Infrastructure Payment Adapter | AgriSustain.Infrastructure/Payments/PayOS |
| Infrastructure Email & Media Adapters | AgriSustain.Infrastructure/Email/Gmail và Storage |
| Infrastructure Operational Services | Infrastructure/Identity, Caching, Auditing, Storage; cấu hình HTTP/logging ở API khi phù hợp |
| Infrastructure Hosted Background Workers | AgriSustain.Infrastructure/BackgroundJobs |
| Infrastructure DI registration | AgriSustain.Infrastructure/DependencyInjection.cs |

## Phân chia feature

Đây là ranh giới trách nhiệm đề xuất. Một use case có thể phối hợp nhiều feature; không chia backend theo vai trò người dùng hoặc từng màn hình.

| Feature Application | Trách nhiệm |
| --- | --- |
| Authentication | Đăng ký, đăng nhập, OTP, đổi/đặt lại mật khẩu, refresh/revoke session |
| Profiles | Hồ sơ cá nhân, nông trại, doanh nghiệp và địa chỉ giao hàng |
| Catalog | Sản phẩm, danh mục, duyệt listing, marketplace, tìm nhà cung cấp và đánh giá sản phẩm |
| Farming | Mùa vụ, nhật ký canh tác, lô thu hoạch, nhập kho và điều chỉnh tồn kho |
| Traceability | Cấp/thu hồi QR và tra cứu nguồn gốc công khai theo lô |
| Orders | Giỏ hàng, checkout bán lẻ/bán sỉ, tách SellerOrder, giữ/phân bổ hàng, giao nhận và lịch sử trạng thái |
| PreOrders | Chiến dịch, giữ năng lực dự kiến, cam kết trả trước và phân bổ hàng sau thu hoạch |
| Contracts | Đề xuất, phiên bản, ký hợp đồng, milestone, đối soát giao nhận, hóa đơn và chấm dứt |
| Payments | Khởi tạo thanh toán, tiếp nhận kết quả đã xác minh, áp dụng kết quả và đối soát tự động |
| Subscriptions | Gói thành viên, quyền lợi, đăng ký/gia hạn và trạng thái hiệu lực |
| Verification | KYC, xét duyệt chứng chỉ, thu hồi và theo dõi hết hạn chứng chỉ |
| Promotions | Voucher, điều kiện áp dụng, hạn mức và bản ghi sử dụng |
| Disputes | Khiếu nại, bằng chứng, phân công xử lý và quyết định giải quyết |
| Finance | Đối soát thủ công, hoàn tiền được duyệt, payout, hoa hồng và báo cáo tài chính |
| PriceBenchmarks | Giá tham chiếu vùng và dữ liệu dùng cho công thức giá hợp đồng |
| Notifications | Soạn/lưu thông báo, trạng thái gửi và retry; gọi abstraction để gửi email |

Catalog quản lý duyệt sản phẩm và đánh giá nên không tạo thêm feature Moderation trùng trách nhiệm. Dashboard lấy dữ liệu qua application service/query tương ứng; quyền truy cập vẫn theo SRS.

Payments quản lý lifecycle thanh toán và kết quả gateway. Finance quản lý quyền phê duyệt và nghiệp vụ tài chính của nhân viên. Hai luồng phải dùng chung xử lý chuyển trạng thái, không ghi nhận tiền hoặc hoàn tiền hai lần.

## Phân chia abstraction

Tên dưới đây là ví dụ dự kiến; chỉ tạo interface khi use case cần gọi nó.

| Thư mục Abstractions | Ví dụ interface |
| --- | --- |
| Persistence | IOrderRepository, IHarvestBatchRepository, IUnitOfWork |
| Identity | ICurrentUser, IPasswordHasher, ITokenProvider, ISessionStore |
| Payments | IPaymentGateway |
| Notifications | IEmailSender |
| Storage | IFileStorage |
| Caching | ICacheService |
| Auditing | IAuditWriter |
| Reporting | IReportExporter |

IOrderService thuộc Features/Orders, còn IPaymentGateway thuộc Abstractions/Payments. Phân biệt service điều phối thanh toán ở Application và adapter gọi nhà cung cấp ở Infrastructure.

ICurrentUser là abstraction của Application. Nếu implementation đọc HttpContext thì đặt tại API; không truyền HttpContext hoặc ClaimsPrincipal vào Domain.

## Nhóm entity Domain

Các entity dưới đây là ví dụ dự kiến, chưa được triển khai.

| Nhóm entity | Thực thể |
| --- | --- |
| Accounts | User, FarmerProfile, WholesaleBuyerProfile, DeliveryAddress |
| Catalog | Product, Category, ProductImage, RegionalPriceBenchmark |
| Farming | CropSeason, CultivationLog, HarvestBatch, InventoryMovement, PreOrderCampaign, TraceabilityQRCode, FarmCertificate |
| Orders | Cart, CartItem, Order, SellerOrder, OrderItem, OrderItemBatchAllocation, Shipment, ShipmentItem, OrderStatusHistory |
| Contracts | B2BContract, ContractVersion, ContractLine, ContractMilestone, ContractSignature, ContractTerminationRequest |
| Billing | B2BSubscriptionPackage, B2BSubscription, Invoice, InvoiceLine, PaymentTransaction, RefundTransaction, CommissionPolicy, FarmerPayout, PayoutItem |
| Engagement | ProductReview, Voucher, VoucherRedemption, DisputeTicket, DisputeEvidence, Notification, AuditLog |

Nhóm entity giúp tìm file; không phải project hay ranh giới transaction. Khi triển khai cần xác định aggregate và transaction theo quy tắc nghiệp vụ. Domain dùng kiểu C# thuần; cấu hình database nằm trong Infrastructure.

## Phụ thuộc và runtime

```text
Api -> Application
Api -> Infrastructure              (startup/DI)
Application -> Domain
Infrastructure -> Application
Infrastructure -> Domain
Domain -> không project nào khác
```

Không tạo Application -> Infrastructure hoặc Domain -> Application/Infrastructure/Api. Controller dùng application service; background job tạo scope rồi gọi application service.

PayOS gửi webhook vào API, adapter xác minh dữ liệu theo nhà cung cấp, Application xử lý kết quả nghiệp vụ. Application không gọi trực tiếp SDK hoặc nhận kiểu dữ liệu riêng của PayOS.

DTO và validator là thành phần logic trong Application; không bắt buộc có thư mục DTOs và Validation toàn cục.

## Thay đổi so với bộ khung cũ

| Cũ | Mới |
| --- | --- |
| Application/UseCases + DTOs + Validation | Application/Features |
| Application/Ports | Application/Abstractions |
| Api/CompositionRoot | Api/Extensions và Program.cs |
| Api/RequestPipeline/Middleware | Api/Middleware |
| Api/Security/Authorization và Policies | Api/Authorization |
| Domain/Types/Enums, Types/Exceptions | Domain/Enums, Exceptions |
| Domain/Entities/Ordering | Domain/Entities/Orders |
| Infrastructure/Adapters | Infrastructure/Payments, Email, Storage |
| Infrastructure/OperationalServices | Infrastructure/Identity, Caching, Auditing, Storage |
| Infrastructure/HostedBackgroundWorkers | Infrastructure/BackgroundJobs |
| Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs | Infrastructure/DependencyInjection.cs |

Các nhánh Rules, Services, Events, Filters và nhóm job rỗng chưa có nhu cầu cụ thể được bỏ khỏi bộ khung. Bổ sung khi có code tương ứng.
