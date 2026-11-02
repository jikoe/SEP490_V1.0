# Coding Standards — AgriSustain

Phiên bản 1.1 — ngày 23/10/2026.

Áp dụng cho backend C#/.NET và frontend React trong repository này.
Đối chiếu source tại commit `f9d9f36` trên nhánh `ThuongNQ` và cấu trúc package
trong thiết kế backend của nhóm. Cây package đầy đủ bên dưới là cấu trúc chuẩn phải giữ.

## 1. Phạm vi áp dụng

- Áp dụng cho code mới và phần code được sửa. Không format hoặc refactor cả module
  không liên quan trong một thay đổi nghiệp vụ.
- Đặc tả use case xác định hành vi sản phẩm; tài liệu này quy định cách tổ chức và viết code.
- Phân biệt code đang tồn tại với code đã hoàn thành. Thư mục trống, package đã cài,
  entity hoặc giao diện demo không chứng minh chức năng đã được tích hợp.
- Code sinh từ database được miễn quy tắc trình bày, nhưng vẫn phải review mapping,
  bảo mật, tính đúng đắn và ảnh hưởng dữ liệu.
- Ngoại lệ về kiến trúc hoặc contract phải có lý do cụ thể trong phần mô tả thay đổi.

## 2. Công nghệ và cấu trúc package chuẩn

- Backend: ASP.NET Core, target `net8.0`, EF Core và SQL Server; bật nullable/implicit usings.
- Solution chỉ có một project Web. Các tầng là thư mục/namespace trong cùng assembly.
- Frontend: React 19, Vite 8, JavaScript/JSX, Tailwind CSS 4 và CSS riêng của feature.
- Không tự đổi framework, tách project hoặc bổ sung thư viện khi không cần cho yêu cầu.

```text
AgriSustain/
  AgriSustain.sln
  AgriSustain/
    AgriSustain.csproj
    Properties/
      launchSettings.json
    Api/
      Controllers/
      Middlewares/
      Security/
    Application/
      Common/
      DTOs/
      Interfaces/
      Mappings/
      Services/
      Validators/
    Domain/
      Entities/
    Infrastructure/
      Auditing/
      BackgroundWorkers/
      Data/
        AgriSustainContext.cs
      Email/
      FileStorage/
      Payments/
      Repositories/
        MarketplaceRepository.cs
      UnitOfWork/
    appsettings.json
    Program.cs
  frontend/
    src/
      main.jsx
      app/App.jsx
      api/client.js
      session/session.js
      components/ui/Common.jsx
      features/
      styles/app.css
```

Giữ đầy đủ tên và vị trí package như trên, kể cả package chưa có implementation.
Không gộp, đổi tên hoặc bỏ package khỏi cấu trúc chuẩn chỉ vì use case hiện tại chưa dùng.
Package trống không yêu cầu tạo class/interface giả; chỉ bổ sung implementation khi có nghiệp vụ.
Khi cần, nhóm file theo use case bên trong package hiện có, không tạo một tầng song song.

Connected Services và Dependencies trong Visual Studio là các node quản lý của IDE,
không phải package nghiệp vụ cần tạo thành thư mục. Properties chứa cấu hình chạy;
appsettings.json và Program.cs nằm ở gốc project, không nằm trong Infrastructure.

## 3. Trách nhiệm package và chiều phụ thuộc

### 3.1. Trách nhiệm từng package

- **Api/Controllers:** bind request, lấy danh tính đã xác thực, gọi service, trả HTTP response.
  Controller mới không truy vấn DbContext hoặc chứa nghiệp vụ.
- **Api/Security:** cấu hình/hành vi authentication và authorization, policy/role/resource
  authorization cùng filter bảo vệ/kiểm tra request theo cấu trúc hiện có. Không chứa
  nghiệp vụ thanh toán, tồn kho hoặc tự viết thuật toán mật mã.
- **Api/Middlewares:** xử lý HTTP pipeline dùng chung như exception, trace/correlation
  và logging request phù hợp; không xử lý nghiệp vụ của một use case.
- **Application/Services:** điều phối use case, validation nghiệp vụ, quyền tài nguyên
  và điểm hoàn tất thao tác ghi; gọi repository/adapter qua interface.
- **Application/Interfaces:** hợp đồng service, repository và tích hợp có nhu cầu thực tế.
  Không trả `DbContext`, `DbSet`, `IQueryable` hoặc kiểu SDK ra ngoài Infrastructure.
- **Application/DTOs:** request/response theo nghiệp vụ. Không dùng EF entity làm public API body.
- **Application/Mappings:** chuyển entity/DTO hoặc kiểu nội bộ theo contract; chỉ ánh xạ
  dữ liệu, không truy vấn DB, gọi API ngoài hoặc quyết định giá/quyền/trạng thái nghiệp vụ.
- **Application/Validators:** kiểm tra dữ liệu đầu vào use case; không xử lý HTTP response.
- **Application/Common:** các kiểu dùng chung như `Result`; không gom mọi helper vào đây.
- **Domain/Entities:** entity và quy tắc thuần như `MarketplaceRules`; không database,
  HTTP, filesystem, DI container hoặc logging hạ tầng.
- **Infrastructure/Data:** `AgriSustainContext` và cấu hình persistence.
  Namespace context phải là `AgriSustain.Infrastructure.Data`. Đây là package kết nối
  EF Core với SQL Server; migrations/seed chỉ thêm khi quy trình database yêu cầu.
- **Infrastructure/Repositories:** thực thi truy vấn/ghi dữ liệu theo hợp đồng Application;
  không quyết định quyền HTTP, giá hoặc chuyển trạng thái nghiệp vụ độc lập với service.
- **Infrastructure/UnitOfWork:** triển khai hợp đồng commit/transaction ở Application/Interfaces
  khi use case cần. Các repository trong một unit of work dùng cùng DbContext;
  không tự commit riêng từng repository hoặc đưa điều phối nghiệp vụ vào UnitOfWork.
- **Infrastructure/Payments:** adapter PayOS/VietQR, giao tiếp provider, xác minh chữ ký
  và chuyển dữ liệu provider sang kiểu nội bộ. Application quyết định trạng thái nghiệp vụ.
- **Infrastructure/FileStorage:** đọc/ghi/xóa file và tạo liên kết theo hợp đồng;
  không quyết định quyền duyệt hồ sơ hoặc quyền sở hữu tài nguyên thay cho use case.
- **Infrastructure/Email:** adapter Google Gmail API, gửi thư/template theo hợp đồng
  Application; không để kiểu SDK Gmail xuất hiện trong DTO/use case công khai.
- **Infrastructure/Auditing:** lưu lịch sử hành động theo hợp đồng, có actor, resource,
  thời điểm và kết quả; phân biệt audit bền vững với log chẩn đoán.
- **Infrastructure/BackgroundWorkers:** lập lịch/nhận công việc, tạo DI scope, hỗ trợ
  cancellation và gọi application service; không sao chép nghiệp vụ của service vào worker.
- **Program.cs:** composition root, đăng ký DI và cấu hình middleware/endpoint.

### 3.2. Chiều phụ thuộc theo thiết kế

```text
React SPA -- HTTPS/JSON, Bearer JWT --> Api
Api/Controllers --> Application/Interfaces, DTOs
Application/Services --> Application/Interfaces, DTOs, Validators, Mappings, Common
Application --> Domain/Entities
Infrastructure --> Application/Interfaces và các kiểu contract cần thiết
Infrastructure --> Domain/Entities
Infrastructure/Data --> SQL Server
Infrastructure/Payments --> PayOS / VietQR
Infrastructure/Email --> Google Gmail API
Program.cs --> đăng ký DI cho API, Application và Infrastructure
```

- Đây là ranh giới package trong một project, không phải yêu cầu tạo project riêng cho mỗi tầng.
- Controller gọi service qua interface; service gọi repository/adapter qua interface.
  Implementation được đăng ký bằng DI trong Program.cs. Application không tham chiếu
  implementation Infrastructure dù runtime thực thi code của repository/adapter đó.
- Domain không tham chiếu ngược Application, Infrastructure hoặc Api.
  Không để service/repository gọi controller hoặc tạo dependency vòng.
- Luồng dữ liệu SQL chuẩn: Controller → Service → Repository → DbContext → SQL Server.
  Dữ liệu trả về qua repository/service/DTO đến API và frontend.
- Khi cần tác vụ ngoài SQL, Application gọi hợp đồng adapter tương ứng, không gọi trực tiếp
  PayOS/Gmail/filesystem từ controller, Domain hoặc frontend để thay thế nghiệp vụ server.
- Compiler chưa bảo vệ ranh giới namespace vì hiện chỉ có một project, nên cần review mã nguồn.

### 3.3. Hiện trạng và giới hạn áp dụng

Hiện AuthService còn dùng DbContext trực tiếp và ProductModerationController còn chứa
persistence/nghiệp vụ. Không sao chép cách này sang module mới. Chuyển đổi code cũ phải
giữ contract và nằm trong phạm vi thay đổi có review.

Các package Payments, Email, FileStorage, Auditing, BackgroundWorkers và UnitOfWork
trong cây chuẩn không đồng nghĩa các tích hợp đã hoàn thành. Quy tắc trách nhiệm ở trên
áp dụng khi triển khai, không cho phép tự thêm chức năng ngoài phạm vi được giao.

## 4. Quy tắc C#

- Dùng 4 spaces, Allman braces, mỗi statement một dòng; block điều kiện/vòng lặp có `{ }`.
- File UTF-8, có newline cuối file. Ưu tiên LF; tránh diff chỉ đổi line ending hàng loạt.
- Dòng C# tối đa 100 ký tự, trừ literal/URL không thể ngắt hợp lý và code sinh tự động.
- Ưu tiên file-scoped namespace, khớp đường dẫn thư mục; bỏ using không dùng.
  Nhóm System trước, sau đó các namespace còn lại theo thứ tự chữ cái.
- Type, method, property và constant dùng PascalCase; parameter/local dùng camelCase;
  private instance field dùng `_camelCase`; interface có tiền tố `I`.
- Hàm trả Task/ValueTask có hậu tố `Async`; dùng tên thể hiện hành động và nghiệp vụ.
- Tên trong code dùng tiếng Anh. Không đổi tên entity/field scaffold hoặc giá trị role/status
  trong database chỉ để hợp quy tắc đặt tên.
- DTO có thể dùng record/init. Giữ convention theo module: Auth đang dùng hậu tố Dto,
  Marketplace dùng Request/Response. Record phụ chỉ dùng cho response chính có thể cùng file.
- Constructor thường và primary constructor đều được phép khi dependency rõ ràng.
- Dùng guard clause; tách helper khi làm rõ trách nhiệm, không tách chỉ để giảm số dòng.
- Expression-bodied member cho biểu thức đơn giản; không nén nhiều bước thành một dòng.
- Giữ nullable contract chính xác; không dùng `!`, catch rỗng hoặc suppression để giấu lỗi.
- Collection rỗng trả collection rỗng; object không tồn tại trả null/Result theo contract.
- Dùng `decimal` cho tiền; không dùng float/double hoặc tính tổng tiền có thẩm quyền ở frontend.
- Giữ `DateTime` UTC trong model/DTO cũ; thời điểm mới ưu tiên UTC/DateTimeOffset.
  Ngày thuần dùng DateOnly. Múi giờ nghiệp vụ phải rõ; Marketplace hiện tính ngày theo UTC+7.

## 5. Validation và truy cập dữ liệu

- Backend kiểm tra độc lập với frontend: ID dương, độ dài, range, min/max, giá trị hữu hạn,
  allowlist sort/status và quy tắc liên quan nhiều field.
- Marketplace hiện dùng static `MarketplaceRequestValidator`; Auth DTO dùng DataAnnotations.
  Chưa dùng FluentValidation; không thêm package nếu validation C# thuần đã đủ.
- Binding/ModelState thuộc API; kiểm tra quyền sở hữu, trạng thái và dữ liệu hiện tại thuộc use case.
  Không sao chép cùng quy tắc nghiệp vụ vào nhiều tầng.
- Tái sử dụng `MarketplaceRules` cho public product, available batch, certificate và review
  khi phù hợp; list/detail không được áp dụng các phiên bản điều kiện mâu thuẫn.
- Query đọc ưu tiên projection và AsNoTracking khi không cần cập nhật entity.
- Filter/sort/page trong SQL trước khi materialize; không tải toàn bảng rồi lọc trong RAM.
- Danh sách có giới hạn và thứ tự ổn định. Giữ contract hiện có: Marketplace mặc định
  pageSize 12, moderation 10, tối đa 100; không tự đổi mặc định của API đã tích hợp.
- Tránh N+1 và Include không cần thiết. Không kết luận SQL chạy đúng chỉ từ build thành công.
- Parameter hóa raw SQL; tên cột/sort động phải qua allowlist. Không nối input vào câu SQL.
- Repository không tự commit sau từng thao tác. Service quyết định ranh giới ghi dữ liệu
  qua hợp đồng persistence phù hợp; không thêm generic repository/UnitOfWork cho hình thức.
- Một thao tác atomic dùng cùng DbContext; transaction tường minh khi nhiều bước cần atomic.
  Không giữ transaction trong lúc chờ thanh toán, email hoặc API bên ngoài.
- Tồn kho/tiền/chuyển trạng thái cần chiến lược concurrency và xử lý conflict; kiểm tra
  tồn tại trước khi ghi không thay thế constraint hoặc khóa đồng thời của database.
- Database hiện có entity/mapping scaffold và trigger. Giữ precision, khóa ngoại,
  computed/default value và trigger khi sửa model; không tự migrate/seed DB dùng chung.

## 6. Async và dependency injection

- Dùng I/O async; truyền CancellationToken cuối parameter qua controller/service/repository.
- Không `.Result`, `.Wait()`, async void hoặc Task.Run để bọc I/O trong request.
- Không fire-and-forget cho công việc phải hoàn tất; không chạy song song trên cùng DbContext.
- Constructor injection; không `new` DbContext/repository hoặc gọi service locator trong nghiệp vụ.
- DbContext/repository/service dùng chúng có lifetime Scoped; không giữ chúng trong Singleton.
- Không dispose dependency do DI sở hữu; tài nguyên tự tạo dùng using/await using đúng ownership.
- Cancellation bình thường không biến thành lỗi 500; thao tác đã commit cần xử lý theo contract.

## 7. API contract và xử lý lỗi

- Route dùng chữ thường/resource rõ nghĩa, đúng HTTP method; giữ route đã được caller tích hợp.
- JSON camelCase. Không serialize navigation graph hoặc đưa dữ liệu nhạy cảm vào DTO công khai.
- Marketplace dùng `Result`/`Result<T>` trong Application/Common và `ToActionResult`
  trong ApiControllerBase. Body có success, message, statusCode và data nếu là Result<T>.
- Result hiện chứa StatusCode dạng số; HTTP status phải khớp. Application không trả IActionResult,
  Domain không phụ thuộc Result/HTTP. Không trả 200 để che lỗi.
- Login trả trực tiếp LoginResponseDto gồm accessToken, expiresAtUtc, user; moderation có
  response riêng. Không tự bọc lại các endpoint này bằng Result.
- MSG01/MSG16/MSG40 đang nằm trong message; Result chưa có field code riêng.
  Không parse message để điều khiển nghiệp vụ; bổ sung code phải cập nhật contract/callers cùng lúc.
- Result dành cho lỗi nghiệp vụ dự kiến; exception dành cho lỗi kỹ thuật/lập trình.
  Chỉ catch khi xử lý được, chuyển nghĩa hoặc thêm context; rethrow bằng `throw;`.
- Program.cs đã cấu hình AddProblemDetails/UseExceptionHandler, nhưng không phải mọi lỗi
  đều có cùng body. Frontend phải xử lý response lỗi có thể thiếu field hoặc không có JSON.
- HTTP chuẩn: 200 có body, 201 tạo resource, 204 không body, 400 input sai, 401 chưa xác thực,
  403 thiếu quyền, 404 không tìm thấy/ẩn resource, 409 conflict, 429 giới hạn, 500 lỗi kỹ thuật.
- Không trả stack trace, SQL hoặc secret ra client. Thay đổi contract phải sửa cả DTO/callers.

## 8. Bảo mật, tích hợp và logging

- Tái sử dụng login/JWT đã có. Xác minh signature, issuer, audience, lifetime; không tin token
  chỉ vì decode được. Frontend ẩn nút không thay thế authorization ở backend.
- Danh tính lấy từ principal; không tin UserId/role/giá/tổng tiền/trạng thái thanh toán do client gửi.
  Kiểm tra quyền theo resource, không chỉ role.
- Không commit/log password, JWT, signing key, connection string có secret hoặc hồ sơ riêng tư.
  Dùng cấu hình môi trường/user-secrets/secret store theo môi trường.
- Không tự viết password hash, tắt TLS validation hoặc nới quyền để tránh lỗi tạm thời.
- Upload giới hạn dung lượng/loại/nội dung, dùng storage key an toàn và chống path traversal.
  File riêng tư cần authorization hoặc liên kết ký có thời hạn.
- Adapter payment/email/storage đi qua interface Application khi triển khai. Không xác nhận
  thanh toán từ redirect/client; kiểm tra provider, chữ ký, số tiền, currency và mã giao dịch.
- Webhook/retry có side effect cần idempotency bền vững. Tác vụ ngoài DB không thể rollback
  bằng SQL transaction; dùng cơ chế phục hồi/outbox khi yêu cầu nhất quán cần tới.
- Logging dùng ILogger<T>, template cố định và đúng level; không log cùng exception ở mọi tầng.
- Comment giải thích lý do, giới hạn hoặc quyết định khó suy ra; không nhắc lại từng dòng code.
  XML/JSDoc dùng khi contract có điều kiện/side effect cần giải thích; không thêm boilerplate vô nghĩa.

## 9. Frontend React

- Function component/hooks; component và file JSX PascalCase, biến/hàm camelCase, hook bắt đầu `use`.
- Dùng 2 spaces, single quotes và semicolon; JSX string attribute dùng double quotes.
  Dùng const mặc định, let khi cần gán lại; không dùng var.
- Feature giữ component/CSS nghiệp vụ của mình. Chỉ đưa UI dùng chung thực sự vào components/ui;
  không gom logic feature vào App.jsx hoặc Common.jsx.
- App.jsx đang dùng hash routing; tái sử dụng navigate/readRoute. Không tự thêm router/store/client mới.
- Giữ controlled input, cập nhật state bất biến; key collection dùng ID ổn định.
- Gọi API qua `src/api/client.js`: request tự thêm /api và Bearer token; truyền path như
  `/marketplace/products`. Login dùng helper login, không triển khai đăng nhập riêng trong feature.
- Đọc đúng response: Marketplace dùng result.data; login/moderation dùng body trực tiếp.
  Không tạo mock/fallback thành công để che lỗi dữ liệu thật.
- Effect tải dữ liệu dùng AbortController/cleanup; bỏ qua AbortError và kết quả bị hủy.
  Cleanup timer/listener/subscription, hoạt động đúng khi React.StrictMode setup/cleanup lặp lại.
- Loading/error/empty/success rõ ràng; lỗi có cách xử lý/thử lại, không đổi lỗi thành danh sách rỗng.
- Marketplace giữ keyword/filter/sort/page/pageSize trong URLSearchParams của hash route.
  Đổi keyword/filter reset page và giữ filter khác; hỗ trợ đồng bộ Back/Forward.
- Tìm kiếm khi gõ debounce khoảng 300 ms, xử lý composition tiếng Việt; Enter tìm ngay,
  xóa keyword khôi phục kết quả theo filter. Request cũ không ghi đè kết quả truy vấn mới.
- Session/token hiện chỉ ở bộ nhớ trong src/session/session.js, mất khi reload/đăng xuất.
  Không tự thêm localStorage token/refresh token hoặc dùng demo fixture làm authentication.
- Giữ tên thư mục role cũ có khoảng trắng khi chỉnh module đó; thư mục mới dùng PascalCase
  không khoảng trắng, không rename module cũ hàng loạt trong task khác.

## 10. CSS và accessibility

- Tái sử dụng Tailwind/CSS hiện có. Style theo phạm vi feature, như .marketplace/.mkt-*;
  tránh rule global input/button/header làm ảnh hưởng trang khác.
- Review desktop/mobile, focus/hover/disabled. Bỏ outline phải có focus indicator thay thế rõ ràng.
- Button cho hành động, link cho điều hướng; khai báo type cho button trong form.
- Input có label/aria-label; icon-only button có accessible name; ảnh có alt phù hợp,
  icon trang trí aria-hidden. Thao tác được bằng bàn phím.
- Dùng role=status cho cập nhật động phù hợp, role=alert cho lỗi cần người dùng xử lý.
- Không render HTML từ API bằng dangerouslySetInnerHTML. URL ngoài kiểm tra protocol;
  tái sử dụng safeUrl/ProductImage trong Marketplace. Ảnh lỗi có fallback trung thực.
- Không tự thêm ảnh, tài liệu hoặc dependency ngoài phạm vi nhiệm vụ được giao.

## 11. Phối hợp và phạm vi tích hợp hiện tại

- Không triển khai trùng module của thành viên khác khi chưa thống nhất phạm vi.
- Theo phân công hiện tại, Marketplace chỉ giữ biểu tượng giỏ và nút thêm ở trang chính
  dưới dạng disabled chờ tích hợp. Chi tiết sản phẩm không có nút thêm hoặc bộ chọn số lượng.
  Chưa có API/service/repository giỏ hàng; không giả lập thông báo add thành công.
- Cart/CartItem và mapping vẫn thuộc schema dùng chung. Không tự xóa chúng vì module giỏ chưa làm.
- Các điểm trên ghi nhận hiện trạng, không đóng băng yêu cầu tương lai. Khi phân công/đặc tả đổi,
  cập nhật contract và callers theo phạm vi mới.
- README/demo/test config cũ có thể khác source hiện tại; đối chiếu Program.cs, csproj,
  DTO và caller trước khi copy code hoặc hướng dẫn chạy.

## 12. Build, kiểm tra và bàn giao

Từ repository root:

```powershell
dotnet build AgriSustain/AgriSustain.sln --configuration Release
npm --prefix AgriSustain/frontend run build
```

Nếu dependencies đã restore, có thể thêm --no-restore cho dotnet build.
Dùng npm ci trong frontend khi cần cài lại dependencies theo lockfile.

- Backend profile http dùng cổng 5179; Vite dùng 127.0.0.1:5173 và proxy /api sang backend.
  Proxy /health còn trong Vite nhưng Program.cs hiện chưa map endpoint này.
- npm run build tạo dist; Program.cs hiện chưa cấu hình phục vụ SPA/static files.
  Không coi build là publish/deploy hoặc bằng chứng luồng UI/SQL chạy đúng.
- Chưa có global.json, .editorconfig thực thi, lint script hoặc CI pipeline được tìm thấy
  tại lần đối chiếu này. Tài liệu không tự kích hoạt format/analyzer/quality gate.
- Solution chưa có backend test project. Frontend có Playwright/test cũ, nhưng config còn
  trỏ `../src/AgriSustain.Api`, /health và chế độ InMemory không khớp backend hiện tại.
  Phải sửa cấu hình trước khi dùng suite này để xác nhận hành vi.
- Khi triển khai test, ưu tiên hành vi/rủi ro thật: validation, quyền tài nguyên, tiền,
  trạng thái, tồn kho/concurrency và SQL translation. EF InMemory không chứng minh SQL Server đúng.
- Nếu task yêu cầu chưa làm test, không tự thêm/xóa test. Nêu rõ check đã chạy và phần chưa kiểm tra.
- Không thêm suppression, skip test hoặc giảm rule để che lỗi. Warning có sẵn phải phân biệt
  với warning mới; bật tooling mới sau khi thống nhất baseline, không format toàn repo tùy tiện.
- Mỗi thay đổi bàn giao nêu vấn đề, hành vi sau sửa, contract/data bị ảnh hưởng, cách kiểm tra
  và giới hạn còn lại. Không báo test pass khi không có test được discover/chạy.
- Commit tập trung đúng phạm vi; không đưa bin/obj/node_modules/dist, secret hoặc file tạm vào Git.
  Không tự commit/push, viết lại lịch sử hoặc đổi nhánh của người khác ngoài yêu cầu được giao.

## 13. Checklist review

- [ ] File/namespace đúng thư mục và dependency đúng tầng.
- [ ] Tái sử dụng helper/contract hiện có; không thêm abstraction hoặc dependency thừa.
- [ ] Validation, authorization, async/cancellation và xử lý lỗi đầy đủ.
- [ ] Query, transaction/concurrency và mapping không gây lộ/mất dữ liệu.
- [ ] API/frontend đồng bộ contract; không có mock thay dữ liệu nghiệp vụ thật.
- [ ] UI responsive, có trạng thái tải/lỗi/rỗng và hỗ trợ bàn phím/focus.
- [ ] Không sửa module ngoài phân công, không commit secret hoặc output sinh tự động.
- [ ] Ghi rõ build/check đã chạy, phần chưa kiểm tra và giới hạn cần bàn giao.
