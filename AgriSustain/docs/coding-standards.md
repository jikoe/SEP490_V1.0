# QUY TẮC LẬP TRÌNH C#/.NET — AGRISUSTAIN

Phiên bản 1.0 — ngày 18/10/2026.

Phạm vi: backend ASP.NET Core, Application, Domain, Infrastructure và automated tests.
Frontend React cần convention riêng; các hợp đồng API trong tài liệu này áp dụng cho cả hai phía.

Tài liệu được xây dựng theo Package_Diagram.drawio, trang Page-1 và cấu trúc solution hiện có.
Solution hiện dùng .NET 8, bật nullable reference types và implicit usings.
Các tên service, DTO và repository bên dưới là quy ước cho code triển khai, không khẳng định
các thành phần đó đã tồn tại. Bộ khung hiện chưa triển khai nghiệp vụ và các tích hợp thực tế.

## 1. Mức độ áp dụng

- **BẮT BUỘC**: các quy định trong tài liệu, trừ nội dung ghi rõ là khuyến nghị hoặc tùy chọn.
- **KHUYẾN NGHỊ**: ưu tiên áp dụng; được lựa chọn khác khi có lý do kỹ thuật cụ thể.
- Ngoại lệ với quy định bắt buộc phải nêu lý do trong pull request và được reviewer chấp thuận.
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
if (order.IsPending
    && order.CustomerId == customerId
    && order.ExpiresAt > currentTime)
{
    order.Confirm();
}
```

Allman, thụt lề và mỗi statement một dòng tương thích với
[hướng dẫn trình bày C# của Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

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

- Namespace khớp project và đường dẫn thư mục; giữ nguyên các package như `DTOs`, `UnitOfWork`
  trong sơ đồ. Ví dụ: `AgriSustain.Application.DTOs.Orders`.
- Class viết `Dto`, `Id`, `Api`, `Url` trong tên: `OrderDto`, `CustomerId`, `ApiOptions`.
- Collection dùng danh từ số nhiều hoặc tên mô tả vai trò: `orders`, `allowedStatuses`,
  `ordersById`. Không bắt buộc hậu tố `List`, `Set`, `Map`, `Array`.
- Không đặt tên gắn với implementation nếu implementation có thể đổi.
- Không shadow field hoặc biến scope ngoài theo cách gây nhầm lẫn. Constructor parameter
  `orderRepository` và field `_orderRepository` là cách dùng hợp lệ.
- Enum lưu trong database hoặc truyền qua API phải có giá trị/serialization ổn định;
  không đổi thứ tự hay đổi tên tùy tiện. Không tạo giá trị `Unknown` nếu domain không cho phép.

PascalCase cho method/constant và tiền tố `I` cho interface phù hợp với
[quy ước định danh C#](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names).

## 4. Kiến trúc và chiều phụ thuộc

Mỗi tầng là một project riêng. `A -> B` nghĩa là project A được phép tham chiếu project B.

```text
AgriSustain.Api            -> AgriSustain.Application, AgriSustain.Infrastructure
AgriSustain.Application    -> AgriSustain.Domain
AgriSustain.Infrastructure -> AgriSustain.Application, AgriSustain.Domain
AgriSustain.Domain         -> Không tham chiếu các project còn lại
```

- `Program.cs` là composition root: cấu hình ứng dụng, middleware, endpoint và đăng ký DI.
- API tham chiếu Infrastructure để gọi đăng ký như `AddInfrastructure`. Controller và
  authorization handler sử dụng abstraction; không gọi implementation Infrastructure trực tiếp.
- Application không phụ thuộc `DbContext`, EF Core, `HttpContext`, `IActionResult`,
  `IFormFile`, PayOS SDK, Gmail SDK hoặc implementation lưu file.
- Domain không I/O; không truy cập database, HTTP, filesystem, DI container hoặc logging hạ tầng.
- Interface cho repository, Unit of Work và tích hợp nằm ở `Application/Interfaces`.
  Implementation tương ứng nằm trong Infrastructure.
- Không có circular dependency, service locator trong nghiệp vụ hoặc gọi ngược controller từ service.
- Runtime có thể gọi implementation Infrastructure thông qua DI; điều này không làm thay đổi
  chiều ProjectReference. Không nhầm luồng thực thi với chiều phụ thuộc mã nguồn.
- Không tự đổi bốn project thành kiến trúc khác. Thay đổi ranh giới tầng phải cập nhật sơ đồ,
  tài liệu và có review kiến trúc.

Việc giới hạn tham chiếu Infrastructure tại composition root phù hợp với
[hướng dẫn Clean Architecture của Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/modern-web-apps-azure-architecture/common-web-application-architectures).

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
| `Application/Common` | Result, Error, phân trang và kiểu dùng chung thực sự. Không gom mọi helper vào đây. |
| `Domain/Entities` | Identity, trạng thái và hành vi bảo vệ invariant của entity. Không chứa chi tiết lưu trữ/HTTP. |
| `Infrastructure/Data` | AppDbContext, Fluent API configuration, migrations, seed. |
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

## 6. OOP, SOLID và thiết kế đối tượng

- Field là private; chỉ mở `internal`, `public`, `protected` khi có nhu cầu contract cụ thể.
  Không có public mutable field.
- Entity bảo vệ trạng thái qua hành vi có tên nghiệp vụ như `Confirm`, `Cancel`, `MarkPaid`;
  tránh setter công khai cho mọi field.
- Constructor/factory kiểm tra dependency bắt buộc và dữ liệu cần để tạo object hợp lệ.
  Constructor dành riêng cho ORM có thể là private/protected nếu EF yêu cầu.
- DTO có thể dùng `record`, `init`, `required`; entity có identity không mặc định dùng record.
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

Giới hạn của validation pipeline đồng bộ được nêu trong
[tài liệu async validation của FluentValidation](https://docs.fluentvalidation.net/en/latest/async.html).

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

Quy tắc lifetime và ownership dựa trên
[hướng dẫn DI và disposal của Microsoft](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines).

## 11. Database, EF Core, repository và transaction

- Mọi EF Core query, configuration và migration thuộc Infrastructure.
- Repository interface mô tả nhu cầu use case, không trả `DbSet`, `DbContext`, EF type hoặc
  `IQueryable` ra Application/API. Trả entity, DTO projection hoặc kiểu kết quả đã thống nhất.
- Query đọc ưu tiên projection `Select` đúng field cần dùng và `AsNoTracking` khi không cần
  tracking. Không áp dụng no-tracking cho entity sẽ được cập nhật trong cùng unit of work.
- Lọc, sắp xếp, phân trang trước khi materialize. Không `ToList` toàn bảng rồi lọc trong RAM.
- Endpoint danh sách phải giới hạn số phần tử; chuẩn mặc định `PageSize = 20`, tối đa `100`,
  trừ contract được review riêng. Thứ tự phân trang phải ổn định, có khóa phân định khi cần.
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
- Không sửa migration đã được áp dụng trên môi trường dùng chung; tạo migration mới.

Tham khảo [truy vấn EF Core hiệu quả](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
và [lifetime/thread safety của DbContext](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/).

## 12. API contract và xử lý lỗi

- Controller mỏng: nhận request → gọi service → ánh xạ kết quả ra HTTP.
  Không chứa công thức tính tiền, điều kiện chuyển trạng thái hoặc gọi SDK nhà cung cấp.
- Route dùng resource số nhiều, chữ thường, nhiều từ dùng dấu gạch ngang:
  `/api/orders`, `/api/payment-transactions`. Method HTTP phải phù hợp với hành động.
- JSON dùng camelCase; thời gian theo ISO 8601; tiền, enum và phân trang phải được định nghĩa
  trong contract. Không trả trực tiếp EF navigation graph.
- Chuẩn nhóm cho kết quả nghiệp vụ dự kiến: `Result`/`Result<T>` và `Error` tại
  `Application/Common`; error có code ổn định. Không trả `bool` hoặc string tùy ý để mô tả mọi lỗi.
- Service trả Result cho các trường hợp như không tìm thấy, không đủ tồn kho, trạng thái không
  cho phép. Exception dành cho lỗi lập trình, vi phạm invariant hoặc sự cố kỹ thuật.
- Domain không tham chiếu `Application.Common.Result`; hành vi Domain dùng kiểu thuần hoặc
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

- Xử lý exception tập trung tại API middleware/exception handler, tránh try/catch giống nhau
  trong mọi action. Catch `Exception` được chấp nhận tại ranh giới xử lý lỗi toàn cục.
- Không catch rỗng, không nuốt exception, không dùng exception làm luồng điều khiển thông thường.
  Chỉ catch khi có thể phục hồi, chuyển nghĩa hợp lý hoặc thêm context cần thiết.
- Rethrow dùng `throw;`, không `throw ex;`. Chuyển exception phải giữ inner exception khi phù hợp.
- Không trả stack trace, câu SQL, connection string hoặc error message nội bộ ra client.
- Thay đổi API gây breaking change phải cập nhật OpenAPI, frontend và kế hoạch tương thích.

Xem [xử lý lỗi và ProblemDetails trong ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api).

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
- Architecture tests đặt dưới `tests/Architecture`: kiểm tra dependency rule giữa project/tầng.
  Các thư mục hiện có cần test project thật trước khi xem đây là kiểm tra đã được chạy.
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

## 17. Tooling và thực thi thống nhất

- Dùng SDK theo `global.json`; không tự đổi TargetFramework hoặc phiên bản ngôn ngữ của từng project.
  Dependency mới cần mục đích rõ ràng và phiên bản cụ thể; tránh nhiều thư viện cùng vai trò.
- `.editorconfig`, analyzer và build settings là cấu hình chung được lưu trong Git.
  Không chỉ dựa vào thiết lập riêng của Visual Studio/Rider/VS Code.
- Nullable, analyzer và format warning mới phải được xử lý; không tắt rule toàn solution để qua build.
  Suppression cục bộ phải nêu lý do và giới hạn phạm vi.
- Khuyến nghị bật `EnforceCodeStyleInBuild` và nâng warning thành error sau khi thống nhất baseline.
  Không áp dụng một lần lên code cũ rồi bỏ mặc hàng loạt warning.
- `dotnet format` hỗ trợ style/analyzer; **không bảo đảm tự xuống mọi dòng về 100 ký tự**.
  Giới hạn này cần review hoặc checker riêng, có loại trừ generated code đã thống nhất.
- CI tối thiểu: restore, format verification, Release build và test suite thực sự.
  Không coi `dotnet test` trả exit code 0 nhưng không discover test nào là bằng chứng có test.
- Khi test projects đã được thêm vào solution, dùng các lệnh từ thư mục chứa solution:

```powershell
dotnet restore AgriSustain.sln
dotnet format AgriSustain.sln --verify-no-changes --no-restore
dotnet build AgriSustain.sln --configuration Release --no-restore
dotnet test AgriSustain.sln --configuration Release --no-build
```

Các lệnh trên là quy trình cần triển khai, không phải báo cáo đã chạy kiểm thử cho tài liệu này.
Tài liệu không tự thay cấu hình build hoặc cài thêm package vào solution.

Analyzer có thể được cấu hình ở mức repository qua
[EditorConfig và cấu hình code analysis](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files).

## 18. Checklist trước khi tạo/duyệt pull request

- [ ] File ở đúng tầng/thư mục; ProjectReference không vi phạm chiều phụ thuộc.
- [ ] Controller không chứa nghiệp vụ; Domain không I/O; mapping không truy vấn dữ liệu.
- [ ] Tên, braces, khoảng trắng và giới hạn dòng phù hợp; không có code/import thừa.
- [ ] Nullable contract rõ, không dùng `!` để giấu lỗi; collection không trả null.
- [ ] Validation, ownership/resource authorization và invariant được bảo vệ đúng nơi.
- [ ] Async/cancellation được truyền; không blocking, fire-and-forget hoặc DbContext dùng đồng thời.
- [ ] Query lấy đúng dữ liệu, phân trang hợp lý; commit/transaction/concurrency có chủ đích.
- [ ] Status và error contract thống nhất; exception/log không làm lộ dữ liệu nhạy cảm.
- [ ] Thanh toán/webhook/retry chống side effect trùng nếu phần thay đổi có liên quan.
- [ ] Test chứng minh hành vi/rủi ro thay đổi; CI có chạy test thực sự khi đã có test project.
- [ ] API, migration, cấu hình và tài liệu liên quan được cập nhật.
- [ ] Mọi ngoại lệ convention đều có lý do và reviewer chấp thuận.

## Phụ lục A. Cấu hình .editorconfig mẫu

Đây là mẫu để nhóm đưa vào root của solution sau khi review cùng baseline hiện có.
Nội dung được ghi trong tài liệu, chưa được kích hoạt như cấu hình repository.
Các rule này chỉ bao phủ một phần convention; rule kiến trúc và nghiệp vụ vẫn cần review/test.

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.md]
trim_trailing_whitespace = false

[*.{json,yml,yaml}]
indent_size = 2

[*.cs]
max_line_length = 100
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_new_line_before_open_brace = all
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_preserve_single_line_blocks = false
csharp_preserve_single_line_statements = false
csharp_prefer_braces = true:warning
csharp_style_namespace_declarations = file_scoped:warning
dotnet_style_operator_placement_when_wrapping = beginning_of_line
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.IDE0011.severity = warning
dotnet_diagnostic.IDE0161.severity = warning
dotnet_diagnostic.IDE1006.severity = warning

dotnet_naming_rule.interfaces_start_with_i.severity = warning
dotnet_naming_rule.interfaces_start_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_start_with_i.style = interface_style
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_style.interface_style.required_prefix = I
dotnet_naming_style.interface_style.capitalization = pascal_case

dotnet_naming_rule.constants_use_pascal_case.severity = warning
dotnet_naming_rule.constants_use_pascal_case.symbols = constants
dotnet_naming_rule.constants_use_pascal_case.style = pascal_case
dotnet_naming_symbols.constants.applicable_kinds = field, local
dotnet_naming_symbols.constants.applicable_accessibilities = *
dotnet_naming_symbols.constants.required_modifiers = const
dotnet_naming_style.pascal_case.capitalization = pascal_case

dotnet_naming_rule.private_fields_use_underscore.severity = warning
dotnet_naming_rule.private_fields_use_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_use_underscore.style = private_field_style
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.private_field_style.required_prefix = _
dotnet_naming_style.private_field_style.capitalization = camel_case

dotnet_naming_rule.private_static_fields_use_s_prefix.severity = warning
dotnet_naming_rule.private_static_fields_use_s_prefix.symbols = private_static_fields
dotnet_naming_rule.private_static_fields_use_s_prefix.style = static_field_style
dotnet_naming_symbols.private_static_fields.applicable_kinds = field
dotnet_naming_symbols.private_static_fields.applicable_accessibilities = private
dotnet_naming_symbols.private_static_fields.required_modifiers = static
dotnet_naming_style.static_field_style.required_prefix = s_
dotnet_naming_style.static_field_style.capitalization = camel_case

dotnet_naming_rule.private_constants_use_pascal_case.severity = warning
dotnet_naming_rule.private_constants_use_pascal_case.symbols = private_constants
dotnet_naming_rule.private_constants_use_pascal_case.style = pascal_case
dotnet_naming_symbols.private_constants.applicable_kinds = field
dotnet_naming_symbols.private_constants.applicable_accessibilities = private
dotnet_naming_symbols.private_constants.required_modifiers = const
```

Quy tắc static có tập symbol cụ thể hơn instance field; private constant có quy tắc giao riêng
để không bị áp dụng tiền tố `s_`. `IDE1006` đặt severity chung cho naming diagnostics;
để naming warning chặn build, nhóm cần bật code-style enforcement và warning-as-error phù hợp.
Chỉ đặt severity trong từng naming rule không bảo đảm được kiểm tra khi build.
Xem [naming rule precedence và enforcement](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/naming-rules).
Không dùng cấu hình này để ép namespace `DTOs` hiện có thành `Dtos` khi chưa đổi convention package.

## Phụ lục B. Những điểm khác bộ quy tắc Java ban đầu

| Quy tắc ban đầu | Chuẩn C# của nhóm |
| --- | --- |
| Dấu `{` cuối dòng | Allman: `{` và `}` trên dòng riêng |
| Method camelCase | Method PascalCase |
| Package chữ thường | Namespace PascalCase theo tên project/thư mục |
| Constant UPPER_SNAKE_CASE | Constant PascalCase |
| Interface theo convention tùy dự án | Interface có tiền tố `I` |
| Collection bắt buộc hậu tố List/Set/Map/Array | Tên số nhiều hoặc mô tả vai trò |
| Không dùng `==` cho string | C# string `==` so sánh giá trị; chỉ rõ StringComparison khi cần |
| try-with-resources | `using` / `await using`, theo ownership tài nguyên |
| Optional | Nullable/Result theo contract; không thêm Optional library mặc định |
| Javadoc | XML documentation cho contract cần giải thích |
| `@Override` | Keyword `override` |
| package-private | `internal` ở cấp assembly; cân nhắc `private` cho member |

Sơ đồ nguồn: [Package_Diagram.drawio — Page-1](https://app.diagrams.net/#G1mJnzsACg-bGQhl9gV-hjsKAF_qAYStPE#%7B%22pageId%22%3A%224hAH4Tu0OPYzQcTq1O-Q%22%7D).
Ánh xạ package trong repository: [package-map.md](package-map.md).
