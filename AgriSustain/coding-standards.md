# QUY TẮC LẬP TRÌNH — AGRISUSTAIN

Phiên bản 2.0 — ngày 22/10/2026.

Áp dụng cho code C#/.NET, hợp đồng API, SQL và việc tích hợp frontend của repository
SEP490_V1.0. Frontend React cần quy ước giao diện riêng; các quy tắc bảo mật, dữ liệu
và API trong tài liệu này áp dụng cho cả hai phía.

Đường dẫn trong tài liệu tính từ thư mục chứa AgriSustain.sln. Bản dùng chung nằm
tại coding-standards.md trong thư mục đó; chỉnh sửa phải được đưa vào Git cùng code.

### Hiện trạng được dùng làm căn cứ

- Một project ứng dụng AgriSustain/AgriSustain.csproj, TargetFramework net8.0,
  bật Nullable và ImplicitUsings. Bốn tầng là folder/namespace bên trong project.
- Ba project test riêng: Unit, Integration và Architecture, cùng reference project chính.
  Test project không phải các tầng ứng dụng.
- SDK trong global.json: 8.0.422, rollForward=latestPatch. Không ghi phiên bản SDK
  toàn máy làm phiên bản bắt buộc của repository.
- EF Core SQL Server; một AgriSustainContext ở Infrastructure/Data; FluentValidation
  cho input của ba UC; JWT cho xác thực; frontend React/Vite ở frontend/.
- Code mới cho UC-ADM-03, UC-ADM-05, UC-RC-02 đã có. Điều này không xác nhận toàn
  bộ nghiệp vụ, schema hoặc các tích hợp đã hoàn chỉnh hay đã được kiểm thử.
- database/Update_DB.sql là bản SQL gộp tạo/nâng schema và nạp demo theo cấu hình.
  Mở/sửa file SQL không đồng nghĩa đã thực thi nó trên SQL Server.

Tài liệu quy định cách viết và sửa code từ hiện trạng trên. Không dùng tài liệu
để khẳng định code hiện có đã tuân thủ mọi quy tắc hoặc để hợp thức hóa lỗi cũ.

## 1. Mức độ áp dụng

- **BẮT BUỘC**: các quy định trong tài liệu, trừ nội dung ghi rõ là khuyến nghị hoặc tùy chọn.
- **KHUYẾN NGHỊ**: ưu tiên áp dụng; được lựa chọn khác khi có lý do kỹ thuật cụ thể.
- Ngoại lệ phải được ghi rõ lý do, phạm vi và ảnh hưởng trong pull request hoặc bàn giao.
  Quyết định kiến trúc đã được thống nhất trong tài liệu này không cần xin duyệt lại từng file.
- Thay đổi convention phải cập nhật tài liệu và cấu hình chung trong cùng pull request.
- Code sinh tự động, migrations và mã của bên thứ ba được miễn quy tắc trình bày; vẫn phải
  được kiểm tra về tính đúng đắn, bảo mật và ảnh hưởng dữ liệu.
- Khi sửa code cũ, áp dụng convention cho phần thay đổi; tách việc format diện rộng thành PR riêng.
- Những lựa chọn như giới hạn 100 ký tự, tên thư mục và Result pattern là chuẩn của nhóm,
  không phải yêu cầu bắt buộc của ngôn ngữ C# hay toàn bộ hệ sinh thái .NET.

## 2. Định dạng và bố cục code

- Dùng **Allman**: `{` và `}` đặt trên dòng riêng, cùng mức thụt lề.
- Mọi block `if`, `else`, `for`, `foreach`, `while`, `do`, `using`, `lock` có thân statement
  phải có `{ }`, kể cả khi chỉ chứa một statement. `using var` được phép.
- Thụt lề bằng **4 dấu cách**, không dùng tab; lưu file UTF-8, LF, có newline cuối file.
- Mỗi dòng code tối đa **100 ký tự**, tính cả thụt lề. Với comment, ưu tiên cùng giới hạn;
  URL dài được giữ nguyên khi cần. Chuỗi không thể ngắt phải giải thích trong PR.
- Khi xuống dòng điều kiện dài, đặt `&&`, `||` ở **đầu dòng tiếp theo**; giữ từng điều kiện
  thành phần liền mạch. Khi lời gọi dài, xuống dòng mỗi đối số; LINQ chain mỗi thao tác một dòng.
- Mỗi statement và mỗi khai báo biến trên một dòng. Không viết nhiều câu lệnh cách nhau bằng `;`.
- Có khoảng trắng sau dấu phẩy, quanh toán tử nhị phân và giữa keyword với `(`.
  Không thêm khoảng trắng ngay trong `()` hoặc giữa tên method và `(`.
- Một dòng trắng giữa các method và giữa các nhóm logic; không chèn nhiều dòng trắng liên tiếp.
- Ưu tiên file-scoped namespace. Một public type chính trong một file; tên file trùng tên type.
- `using` đặt trước namespace, `System` trước, sau đó nhóm còn lại theo thứ tự chữ cái.
  Xóa using không dùng. Implicit usings được phép; global using bổ sung phải dùng chung thực sự.
- Không có wildcard import trong C#; không chuyển nguyên quy tắc import của Java sang đây.
- Auto-property ngắn như `public string Name { get; init; }` được phép trên một dòng.
  Expression-bodied member chỉ dùng cho biểu thức đơn giản; không dùng để nén logic nhiều bước.
- Trong class: constants → fields → constructors → properties → public methods →
  internal/protected methods nếu có → private helpers. Giữ các overload gần nhau.

```csharp
if (user.Status == "Active"
    && !user.IsTemporarilyLocked(clock.GetUtcNow()))
{
    user.Suspend(reason);
}
```

## 3. Đặt tên

Tên trong code dùng tiếng Anh, thể hiện ý nghĩa nghiệp vụ. Không dùng tên viết tắt tự tạo,
tiếng Việt không dấu hoặc tên như `data`, `temp`, `obj`, `manager` khi có tên rõ nghĩa hơn.

| Thành phần | Quy định | Ví dụ |
| --- | --- | --- |
| Project, namespace | PascalCase, có tên sản phẩm và tầng | `AgriSustain.Application.Services` |
| Class, record, struct | PascalCase; danh từ/cụm danh từ | `Order`, `OrderResponse` |
| Interface | `I` + PascalCase | `IOrderRepository`, `IPaymentGateway` |
| Method | PascalCase; động từ/cụm động từ | `CreateOrder`, `CancelOrder` |
| Method bất đồng bộ | Hậu tố `Async` khi trả Task/ValueTask | `CreateOrderAsync` |
| Property | PascalCase | `TotalAmount`, `CreatedAt` |
| Parameter, local variable | camelCase | `customerId`, `pendingOrders` |
| Private instance field | `_camelCase` | `_orderRepository` |
| Private static readonly field | `s_camelCase` | `s_serializerOptions` |
| Constant, kể cả local constant | PascalCase | `MaxPageSize`, `MaxRetryCount` |
| Enum / enum member | PascalCase; tên enum số ít | `OrderStatus.Pending` |
| Boolean | Diễn đạt mệnh đề rõ nghĩa | `IsActive`, `HasPermission`, `CanCancel` |
| Generic parameter | `T` hoặc `T` + ý nghĩa | `T`, `TEntity`, `TResponse` |
| Exception tùy chỉnh | Hậu tố `Exception` | `InvalidOrderStateException` |
| Request / response / validator | Hậu tố theo vai trò | `CreateOrderRequest`, `OrderResponse` |

- Namespace bắt đầu bằng `AgriSustain`, sau đó khớp tầng và thư mục; giữ các tên `DTOs`, `UnitOfWork`
  trong sơ đồ. Ví dụ: `AgriSustain.Application.DTOs.Orders`.
- Class viết `Dto`, `Id`, `Api`, `Url` trong tên: `OrderDto`, `CustomerId`, `ApiOptions`.
- Collection dùng danh từ số nhiều hoặc tên mô tả vai trò: `orders`, `allowedStatuses`,
  `ordersById`. Không bắt buộc hậu tố `List`, `Set`, `Map`, `Array`.
- Không đặt tên gắn với implementation nếu implementation có thể đổi.
- Không shadow field hoặc biến scope ngoài theo cách gây nhầm lẫn. Constructor parameter
  `orderRepository` và field `_orderRepository` là cách dùng hợp lệ.
- Enum lưu trong database hoặc truyền qua API phải có giá trị/serialization ổn định;
  không đổi thứ tự hay đổi tên tùy tiện. Không tạo giá trị `Unknown` nếu domain không cho phép.

## 4. Kiến trúc: bốn layer trong một project

Không tạo bốn project Api/Application/Domain/Infrastructure. Dùng các folder đã có
trong project AgriSustain; namespace tương ứng là AgriSustain.Api,
AgriSustain.Application, AgriSustain.Domain, AgriSustain.Infrastructure.

```text
AgriSustain.sln
AgriSustain/
  AgriSustain.csproj
  Program.cs
  Api/
  Application/
  Domain/
  Infrastructure/
  appsettings.json
  appsettings.Development.json
  appsettings.Checkout.json
frontend/
database/
docs/
tests/
  Unit/
  Integration/
  Architecture/
```

Chiều phụ thuộc mã nguồn được phép:

```text
Api                -> Application; dùng Infrastructure tại composition root/đăng ký DI
Application        -> Domain
Infrastructure     -> Application, Domain
Domain             -> Không phụ thuộc Api, Application, Infrastructure
```

- Controller và authorization handler dùng interface Application; không query DbContext,
  gọi repository implementation hoặc SDK tích hợp trực tiếp.
- Application không dùng EF Core, DbContext, SqlClient, HttpContext, IActionResult,
  IFormFile hoặc kiểu SDK của nhà cung cấp. DI cấp implementation qua interface.
- Domain không I/O: không SQL, HTTP, filesystem, logging hạ tầng hoặc DI container.
- Interface service/repository/UnitOfWork/context/tích hợp đặt tại Application/Interfaces.
  Implementation persistence/tích hợp đặt trong Infrastructure; actor HTTP ở Api/Security.
- Program.cs và Api/UseCaseRegistration.cs là nơi ráp ứng dụng; extension đăng ký
  Infrastructure nằm tại Infrastructure/DependencyInjection.cs. Ngoại lệ composition
  root không áp dụng cho code controller hoặc service nghiệp vụ.
- Vì cùng assembly, compiler và ProjectReference không chặn dependency giữa folder.
  Kiểm tra bằng review và architecture tests theo namespace/source. internal cũng
  có phạm vi assembly, không tạo ranh giới riêng giữa các layer.
- Không tự tách project, chuyển hàng loạt thư mục hoặc tạo DbContext thứ hai khi
  thêm UC. Ba project test có thể reference project chính để kiểm tra code thực tế.
- Luồng chạy Controller -> Service -> Repository implementation không có nghĩa
  Application được phép import namespace Infrastructure.

## 5. Đặt code đúng thư mục

| Thư mục | Trách nhiệm và giới hạn |
| --- | --- |
| `Api/Controllers` | Bind request, lấy danh tính đã xác thực, gọi application service, trả HTTP response. Không truy vấn database hoặc tính nghiệp vụ. |
| `Api/Security` | JWT authentication, policy, role và resource authorization handler. Không tự viết thuật toán mật mã. |
| `Api/Middlewares` | Xử lý lỗi tập trung, correlation/trace và HTTP pipeline. Không chứa quy tắc đặt hàng/thanh toán. |
| `Application/Services` | Điều phối use case, kiểm tra quyền theo tài nguyên và nghiệp vụ xuyên entity, quyết định điểm commit. |
| `Application/Interfaces` | Hợp đồng service, repository, Unit of Work, payment, email, file storage và context cần thiết. |
| `Application/DTOs` | Request/response của use case. Không dùng entity làm request hoặc response công khai. |
| `Application/Mappings` | Chuyển đổi dữ liệu thuần. Không gọi DB, HTTP hoặc chứa quyết định nghiệp vụ. |
| `Application/Validators` | Kiểm tra dữ liệu đầu vào của use case; dùng lại cho HTTP và worker khi phù hợp. |
| `Application/Common` | UseCaseResult<T>, Error, Page<T> của ba UC và contract Result cũ. Không gom mọi helper vào đây. |
| `Domain/Entities` | Identity, trạng thái và hành vi bảo vệ invariant của entity. Không chứa chi tiết lưu trữ/HTTP. |
| `Infrastructure/Data` | AgriSustainContext, partial Fluent API mapping, phân loại lỗi persistence. SQL giao cho người dùng chạy đặt ở database/ tại cấp solution. |
| `Infrastructure/Repositories` | Triển khai truy vấn và thao tác persistence theo hợp đồng. Không quyết định giá, quyền hoặc chuyển trạng thái đơn. |
| `Infrastructure/UnitOfWork` | Triển khai commit và transaction, chia sẻ cùng DbContext với repositories trong một unit of work. |
| `Infrastructure/Payments` | Adapter PayOS/VietQR; gửi request, xác minh chữ ký và chuyển dữ liệu nhà cung cấp thành kiểu nội bộ. |
| `Infrastructure/FileStorage` | Đọc/ghi/xóa file, sinh liên kết truy cập theo chính sách; không quyết định ai được duyệt hồ sơ. |
| `Infrastructure/Email` | Adapter Gmail/API gửi mail, template và xử lý lỗi giao tiếp. |
| `Infrastructure/Auditing` | Ghi lịch sử theo hợp đồng; phân biệt audit nghiệp vụ và log chẩn đoán. |
| `Infrastructure/BackgroundWorkers` | Lịch chạy, scope, cancellation và gọi use case; không sao chép nghiệp vụ của service. |

- Khi nhiều file, nhóm theo nghiệp vụ bên trong package: `Services/Orders`, `DTOs/Orders`,
  `Validators/Orders`. Không tạo song song `Feature/Order` và `Services/Orders` cho cùng mục đích.
- Domain hiện có package `Entities`. Khi cần `Enums`, `ValueObjects` hoặc `Exceptions`,
  bổ sung thư mục đúng vai trò và cập nhật package-map; không đặt chúng vào Application
  rồi để Domain tham chiếu ngược.
- EF configuration nằm tại Infrastructure, ưu tiên Fluent API; không đưa EF attribute vào Domain.
- Nhóm hiện có: Services/Administration, Services/Checkout, DTOs/Administration,
  DTOs/Checkout và Validators tương ứng. Dùng tên nghiệp vụ, không đặt folder theo người viết.
- Chỉ tạo adapter Payments/Email/FileStorage khi có yêu cầu và contract cụ thể;
  folder tồn tại không có nghĩa dịch vụ ngoài đã được tích hợp.

## 6. OOP, SOLID và thiết kế đối tượng

- Field là private; chỉ mở `internal`, `public`, `protected` khi có nhu cầu contract cụ thể.
  Không có public mutable field.
- Entity mới bảo vệ trạng thái qua hành vi như Confirm, Cancel, Reserve, Suspend;
  tránh mở setter nếu không cần cho ORM/contract. Entity scaffold hiện có public setter;
  giữ tương thích, không đổi toàn bộ setter/constructor của model cũ trong một UC.
- Bổ sung hành vi vào partial file: User.AccountActions.cs, Category.Management.cs,
  HarvestBatch.Reservation.cs. Các file này vẫn thuộc cùng entity, không tạo thêm bảng.
- CategorySettings là entity/bảng bổ sung; RetailPricing là lớp tính toán thuần,
  không phải entity. Mỗi bảng mới phải có lý do nghiệp vụ và SQL tương ứng.
- Không sửa file scaffold để nhét nghiệp vụ hoặc scaffold đè các partial thủ công.
- Constructor/factory kiểm tra dependency bắt buộc và dữ liệu cần để tạo object hợp lệ.
  Constructor dành riêng cho ORM có thể là private/protected nếu EF yêu cầu.
- DTO có thể dùng `record`, `init`, `required`; entity có identity không mặc định dùng record.
  C# 12 primary constructor và collection expression được phép khi code dễ đọc.
  `required` không thay thế runtime validation.
- Ưu tiên immutable cho value object và dữ liệu truyền qua các bước xử lý.
  `readonly` chỉ ngăn gán lại field, không tự làm object bên trong bất biến.
- Mỗi class một trách nhiệm chính; method ở một mức trừu tượng. Nhiều dependency không liên
  quan hoặc nhiều nhóm method độc lập là dấu hiệu cần tách class.
- Inheritance chỉ dùng cho quan hệ is-a và phải giữ contract/LSP; ưu tiên composition.
- Interface nhỏ, tập trung. Tạo abstraction ở ranh giới cần thay thế, kiểm thử hoặc đảo chiều
  phụ thuộc; không tạo interface cho mọi class chỉ để đủ hình thức.
- Mở rộng bằng strategy/polymorphism khi có nhiều hành vi thay đổi độc lập. `switch` với enum
  hoặc pattern matching đơn giản được phép; không ép thay mọi `switch` bằng design pattern.
- Giao hành vi cho object chịu trách nhiệm; tránh truy cập sâu graph object để sửa trạng thái.
- Không God Class, circular dependency, mutable global state hoặc singleton giữ dữ liệu request.
- Override dùng keyword `override`; override `Equals` phải có `GetHashCode` nhất quán.
  Nếu overload `==`, phải đảm bảo nhất quán với equality contract và xử lý null.
- Dùng enum/value object cho khái niệm quan trọng khi giúp ngăn dữ liệu sai; không tạo wrapper
  cho mọi primitive một cách máy móc.

## 7. C# và clean code

- Dùng `string`, `int`, `bool`, `decimal` thay cho `String`, `Int32`, `Boolean`, `Decimal`
  trong khai báo thông thường.
- Bật nullable reference types. `T?` phải phản ánh contract có thể thiếu giá trị.
  Không dùng `!` để che warning; ngoại lệ do framework/ORM phải có lý do rõ ràng.
- `var` dùng khi kiểu rõ từ vế phải, ví dụ `var order = new Order(...)`.
  Dùng kiểu tường minh khi kiểu trả về khó nhận biết. Không bắt buộc đổi local collection
  thành interface; ưu tiên abstraction ở public contract và ranh giới module.
- Dùng generic đầy đủ; không dùng `ArrayList`, `Hashtable`, `dynamic` hoặc `object`
  thay generic trừ trường hợp interoperability có lý do.
- Array viết `Order[] orders`. Dùng `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`
  theo nhu cầu thứ tự, duy nhất hoặc tra cứu.
- Dữ liệu chỉ đọc trả về `IReadOnlyList<T>`/`IReadOnlyCollection<T>` khi phù hợp;
  không trả collection nội bộ có thể bị sửa nếu cần bảo vệ invariant.
- Collection không có phần tử trả collection rỗng, không trả null. Một object không tìm thấy
  có thể là `T?` hoặc Result theo contract; không tạo object rỗng để giả lập dữ liệu tồn tại.
- Không lạm dụng `dynamic`, reflection, extension method hoặc operator overload làm code khó hiểu.
- String trong C# dùng `==` để so sánh giá trị được; khi cần chính sách so sánh rõ,
  dùng `string.Equals(..., StringComparison.Ordinal/OrdinalIgnoreCase)`.
  So sánh theo văn hóa dành cho yêu cầu ngôn ngữ hiển thị, không dùng tùy tiện cho identifier.
- `ReferenceEquals` dành cho identity tham chiếu. Không áp dụng quy tắc Java cấm `==` cho string.
- Không magic number/string mang ý nghĩa nghiệp vụ. Constant chỉ cho giá trị cố định;
  giá trị thay đổi theo môi trường dùng cấu hình; giá trị quản trị thay đổi dùng dữ liệu.
- Constant đặt gần phạm vi sử dụng. Không tạo class `Constants` chung chứa mọi loại giá trị.
- Dùng string interpolation cho chuỗi ngắn; dùng `StringBuilder` khi xây chuỗi lặp nhiều lần
  với khối lượng đáng kể. Logging dùng message template, không interpolation.
- Dùng `decimal` cho tiền, xác định currency và quy tắc làm tròn; không dùng `float`/`double`.
  Kiểm tra giới hạn và chuyển đổi đơn vị khi tích hợp nhà cung cấp thanh toán.
- Thời điểm dùng `DateTimeOffset`, lưu/trao đổi UTC; ngày thuần dùng `DateOnly` khi phù hợp.
  Múi giờ nghiệp vụ phải tường minh. Dùng `TimeProvider` qua DI khi logic phụ thuộc thời gian.
  Scaffold hiện còn DateTime cho cột datetime2; diễn giải UTC rõ ràng ở boundary,
  không đổi hàng loạt kiểu entity/cột hoặc gán giờ local thành UTC để che sai lệch.
- Tránh nested if/loop sâu; ưu tiên guard clause và helper có tên rõ nghĩa.
  Khuyến nghị method dưới khoảng 40 dòng logic; vượt mức là tín hiệu review, không chia vụn
  method chỉ để đạt số dòng.
- Không bỏ code chết, biến không dùng hoặc code bị comment-out trong repository.

## 8. Validation và quy tắc nghiệp vụ

- Frontend kiểm tra để hỗ trợ người dùng; backend luôn kiểm tra độc lập.
- API kiểm tra binding, định dạng giao tiếp, giới hạn body, multipart và thông tin HTTP.
- Application validator kiểm tra input use case: bắt buộc, độ dài, khoảng giá trị và quan hệ
  giữa các field. Chuẩn nhóm: dùng FluentValidation khi triển khai; không lặp cùng quy tắc
  trong controller, DataAnnotations và validator.
- Validator có truy vấn async phải được gọi bằng `ValidateAsync`; không dựa vào validation
  pipeline đồng bộ để chạy quy tắc async. Các lối vào HTTP và worker đều phải áp dụng
  validation cần thiết trước khi xử lý use case.
- Application service kiểm tra quyền sở hữu, dữ liệu tham chiếu, trạng thái hiện hành và
  quy tắc phối hợp nhiều entity. Domain bảo vệ invariant khi thay đổi trạng thái.
- Validation tại API không thay thế invariant của Domain; worker và test có thể gọi use case
  mà không đi qua controller.
- Kiểm tra trùng dữ liệu trước khi ghi giúp thông báo sớm; constraint/index ở database
  vẫn phải bảo vệ tính duy nhất khi có request đồng thời.
- Không tự trim/lowercase mọi input. Chuẩn hóa theo contract từng field; không tự thay đổi
  password, mã ký hoặc dữ liệu cần kiểm tra chữ ký.

## 9. Async/await và cancellation

- I/O database, HTTP, file và email dùng API bất đồng bộ khi thư viện hỗ trợ.
- Method thông thường trả `Task`/`Task<T>`; chỉ dùng `ValueTask` khi có lý do đo được hoặc
  contract thư viện yêu cầu. Không dùng `async void`, ngoại trừ event handler yêu cầu của framework.
- Không dùng `.Result`, `.Wait()` hoặc `.GetAwaiter().GetResult()` để chặn async trong luồng request.
- Không dùng `Task.Run` để bọc I/O async trong ASP.NET Core.
- Truyền `CancellationToken cancellationToken` ở cuối danh sách parameter qua các tầng và
  xuống API I/O. API dùng request cancellation; worker dùng stopping token.
- Không nuốt `OperationCanceledException` và biến việc hủy bình thường thành lỗi 500.
  Thao tác đã commit hoặc tác vụ quan trọng sau commit phải có cơ chế phục hồi riêng.
- `Task.WhenAll` chỉ cho tác vụ độc lập và có kiểm soát mức song song.
  Không chạy nhiều thao tác đồng thời trên cùng một `DbContext`.
- Không fire-and-forget trong controller/service bằng `_ = SomeAsync()` khi tác vụ phải hoàn tất.
  Công việc nền quan trọng cần hàng đợi/bản ghi bền vững hoặc outbox theo yêu cầu.
- Không bắt buộc `ConfigureAwait(false)` trên toàn bộ backend ASP.NET Core; chỉ áp dụng
  theo ngữ cảnh thư viện dùng lại và yêu cầu synchronization context.

Việc tránh blocking và sử dụng I/O async phù hợp với
[ASP.NET Core best practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices).

## 10. Dependency injection và quản lý tài nguyên

- Dùng constructor injection. Không `new` repository, DbContext, gateway hoặc service hạ tầng
  bên trong logic nghiệp vụ. `new` entity, DTO, value object là hợp lệ.
- Không gọi `IServiceProvider.GetService` trong service nghiệp vụ. Scope factory chỉ dùng tại
  ranh giới quản lý lifetime như background worker hoặc factory có trách nhiệm rõ ràng.
- DI registration nằm ở composition root hoặc extension đăng ký của từng tầng;
  không gọi `BuildServiceProvider` giữa quá trình đăng ký để lấy dependency.
- Chọn lifetime theo dependency: DbContext/repository/UnitOfWork thường Scoped;
  service dùng các dependency này không được có lifetime dài hơn chúng.
- Singleton phải thread-safe, không giữ user, token, request, DbContext hoặc mutable state
  của một thao tác. Không inject Scoped trực tiếp vào Singleton.
- Background worker tạo scope cho từng lần xử lý hoặc batch có giới hạn; resolve application
  service trong scope rồi dispose scope. Không giữ service scoped suốt đời worker.
- Resource do code tạo và sở hữu phải đóng bằng `using`/`await using` khi phù hợp.
  Không dispose dependency do DI container tạo và sở hữu.
- HTTP tích hợp dùng typed/named client qua `IHttpClientFactory` hoặc client được quản lý
  lifetime đúng cách; không tạo/dispose HttpClient mới cho từng request.

## 11. Database, EF Core, repository và transaction

- EF Core query, configuration và migration class (nếu dùng EF Migrations) thuộc
  Infrastructure. Các SQL script phiên bản/bản gộp hiện tại nằm ở database/ cấp solution.
- Repository interface mô tả nhu cầu use case, không trả `DbSet`, `DbContext`, EF type hoặc
  `IQueryable` ra Application/API. Trả entity, DTO projection hoặc kiểu kết quả đã thống nhất.
- Query đọc ưu tiên projection `Select` đúng field cần dùng và `AsNoTracking` khi không cần
  tracking. Không áp dụng no-tracking cho entity sẽ được cập nhật trong cùng unit of work.
- Lọc, sắp xếp, phân trang trước khi materialize. Không `ToList` toàn bảng rồi lọc trong RAM.
- Endpoint danh sách phải giới hạn số phần tử; chuẩn mặc định `PageSize = 20`, tối đa `100`,
  trừ contract cụ thể. Ba UC hiện tại dùng mặc định 10, tối đa 100 theo DTO/SRS.
  Thứ tự phân trang phải ổn định, có khóa phân định khi cần.
- Tránh N+1 query, lazy loading ngoài kiểm soát và `Include` quá nhiều quan hệ.
  Kiểm tra SQL/query plan khi có dấu hiệu chậm; không đoán hiệu năng chỉ từ LINQ.
- Kiểm tra tồn tại dùng `AnyAsync` khi phù hợp; không đếm toàn bộ chỉ để biết có dữ liệu hay không.
- Một unit of work sử dụng cùng DbContext cho các repository tham gia.
  Repository không tự `SaveChanges` sau từng thao tác; application service quyết định điểm commit
  thông qua `IUnitOfWork`.
- Một `SaveChanges` được EF thực hiện trong transaction khi provider hỗ trợ.
  Chỉ mở transaction tường minh khi cần nhiều bước atomic hoặc yêu cầu isolation cụ thể.
- Không giữ transaction database trong lúc chờ thanh toán/email/file/network.
  Database transaction không rollback được tác động đã gửi tới dịch vụ ngoài.
- Tồn kho, giữ chỗ và chuyển trạng thái thanh toán phải có chiến lược concurrency:
  conditional update, concurrency token/rowversion hoặc locking phù hợp; xử lý conflict rõ ràng.
- Parameter hóa SQL; không nối trực tiếp input vào raw SQL. Tên cột sắp xếp động phải qua allowlist.
- Migration phải được review cùng thay đổi model; kiểm tra data loss, index, precision,
  constraint và kế hoạch chuyển đổi dữ liệu. Không tự chạy migration production ngoài quy trình deploy.
- Không sửa migration đã phát hành/áp dụng trên môi trường dùng chung; tạo file có
  phiên bản mới. Khi đang chuẩn bị bản chưa phát hành, giữ bản gộp và file thành phần
  đồng nhất, ghi rõ trạng thái áp dụng thay vì suy đoán từ tên file.
- Hiện SqlUnitOfWork mở transaction Serializable cho ba UC. Không coi mức isolation
  này là mặc định bắt buộc cho mọi truy vấn; khi thay đổi phải đánh giá khóa, deadlock
  và việc retry. Dispose transaction chưa commit phải rollback.
- Chỉ dùng một AgriSustainContext ở Infrastructure/Data. Mapping thủ công nằm ở
  AgriSustainContext.UseCases.cs; không tạo context song song để né schema không khớp.
- Nullability của entity, cấu hình EF và cột SQL phải tương thích; nullable column
  được thêm cho dữ liệu cũ không có nghĩa có thể bỏ qua contract đọc dữ liệu của EF.
- Không thêm cột dư chỉ để làm hết exception. Ví dụ OrderItem liên kết lô qua
  OrderItemBatchAllocation; mapping trực tiếp OrderItem.HarvestBatchId đã được bỏ.
- Database-generated ID phải lấy sau khi lưu hoặc bằng OUTPUT/SCOPE_IDENTITY;
  không giả định khóa luôn là 1..n, kể cả sau khi transaction rollback.

### 11.1. SQL, seed và chia sẻ database qua Git

- SQL phiên bản, bản gộp và hướng dẫn đặt tại database/ cấp solution; mapping EF
  vẫn ở Infrastructure/Data. Trạng thái chi tiết xem database/README.md.
- Update_DB.sql là file người dùng có thể chạy toàn bộ trong SSMS: tạo DB khi chưa
  có, bổ sung schema khi đã có và nạp demo theo tùy chọn. Không DROP DB hiện có.
- Insert.sql là phần seed riêng. File thành phần được giữ để truy vết; người dùng
  không bắt buộc mở từng migration khi dùng bản gộp.
- Bộ demo hiện nạp 340 dòng vào 54 bảng, có bảng ánh xạ ID và seed marker. Marker
  không phải hệ thống theo dõi phiên bản schema. Không tự đánh dấu thành công để
  bỏ qua dữ liệu chèn chưa hoàn tất hoặc collision.
- Không cập nhật password/role/khóa tạm của toàn bộ User để sửa lỗi đăng nhập seed.
  Hash demo chỉ cấp cho tài khoản demo mới; không dùng tài khoản/mật khẩu demo production.
- Không tạo procedure rỗng, tắt trigger, bỏ FK/index hoặc bịa trạng thái đã thanh
  toán để che lỗi dữ liệu. Fixture thanh toán giả lập phải được ghi rõ là demo.
- Dùng transaction, XACT_ABORT, TRY/CATCH, rollback và giới hạn chờ khóa phù hợp.
  Khi thêm filtered index phải có SET options tương ứng; kiểm tra dữ liệu trùng
  trước khi tạo unique index, không tự xóa hàng trùng để vượt lỗi.
- Bản gộp gồm nhiều batch phải dừng phần sau nếu phần trước lỗi. Nêu rõ phần nào
  commit riêng; lỗi seed không có nghĩa mọi schema thay đổi trước đó đã rollback.
- Ghi rõ bổ sung NULL, giá trị mặc định, computed column, backfill và ảnh hưởng
  dữ liệu cũ. Không âm thầm đổi kiểu cột hoặc suy đoán snapshot lịch sử.
- Git chỉ chia sẻ file đã commit/push. Mỗi máy vẫn cần chạy SQL trên instance
  của mình và cấu hình ConnectionStrings:MyCnn đúng đích. Không sao chép named pipe
  LOCALDB#... giữa các máy; đây là địa chỉ tạm của một instance đang chạy.
- Yêu cầu sửa/gộp file SQL chỉ cho phép sửa file. Chỉ thực thi lên DB khi phạm vi
  công việc bao gồm việc thực thi và đích đã xác định. Nếu đã được chỉ định rõ
  trong phiên làm việc thì không yêu cầu xác nhận lặp lại cho cùng thao tác.
- Trước khi thực thi thay đổi DB phải nêu đích, schema/dữ liệu chịu ảnh hưởng và
  cách giữ dữ liệu; báo chính xác kết quả sau thực thi. Không đổi production hoặc
  xóa dữ liệu chỉ để thuận tiện kiểm thử trên máy phát triển.
- Kiểm tra cú pháp bằng ScriptDom, xuất DDL từ EF hoặc build không xác nhận script
  đã chạy thành công. Không ghi 'đã migrate/seed' khi mới tạo hoặc chỉnh file.
- Không dùng EnsureCreated/EnsureDeleted hoặc tự chạy Update_DB.sql khi API startup
  để thay thế quy trình nâng database đang có dữ liệu.

## 12. API contract và xử lý lỗi

- Controller mỏng: nhận request → gọi service → ánh xạ kết quả ra HTTP.
  Không chứa công thức tính tiền, điều kiện chuyển trạng thái hoặc gọi SDK nhà cung cấp.
- Route dùng resource số nhiều, chữ thường, nhiều từ dùng dấu gạch ngang:
  `/api/orders`, `/api/payment-transactions`. Method HTTP phải phù hợp với hành động.
- JSON dùng camelCase; thời gian theo ISO 8601; tiền, enum và phân trang phải được định nghĩa
  trong contract. Không trả trực tiếp EF navigation graph.
- Ba UC mới dùng UseCaseResult<T>, Error và Page<T> tại Application/Common. Giữ
  contract Result/Result<T> cũ cho bên đang dùng; không tạo thêm một Result cùng tên
  hoặc đổi envelope của API cũ chỉ để tích hợp UC mới.
- Controller mới kế thừa UseCaseControllerBase. Thành công trả DTO; thất bại được
  map từ Error.Kind. Không serialize toàn bộ UseCaseResult làm response mặc định.
- Error.Code phải ổn định. Không trả bool hoặc string tùy ý để mô tả mọi lỗi.
- Service trả Result cho các trường hợp như không tìm thấy, không đủ tồn kho, trạng thái không
  cho phép. Exception dành cho lỗi lập trình, vi phạm invariant hoặc sự cố kỹ thuật.
- Domain không tham chiếu Application.Common, gồm Result và UseCaseResult; dùng kiểu thuần hoặc
  kiểu lỗi Domain riêng. Không catch exception kỹ thuật rồi trả thành lỗi nghiệp vụ giả.
- API map error code sang status bằng cơ chế dùng chung, không kiểm tra nội dung message.
  Application/Domain không trả HTTP status hoặc `IActionResult`.
- Lỗi HTTP dùng `ProblemDetails`; lỗi validation dùng `ValidationProblemDetails`.
  Bổ sung `code` và `traceId` qua extensions khi phù hợp. Thành công trả response DTO;
  không bọc thêm một lớp envelope chung nếu không có yêu cầu contract.

| Trường hợp | HTTP status chuẩn nhóm |
| --- | --- |
| Đọc/cập nhật thành công có response | 200 |
| Tạo resource thành công | 201 và Location khi có endpoint đọc resource |
| Thành công không có response body | 204 |
| Input hoặc validation không hợp lệ | 400 |
| Chưa xác thực hoặc token không hợp lệ | 401 |
| Đã xác thực nhưng thiếu quyền | 403 |
| Resource không tồn tại/không được phép tiết lộ sự tồn tại | 404 theo chính sách endpoint |
| Xung đột trạng thái, trùng dữ liệu hoặc concurrency | 409 |
| Vượt giới hạn tần suất | 429 |
| Lỗi kỹ thuật không được xử lý | 500; không lộ chi tiết nội bộ |

- Hiện ExceptionMiddleware chỉ gắn vào /api/users, /api/categories và /api/checkout.
  Không mô tả middleware này như đã bao phủ mọi API cũ.
- Xử lý exception tập trung tại API middleware/exception handler, tránh try/catch giống nhau
  trong mọi action. Catch `Exception` được chấp nhận tại ranh giới xử lý lỗi toàn cục.
- Không catch rỗng, không nuốt exception, không dùng exception làm luồng điều khiển thông thường.
  Chỉ catch khi có thể phục hồi, chuyển nghĩa hợp lý hoặc thêm context cần thiết.
- Rethrow dùng `throw;`, không `throw ex;`. Chuyển exception phải giữ inner exception khi phù hợp.
- Không trả stack trace, câu SQL, connection string hoặc error message nội bộ ra client.
- Thay đổi API gây breaking change phải cập nhật OpenAPI, frontend và kế hoạch tương thích.

## 13. Authentication, authorization và dữ liệu nhạy cảm

- Authentication xác minh danh tính; authorization kiểm tra quyền thực hiện hành động.
  Có token hợp lệ không đồng nghĩa được truy cập mọi resource.
- JWT phải được xác minh chữ ký, issuer, audience và lifetime theo cấu hình tin cậy.
  Không tin payload chỉ vì decode được token.
- Endpoint bảo vệ phải có policy/authorization thống nhất. Kiểm tra quyền sở hữu/tenant của
  resource tại use case hoặc abstraction phù hợp, áp dụng cho cả worker và lối gọi khác.
- Không tin `UserId`, role, đơn giá, tổng tiền hoặc trạng thái thanh toán gửi từ frontend.
  Danh tính lấy từ principal đã xác thực; giá và số tiền phải tính/đối chiếu phía server.
- Không hard-code hoặc commit password, API key, signing key, token, connection string chứa secret
  hay đường dẫn máy cá nhân. Dùng cấu hình môi trường, user-secrets cho local và secret store phù hợp.
- Dùng Options pattern cho cấu hình theo nhóm; validate cấu hình bắt buộc lúc startup.
- JWT role không thay thế kiểm tra tài khoản/quyền hiện hành. Ba UC mới dùng
  ActiveAccountRequirement/Handler và kiểm tra lại trong service. Không khẳng định
  việc suspend đã thu hồi quyền ở mọi endpoint cũ nếu chưa tích hợp chúng.
- Role mẫu phải khớp contract; admin mới dùng SystemAdmin, policy hiện còn chấp nhận
  Administrator/Admin để tương thích. Không đổi role của tài khoản cũ hàng loạt.
- AuthService hiện hỗ trợ Identity hash và BCrypt cũ, nâng hash khi đăng nhập thành
  công. Không thay bằng plaintext/hash nhanh hoặc đổi cơ chế xác thực ngoài phạm vi UC.
- appsettings.Development.json có thể giữ LocalDB dùng Windows Authentication không
  chứa password. Mỗi máy có thể ghi đè bằng ConnectionStrings__MyCnn hoặc user secrets.
  Không lấy đường dẫn cá nhân hoặc connection string chứa secret làm cấu hình dùng chung.
- Không log Authorization header, JWT, password, refresh token, API secret hoặc toàn bộ hồ sơ KYC.
  Log có dữ liệu cá nhân phải giới hạn/masking theo nhu cầu cụ thể.
- Không dùng hash tự viết hoặc hash nhanh như SHA-256 thuần để lưu password;
  dùng cơ chế password hashing của Identity/thư viện phù hợp.
- CORS dùng allowlist origin theo môi trường. Không tắt xác minh TLS hoặc nới quyền để sửa lỗi tạm.
- Upload phải giới hạn dung lượng, kiểm tra loại/nội dung và tên file; không tin extension hoặc
  Content-Type từ client. Server tạo storage key, ngăn path traversal.
- Tài liệu riêng tư/KYC không đặt ở public URL cố định; việc tải file phải được kiểm tra quyền
  hoặc dùng URL ký có hạn sử dụng theo thiết kế.
- Nếu lưu token bằng cookie, phải thiết kế bảo vệ CSRF và thuộc tính cookie phù hợp;
  không xem CORS là cơ chế authorization hay chống CSRF đầy đủ.

## 14. Thanh toán, email, file storage, worker và audit

- PayOS/Gmail/storage chỉ được truy cập qua interface Application và adapter Infrastructure.
  Kiểu SDK không xuất hiện trong public contract của Application.
- Webhook được nhận tại API; adapter Payments xác minh chữ ký theo tài liệu chính thức của
  nhà cung cấp, sau đó Application xử lý use case. Không tự suy đoán thuật toán ký.
- Không đánh dấu đã thanh toán từ redirect URL hoặc nội dung frontend gửi lên.
  Phải kiểm tra thông tin xác thực từ provider, mã giao dịch, đơn hàng, số tiền và currency.
- Xử lý webhook có **idempotency bền vững**: request trùng không ghi nhận tiền, cập nhật đơn
  hoặc phát sinh side effect lần nữa. Bảo vệ bằng khóa/constraint và transaction phù hợp;
  kiểm tra trong bộ nhớ không đủ khi nhiều instance hoặc process restart.
- Có quy tắc chuyển trạng thái hợp lệ cho callback đến trễ, trùng hoặc sai thứ tự.
  Không ghi đè một trạng thái cuối chỉ vì callback đến sau.
- Retry chỉ cho lỗi tạm thời, có giới hạn, timeout và backoff; không retry thao tác tạo giao dịch,
  gửi mail hoặc ghi dữ liệu nếu chưa xử lý nguy cơ tạo side effect trùng.
- Nếu yêu cầu bảo đảm cập nhật DB và gửi thông báo/tác vụ ngoài nhất quán, dùng outbox hoặc
  cơ chế lưu tác vụ bền vững. Không dùng một database transaction để giả lập tính atomic với API ngoài.
- Worker gọi lại application service, tạo scope đúng lifetime, hỗ trợ shutdown/cancellation.
  Chạy nhiều instance phải có cơ chế claim công việc/chống xử lý trùng khi cần.
- Job quan trọng cần lưu trạng thái, số lần thử và kết quả; không chỉ dựa vào timer trong RAM.
- Worker hiện có ReservationExpiryWorker gọi ReservationExpiryService mỗi 30 giây,
  tối đa 25 đơn PendingPayment bán lẻ có quote mỗi lượt. Đây là implementation hiện
  tại, không phải bằng chứng scheduler bền vững/outbox hoặc retry có backoff đã có.
- Checkout Quote chỉ lưu bản tính giá; Place mới revalidate và giữ kho/voucher/điểm
  trong transaction. RequestKey và unique quote index bảo vệ retry qua restart;
  sessionStorage phía frontend không thay thế cơ chế này trên server.
- PendingPayment không phải Paid. PayOS/VietQR trong input và PaymentPath trả về
  chỉ là contract bàn giao khi module thanh toán thực tế chưa được tích hợp.
- Audit hành động quan trọng phải có actor, thời điểm, resource, hành động, kết quả và correlation
  phù hợp. Phân biệt actor hệ thống/webhook với người dùng; không gán giả user.
- Bản ghi audit bắt buộc cùng thay đổi nghiệp vụ phải được thiết kế cùng transaction/outbox;
  một dòng log thành công không thay thế được audit bền vững.

## 15. Logging, comment và tài liệu

- Logging ứng dụng dùng `ILogger<T>` với structured logging; không dùng `Console.WriteLine`,
  `Debug.WriteLine` hoặc `Exception.ToString()` làm cơ chế logging production.
- Dùng template cố định và truyền exception đúng overload:

```csharp
logger.LogWarning(
    "Payment status mismatch for order {OrderId}",
    orderId);

logger.LogError(
    exception,
    "Payment reconciliation failed for order {OrderId}",
    orderId);
```

- Dùng đúng level: Debug cho chẩn đoán chi tiết; Information cho sự kiện vận hành;
  Warning cho tình huống bất thường có thể xử lý; Error cho thao tác thất bại;
  Critical cho sự cố nghiêm trọng cấp hệ thống.
- Không log cùng exception nhiều lần ở mọi tầng. Tầng xử lý cuối ghi log với context cần thiết.
- Comment trong code dùng tiếng Anh, ngắn gọn, giải thích lý do/quyết định khó suy ra.
  Không viết comment chỉ lặp lại tên method hoặc từng dòng code.
- XML documentation dùng cho contract public có ràng buộc, side effect, exception hoặc nullability
  cần giải thích; không bắt buộc viết mô tả vô nghĩa cho mọi getter/DTO field.
- TODO phải có nội dung cụ thể và issue/task hoặc người phụ trách; không để TODO thay cho xử lý lỗi.
- README và tài liệu kiến trúc của nhóm có thể dùng tiếng Việt. Nội dung hiển thị cho người dùng
  thống nhất ngôn ngữ sản phẩm, tập trung ở resource/template khi cần; không dùng error message làm code.

## 16. Testing và code review

- Unit tests đặt dưới `tests/Unit`: tập trung Domain, tính tiền, chuyển trạng thái và use case.
- Integration tests đặt dưới `tests/Integration`: EF/SQL Server, transaction, API pipeline,
  authorization và adapter contract khi cần. Không gọi thanh toán thật trong CI thông thường.
- Architecture tests đặt dưới tests/Architecture: kiểm tra dependency theo
  namespace/source và abstraction constructor trong project ứng dụng duy nhất.
- Cả ba nhóm hiện đã có test project trong solution. Có file test hoặc test project
  không đồng nghĩa test đã chạy hoặc bao phủ toàn bộ code. Architecture test đang
  loại trừ AuthService cũ ở phần kiểm tra Application; không coi đó là chứng nhận
  toàn bộ Application đã tách persistence đúng tầng.
- Integration test dùng EnsureCreated trên DB tạm chỉ kiểm tra schema sinh từ model;
  không xác minh migration nâng DB cũ, trigger, procedure, view hoặc seed SQL thực tế.
- Thực hiện kiểm tra phù hợp phạm vi/yêu cầu công việc. Nếu người dùng hoãn test,
  ghi rõ chưa chạy và rủi ro chưa xác minh; không tự tuyên bố đạt test hoặc thay đổi
  database thật để chứng minh code chạy. Hoãn test một lần không xóa yêu cầu chất
  lượng của lần phát hành.
- Chuẩn test khi triển khai: xUnit, tên `MethodName_Scenario_ExpectedBehavior`, bố cục
  Arrange–Act–Assert. Test độc lập, kết quả ổn định, không phụ thuộc thứ tự thực thi.
- Test thay đổi nghiệp vụ phải gồm luồng chính và các biên có rủi ro. Thanh toán cần kiểm tra
  callback trùng/sai số tiền/sai chữ ký; tồn kho cần kiểm tra cạnh tranh;
  authorization cần kiểm tra truy cập dữ liệu của người khác.
- Không kiểm thử đơn thuần getter/setter hoặc mock lại chính implementation.
  Chọn test chứng minh hành vi và ngăn lỗi có ý nghĩa.
- Không dùng EF InMemory để kết luận SQL Server translation, constraint hoặc transaction chạy đúng.
  Những hành vi này cần integration test với SQL Server hoặc môi trường tương đương đủ khả năng.
- Không đặt ngưỡng coverage tùy tiện thay cho review chất lượng; ưu tiên các nhánh nghiệp vụ quan trọng.
- Mỗi PR tập trung một thay đổi, giải thích vấn đề, hành vi sau sửa, cách kiểm thử và ảnh hưởng dữ liệu/API.
  Cần ít nhất một reviewer khác tác giả trước khi merge vào nhánh bảo vệ.
- Lỗi nghiệp vụ, bảo mật, kiến trúc hoặc mất dữ liệu là lý do chặn merge. Khác biệt sở thích
  đã được convention quyết định không được biến thành tranh luận lặp lại trong từng PR.

## 17. Tooling và xác minh theo hiện trạng

- Dùng SDK theo global.json và TargetFramework trong project; không tự đổi SDK,
  EF provider hoặc phiên bản ngôn ngữ để sửa lỗi build chưa rõ nguyên nhân.
- Dependency mới phải có mục đích, phiên bản cụ thể và thay đổi csproj tương ứng.
  FluentValidation hiện dùng 11.11.0; không nâng package như một phần format tài liệu.
- .editorconfig đã tồn tại ở cấp solution, hiện cấu hình UTF-8, LF, 4 dấu cách,
  Allman, file-scoped namespace và đánh dấu scaffold DbContext là generated.
  Nó chưa thực thi toàn bộ quy định trong tài liệu này, gồm giới hạn 100 ký tự.
- Không thay .editorconfig toàn repository bằng mẫu chưa đối chiếu baseline.
  Khuyến nghị tăng analyzer/enforcement theo phạm vi được thống nhất; không bật
  warning-as-error diện rộng rồi sửa code cũ không liên quan để vượt build.
- dotnet format không tự bảo đảm mọi dòng dưới 100 ký tự. Format toàn solution có
  thể đụng legacy/scaffold; ưu tiên file thay đổi và kiểm tra diff.
- Có test project, Playwright script hoặc package Swagger không chứng minh CI,
  E2E, OpenAPI endpoint hoặc payment integration đã được cấu hình/chạy thành công.
- Build ứng dụng chính từ thư mục chứa solution:

```powershell
dotnet restore AgriSustain/AgriSustain.csproj
dotnet build AgriSustain/AgriSustain.csproj -c Release --no-restore
```

Khi phạm vi công việc bao gồm test:

```powershell
dotnet restore AgriSustain.sln
dotnet build AgriSustain.sln -c Release --no-restore
dotnet test AgriSustain.sln -c Release --no-build
```

- SQL integration test chỉ chạy trên DB tạm có tên nhận diện riêng; kiểm tra đích
  trước khi xóa. Không dùng DB AgriSustain đang có dữ liệu làm DB test.
- Frontend hiện dùng npm/Vite. Từ frontend/: npm ci, npm run build; npm run dev
  phục vụ cổng 5173, proxy /api về backend 5179. Production cần reverse proxy riêng.
- Khi cần E2E, xem contract và dữ liệu test hiện tại trước khi chạy npm run test:e2e;
  test demo cũ không chứng minh ba UC SQL mới chạy đúng.
- Báo cáo bàn giao tách: file đã sửa; build đã chạy; test đã chạy/chưa chạy; SQL mới
  chỉnh/chưa thực thi/đã thực thi trên DB nào; giới hạn chưa xác nhận.
- Không lấy HTTP 200 tại / (Hello World), build qua hoặc worker đọc 0 đơn thành
  công làm bằng chứng checkout/hoàn giữ chỗ/thanh toán hoạt động với dữ liệu thật.

## 18. Checklist trước khi tạo/duyệt pull request

- [ ] File đúng folder/namespace; code mới giữ chiều phụ thuộc bốn layer trong một project.
- [ ] Controller không chứa nghiệp vụ; Domain không I/O; mapping không truy vấn dữ liệu.
- [ ] Tên, braces, khoảng trắng và giới hạn dòng phù hợp; không có code/import thừa.
- [ ] Nullable contract rõ, không dùng `!` để giấu lỗi; collection không trả null.
- [ ] Validation, ownership/resource authorization và invariant được bảo vệ đúng nơi.
- [ ] Async/cancellation được truyền; không blocking, fire-and-forget hoặc DbContext dùng đồng thời.
- [ ] Query lấy đúng dữ liệu, phân trang hợp lý; commit/transaction/concurrency có chủ đích.
- [ ] Status và error contract thống nhất; exception/log không làm lộ dữ liệu nhạy cảm.
- [ ] Thanh toán/webhook/retry chống side effect trùng nếu phần thay đổi có liên quan.
- [ ] Test chứng minh hành vi/rủi ro thay đổi; CI có chạy test thực sự khi đã có test project.
- [ ] API, model, partial mapping, SQL thành phần/bản gộp, cấu hình và tài liệu liên quan khớp nhau.
- [ ] Phân biệt sửa file SQL với thực thi DB; seed không ghi đè tài khoản/dữ liệu cũ ngoài phạm vi.
- [ ] Ngoại lệ convention có lý do, phạm vi và ảnh hưởng được ghi rõ để review.

## Phụ lục A. Các thành phần hiện có để tham chiếu

| Nhóm | Thành phần |
| --- | --- |
| Entity/hành vi mới | User.AccountActions, Category.Management, HarvestBatch.Reservation, CategorySettings, RetailPricing |
| Application chung | UseCaseResult<T>, Error, Page<T>, IActorContext, IUnitOfWork, ITransaction |
| Nghiệp vụ | AdministrationService, CheckoutService, ReservationExpiryService |
| Persistence | AgriSustainContext, AgriSustainContext.UseCases, AdministrationRepository, CheckoutRepository, SqlUnitOfWork |
| HTTP | UsersController, CategoriesController, CheckoutController, UseCaseControllerBase, ExceptionMiddleware |
| Quyền | ActorContext, ActiveAccountRequirement, ActiveAccountHandler |
| Ráp ứng dụng | Program.cs, Api/UseCaseRegistration.cs, Infrastructure/DependencyInjection.cs |
| Frontend | api/threeUseCases.js, UsersScreen, CategoriesScreen, CheckoutScreen |
| Database | database/Update_DB.sql, Insert.sql, các migration SQL và README.md |

Order, SellerOrder, OrderItem, RetailCheckoutQuote, RewardPointEntry, Voucher và các
entity scaffold khác đã có trước các partial hành vi mới. Di chuyển DbContext
không biến nó thành context mới hoặc bắt buộc chuyển đổi mọi entity.

## Phụ lục B. Các điểm chưa được coi là hoàn tất

- Một số code cũ còn truy cập EF trực tiếp ở Application/Controller. Khi sửa UC
  liên quan cần nêu rõ; không sao chép cách đó sang code mới hoặc refactor diện rộng.
- Code hiện tại còn vị trí chưa đạt toàn bộ quy tắc trình bày, nullable và tách
  trách nhiệm. Giữ chuẩn khi sửa phần liên quan; không sửa chuẩn để hợp thức hóa lỗi.
- Areas đang rỗng và chính sách đổi điểm đang thiếu trong cấu hình checkout mặc
  định. Cần dữ liệu cấu hình hợp lệ trước khi dùng checkout đầy đủ; không tự bịa phí.
- Tích hợp cổng thanh toán/callback, CI và phạm vi bao phủ test phải được xác minh
  riêng. Có PaymentPath hoặc package SDK không chứng minh tích hợp đã hoạt động.
- Schema compatibility thêm bảng/cột không khôi phục các procedure/trigger/view
  nghiệp vụ của một schema khác, không tự backfill lịch sử hoặc đổi kiểu cột cũ.
- Cú pháp SQL đã kiểm tra không đồng nghĩa đã chạy bản gộp/seed trên SQL Server.
  Trạng thái áp dụng phải được ghi theo từng lần bàn giao, không suy ra từ tài liệu.

Tài liệu này chỉ cập nhật quy tắc và mô tả hiện trạng. Việc viết lại tài liệu không
tự sửa code, thay đổi cấu hình build, thực thi SQL hoặc chạy test.
