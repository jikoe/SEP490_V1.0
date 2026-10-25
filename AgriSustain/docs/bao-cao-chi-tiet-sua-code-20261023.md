# Báo cáo chi tiết cập nhật và sửa code AgriSustain

Ngày lập: 23/10/2026. Phạm vi: đợt pull và ghép code từ repository SEP490_V1.0.

## 1. Kết quả và phạm vi thực hiện

Đã cập nhật nhánh main của repository con tới commit d2b64f7, áp dụng lại các
chỉnh sửa cục bộ và giải quyết các xung đột phát sinh. Backend, frontend đều build
thành công; 25 unit test và 3 architecture test đã chạy thành công trong đợt cập nhật.

| Nội dung | Giá trị |
| --- | --- |
| Repository | https://github.com/jikoe/SEP490_V1.0.git |
| Thư mục được pull | C:\Users\huyng_jgxkbi0\Downloads\SEP490\SEP490_V1.0 |
| Thư mục chứa solution | C:\Users\huyng_jgxkbi0\Downloads\SEP490\SEP490_V1.0\AgriSustain |
| Nhánh cập nhật | main |
| Commit trước cập nhật | ae4d9e79c256e2689142e6fd1eb8caff09126060 |
| Commit sau cập nhật | d2b64f71e37022e642067a0871cde1793bf7e920 |
| Hình thức cập nhật | Fast-forward bằng git pull --ff-only origin main |
| Chênh lệch commit với origin/main | 0 ahead, 0 behind tại thời điểm kiểm tra |
| Trạng thái code cục bộ | Chưa commit, chưa push; có bản sao lưu stash |

Repository gốc tại C:\Users\huyng_jgxkbi0\Downloads\SEP490 là một Git repository
khác, đang ở nhánh huy. Repository đó chỉ được fetch để đối chiếu; working tree
không được ghép code trong đợt này. Theo lựa chọn của người dùng, thao tác pull và
sửa tích hợp thực hiện trong repository con SEP490_V1.0.

Lần lập báo cáo này đọc lại source, diff và snapshot Git để mô tả thay đổi; không
thực hiện thêm sửa đổi logic ứng dụng. Kết quả build/test dưới đây là kết quả đã
thực thi trong đợt pull, không phải một lượt kiểm thử mới khi viết báo cáo.

## 2. Cách xác định nguồn gốc thay đổi

Báo cáo sử dụng ba mốc đối chiếu để tránh nhầm phần code đã có với phần vừa sửa:

| Mốc | Ý nghĩa |
| --- | --- |
| Commit ae4d9e7 | Code đã commit trong repository con trước khi pull. |
| Stash trước pull | Các sửa đổi tracked và các file untracked người dùng đang làm trên máy. |
| Commit d2b64f7 và working tree hiện tại | Code GitHub sau pull và kết quả ghép lại code cục bộ. |

Các chức năng quản trị người dùng, quản lý danh mục và checkout trong code cục bộ
đã tồn tại trước lần pull. Việc các file này đang được Git hiển thị là untracked
không có nghĩa chúng được viết mới trong lần tích hợp này.

## 3. Những thay đổi nhận từ GitHub

Giữa hai commit có 8 commit mới, với 106 file thay đổi, 10.051 dòng thêm và
125 dòng xóa theo Git. Thống kê này chỉ tính chênh lệch commit, chưa tính code cục bộ
được khôi phục hoặc tài liệu báo cáo.

| Nhóm | Nội dung nhận về | Ảnh hưởng cần ghép |
| --- | --- | --- |
| Xác thực | Service, repository, validator cho đăng ký và xác thực; mở rộng đăng ký DI. | Giữ cùng cấu hình JWT và cơ chế xác thực khi thêm ba UC cục bộ. |
| Hồ sơ | Service/repository và giao diện hồ sơ, địa chỉ. | Dùng chung DbContext và hạ tầng persistence. |
| Sản phẩm | API, service, repository, validator và màn hình quản lý sản phẩm. | Product dùng collection ProductImages, ảnh hưởng checkout cục bộ. |
| Giỏ hàng | CartService, CartPricingService, repository và giao diện giỏ hàng. | Điều hướng checkout cần quay về đúng route /cart. |
| Đơn hàng | Xử lý đơn phía người bán, quy tắc fulfillment và theo dõi đơn. | Có thêm hợp đồng UnitOfWork và hành vi trên HarvestBatch. |
| Giao diện | Customer portal, header/footer, CSS và các route mới. | Giữ giao diện checkout cục bộ nhưng ghép điều hướng của bản mới. |
| Persistence | Di chuyển DbContext vào Infrastructure/Data; thêm cấu hình concurrency. | Tránh trùng DbContext và trùng implementation của partial method. |
| SQL | Nhận thêm AgriSustain_For_SSMS.sql. | Chỉ cập nhật file; chưa chạy script trên SQL Server. |

Các commit nhận về:

| Commit | Nội dung theo lịch sử Git |
| --- | --- |
| 87e7355 | Port các module Auth, Profile, Orders của nhánh dun sang cấu trúc mới. |
| f26d8da | Loại bỏ EfThreeUcSession cũ, sử dụng trực tiếp AgriSustainContext trong service. |
| a812ab3 | Chuẩn hóa tên UnitOfWork và repository, bỏ tiền tố EfThreeUc. |
| 36b3c18 | Merge pull request #4 từ nhánh dun. |
| b90200b | Thêm chức năng sản phẩm, giỏ hàng và xử lý đơn. |
| b0041ca | Merge main vào XumLM, giữ các phiên bản main ở những file xung đột. |
| 2b9e5c0 | Sửa build tích hợp và nối các service commerce. |
| d2b64f7 | Merge pull request #5 từ nhánh XumLM. |

Các nội dung trong bảng là thay đổi có trong commit nhận từ GitHub. Chúng không
được coi là chức năng mới do lần xử lý xung đột cục bộ này tự triển khai.

## 4. Sao lưu và khôi phục code đang làm

Trước pull có 10 đường dẫn tracked bị thay đổi và 78 file untracked. Đã lưu các
thay đổi này bằng git stash push --include-untracked, sau đó pull và dùng stash apply
để khôi phục. Không dùng stash pop hoặc drop nên bản sao lưu vẫn còn.

| Nội dung | Kết quả |
| --- | --- |
| Tên stash | codex-before-pull-20261023-SEP490_V1.0 |
| Stash commit | 52eea93d3d37ead41eb5a48c1ccece1362f87968 |
| Đối chiếu 78 file untracked đã lưu | Đủ 78 đường dẫn, không thiếu đường dẫn nào. |
| Nội dung không đổi trong nhóm 78 file | 68 file khớp Git hash với snapshot ban đầu. |
| Nội dung được ghép/điều chỉnh trong nhóm đó | 10 file khác snapshot, được giải thích trong phần sửa tích hợp. |

Hợp đồng giao dịch cục bộ ban đầu ở IUnitOfWork.cs được chuyển sang file mới
IUseCaseUnitOfWork.cs; đường dẫn IUnitOfWork.cs hiện giữ hợp đồng của upstream.
Vì vậy, kiểm tra đủ đường dẫn được kết hợp với đối chiếu nội dung, không được hiểu
là mọi file sau ghép đều giống nguyên bản trước pull.

## 5. Các phần thực sự sửa khi tích hợp

### 5.1. Cấu hình package backend

File: [AgriSustain.csproj](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/AgriSustain.csproj).

Code cục bộ và upstream đều có khai báo FluentValidation, trong khi upstream bổ sung
FluentValidation.DependencyInjectionExtensions. Khi áp dụng stash, Git tạo conflict
ở nhóm PackageReference.

Đã giữ FluentValidation và FluentValidation.DependencyInjectionExtensions phiên bản
11.11.0, mỗi package một khai báo. Bản csproj cuối cùng khớp upstream nên không còn
xuất hiện trong danh sách tracked files khác HEAD. Việc không có diff cuối cùng
không có nghĩa file này chưa từng cần xử lý xung đột.

### 5.2. Ghép cấu hình khởi động ứng dụng

File: [Program.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Program.cs).

Bản GitHub đăng ký các module commerce, xác thực, hồ sơ, theo dõi đơn và DbContext.
Bản cục bộ cần nạp cấu hình checkout, đăng ký ba UC và middleware xử lý lỗi riêng.
Nếu chọn toàn bộ một bên của conflict, phần đăng ký của bên còn lại sẽ bị mất.

Đã ghép các nội dung sau:

- Nạp appsettings.Checkout.json; tiếp tục nạp environment variables và command-line
  sau đó để các nguồn này có thể ghi đè cấu hình vừa nạp.
- Gọi AddThreeUseCases(builder.Configuration). DbContext được đăng ký qua chuỗi
  AddThreeUseCases → AddInfrastructure, thay cho lệnh AddDbContext trực tiếp ở đây.
- Giữ AddControllers với UtcDateTimeConverter từ upstream.
- Giữ đăng ký service, repository, validator, UnitOfWork của các module GitHub.
- Giữ cấu hình JWT, authorization, middleware tài khoản, static files và health endpoints.
- Ghép nhánh ExceptionMiddleware dành cho /api/users, /api/categories, /api/checkout.

AddThreeUseCases và các service ba UC là code cục bộ đã có. Phần sửa trong lần này
là nối chúng vào Program.cs chung để các module cùng được đăng ký. Build xác nhận
khả năng biên dịch; chưa khởi động và kiểm tra HTTP đầy đủ để xác nhận toàn bộ DI,
middleware, cấu hình runtime và kết nối database hoạt động đúng.

### 5.3. Xử lý DbContext bị di chuyển và trùng cấu hình model

Các file:

- [AgriSustainContext.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/Data/AgriSustainContext.cs).
- [AgriSustainContext.UseCases.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/Data/AgriSustainContext.UseCases.cs).
- [FeatureConcurrencyConfiguration.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/Data/FeatureConcurrencyConfiguration.cs).

Upstream đổi vị trí AgriSustainContext từ Domain/Entities sang Infrastructure/Data.
Code cục bộ cũng đã xóa đường dẫn cũ và có một file mới tại đường dẫn đích. Git vì
vậy báo rename/delete và modify/delete khi khôi phục stash.

Đã giữ file context chính từ upstream ở Infrastructure/Data, không đưa context trở
lại Domain. Phần model của ba UC và phần concurrency từ upstream trước đó cùng
implement OnModelCreatingPartial(ModelBuilder), gây trùng implementation.

Đã đổi phương thức trong AgriSustainContext.UseCases.cs thành
ConfigureUseCaseModel(ModelBuilder), sau đó gọi nó từ implementation
OnModelCreatingPartial trong FeatureConcurrencyConfiguration.cs.

Kết quả model hiện kết hợp các cấu hình đã có của hai bên:

- CategorySettings và quan hệ một-một theo CategoryId.
- Index duy nhất cho tên danh mục theo danh mục cha.
- CheckoutRequestKey cho phép null đối với đơn cũ và index chống trùng yêu cầu checkout.
- Các cấu hình bỏ qua navigation/cột OrderItem liên kết lô trực tiếp của bản cục bộ.
- Concurrency token của User, Category, Order, HarvestBatch cùng các token upstream
  trên SellerOrder, Product, Cart và DisputeEvidenceRequest.

Unit test tạo script từ EF model đã đạt. Đây là kiểm tra model trong code; chưa
đối chiếu hoặc nâng schema của SQL Server đang chạy.

### 5.4. Tách hai hợp đồng giao dịch không tương thích

Các file liên quan:

- [IUseCaseUnitOfWork.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Interfaces/IUseCaseUnitOfWork.cs).
- [SqlUnitOfWork.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/UnitOfWork/SqlUnitOfWork.cs).
- [DependencyInjection.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/DependencyInjection.cs).
- [AdministrationService.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Services/Administration/AdministrationService.cs).
- [CheckoutService.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Services/Checkout/CheckoutService.cs).
- [ReservationExpiryService.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Services/Checkout/ReservationExpiryService.cs).

Hai bên dùng cùng tên đầy đủ AgriSustain.Application.Interfaces.IUnitOfWork, nhưng
khác hợp đồng:

| Thành phần | Code cục bộ trước ghép | Upstream |
| --- | --- | --- |
| BeginAsync | Trả Task<ITransaction>. | Trả Task. |
| Lưu thay đổi | SaveChangesAsync. | FlushAsync và CommitAsync. |
| Quản lý transaction | Người gọi giữ ITransaction và dispose sau thao tác. | UnitOfWork cung cấp CommitAsync/RollbackAsync. |

Không thể ghép hai BeginAsync có cùng tham số chỉ bằng cách giữ cả hai kiểu trả về.
Đã giữ IUnitOfWork upstream và tạo IUseCaseUnitOfWork cho hợp đồng cục bộ.
SqlUnitOfWork implement interface mới; DI ánh xạ IUseCaseUnitOfWork tới SqlUnitOfWork;
ba service cục bộ chuyển constructor dependency sang interface mới.

Cách quản lý giao dịch của từng nhóm được giữ theo code trước ghép. Đây là điều
chỉnh tên/hợp đồng để tích hợp, chưa phải hợp nhất toàn bộ các thiết kế UnitOfWork.
Upstream còn có một interface IUnitOfWork khác trong namespace
AgriSustain.Application.Interfaces.UnitOfWork; interface đó cũng được giữ lại.

### 5.5. Giữ thông tin validation theo từng trường

File: [Error.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Common/Error.cs).

Upstream định nghĩa Error gồm Code, Message, Kind; code cục bộ cần thêm Fields để
trả lỗi validation của từng input. Đã bổ sung tham số tùy chọn
IReadOnlyDictionary<string, string[]>? Fields = null.

Các chỗ tạo Error với ba tham số vẫn biên dịch. UseCaseResult.Validation của code
cục bộ tiếp tục truyền được lỗi trường; UseCaseControllerBase ánh xạ dữ liệu này
thành ValidationProblemDetails. Frontend ba UC có thể đọc errors và hiển thị thông
báo tương ứng. Chưa có kiểm tra HTTP mới cho toàn bộ các đường đi trả lỗi.

### 5.6. Điều chỉnh checkout theo model nhiều ảnh sản phẩm

Các file:

- [CheckoutRepository.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Infrastructure/Repositories/CheckoutRepository.cs).
- [CheckoutService.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Services/Checkout/CheckoutService.cs).

Model Product mới có collection ProductImages thay cho navigation đơn ProductImage
mà checkout cục bộ đang dùng. Đây là lỗi tương thích được phát hiện khi build.

| Vị trí | Trước sửa | Sau sửa |
| --- | --- | --- |
| Tải dữ liệu giỏ hàng | ThenInclude(x => x.ProductImage). | ThenInclude(x => x.ProductImages). |
| Điều kiện ảnh sản phẩm | Kiểm tra ảnh đơn khác null và IsPrimary. | Kiểm tra ProductImages.Any(image => image.IsPrimary). |
| Ảnh trong CheckoutLine | Lấy ProductImage?.ImageUrl. | Lấy ImageUrl của ảnh đầu tiên có IsPrimary. |

Điều kiện checkout phải có ảnh chính được giữ, đồng thời tương thích với model mới.
Chưa bổ sung quy tắc đảm bảo chỉ có một ảnh IsPrimary và chưa kiểm thử HTTP/SQL cho
trường hợp dữ liệu có nhiều ảnh chính; việc chọn ảnh hiện dùng FirstOrDefault.

### 5.7. Xử lý hai phương thức IsSaleable trên HarvestBatch

Các file:

- [HarvestBatch.Reservation.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Domain/Entities/HarvestBatch.Reservation.cs).
- [HarvestBatch.Behavior.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Domain/Entities/HarvestBatch.Behavior.cs).
- [CheckoutService.cs](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain/Application/Services/Checkout/CheckoutService.cs).

Hai partial class đều khai báo IsSaleable(DateOnly), gây lỗi trùng member khi build.
Nội dung hai phương thức cũng khác nhau nên không thể xóa tùy ý một bên rồi coi
nghiệp vụ không thay đổi.

Đã đổi tên phương thức của checkout cục bộ thành IsReservableForCheckout, cập nhật
Reserve và bước chọn batch của CheckoutService gọi tên mới. IsSaleable của upstream
và các nơi dùng nó trong commerce được giữ nguyên.

| Điều kiện | IsReservableForCheckout cục bộ | IsSaleable upstream |
| --- | --- | --- |
| Trạng thái chấp nhận | Available, InStock. | Available, Harvested, InStock. |
| ActualHarvestDate | Phải có giá trị. | Phải có giá trị và không sau today. |
| StockEnteredAt | Phải có giá trị. | Phải có giá trị. |
| ExpirationDate | Không có hoặc >= today. | Không có hoặc > today. |

Đây là khác biệt nghiệp vụ vẫn tồn tại sau tích hợp. Lần sửa này loại bỏ xung đột
biên dịch, giữ hành vi từng bên; chưa thống nhất chính sách ngày hết hạn và trạng
thái Harvested giữa giỏ hàng, checkout và fulfillment. Checkout còn có kiểm tra
ngày hết hạn so với ngày giao hàng khi chọn batch.

### 5.8. Ghép màn hình checkout và điều hướng giỏ hàng

File: [CheckoutScreen.jsx](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/frontend/src/features/RetailCustomer/CheckoutScreen.jsx).

Giữ phiên bản cục bộ đang gọi API từ threeUseCases.js, sử dụng danh sách vùng giao
hàng, quote phía server và request key trong sessionStorage. Ghép thêm điều hướng
tương thích với màn hình giỏ hàng của upstream.

So với snapshot cục bộ trước pull, chỉ có ba dòng nội dung thay đổi:

1. Component nhận prop navigate.
2. Nút Xem giỏ hàng còn lại gọi navigate('/cart') thay cho reload toàn trang.
3. Thêm breadcrumb Giỏ hàng dẫn tới /cart.

Diff của file so với HEAD upstream lớn hơn nhiều, vì hai phiên bản giao diện đã
khác nhau trước khi ghép. Không diễn giải toàn bộ diff đó là code mới viết trong
lần xử lý xung đột. Frontend đã build; chưa chạy browser E2E cho điều hướng và đặt đơn.

### 5.9. Giữ proxy phục vụ ảnh/media

File: [vite.config.js](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/frontend/vite.config.js).

Bản cục bộ trình bày proxy nhiều dòng cho /api và /health; upstream thêm /media.
Đã giữ cả ba route trỏ tới http://127.0.0.1:5179, đồng thời giữ định dạng nhiều dòng.
Về hành vi, cấu hình cuối cùng tương đương upstream. Cấu hình này áp dụng cho Vite
development server; build frontend không xác nhận media proxy đã được kiểm tra runtime.

## 6. Các phần cục bộ được giữ lại

### 6.1. Quản trị người dùng

File giao diện: [UsersScreen.jsx](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/frontend/src/features/Administrator/UsersScreen.jsx).

Giữ thay đổi cục bộ chuyển request/queryString sang threeUseCases.js. API client này
lấy accessToken từ session, gửi Bearer token và đọc thông báo lỗi từ ProblemDetails.
Đây là thay đổi trước pull, không phải một chức năng được viết mới trong lần ghép.

Các controller, service, validator, DTO và repository quản trị tương ứng đã được
khôi phục. Đợt ghép chỉ thay dependency giao dịch trong AdministrationService theo
mục 5.4, không viết lại toàn bộ logic quản lý trạng thái tài khoản.

### 6.2. Quản lý danh mục

File giao diện: [CategoriesScreen.jsx](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/frontend/src/features/Administrator/CategoriesScreen.jsx).

Giữ các thay đổi cục bộ đã có so với upstream:

- Dùng API client threeUseCases.js.
- Gửi imageBase64, imageContentType và removeImage khi lưu danh mục.
- Chọn ảnh PNG/JPEG, kiểm tra kích thước tối đa 5 MB, hiển thị ảnh xem trước và bỏ ảnh.
- Bổ sung thao tác archive và thông báo giữ tham chiếu lịch sử.
- Chuyển việc quyết định xóa/lưu trữ sang kiểm tra tại server, thay cho chỉ khóa nút
  Xóa khi productCount lớn hơn 0.
- Giới hạn nhập tên danh mục trong giao diện là 150 ký tự.

Các nội dung trên được giữ lại khi khôi phục stash; không được tính là các cải tiến
giao diện vừa tự phát triển trong lần pull.

### 6.3. Solution và bộ test

File: [AgriSustain.sln](C:/Users/huyng_jgxkbi0/Downloads/SEP490/SEP490_V1.0/AgriSustain/AgriSustain.sln).

Giữ phần bổ sung cục bộ đưa ba project UnitTests, IntegrationTests và ArchitectureTests
vào solution, cùng cấu hình Debug/Release và solution folder tests. Không thêm project
backend tầng mới trong đợt tích hợp. Ba project test đều được build trong lượt build
solution, nhưng chỉ UnitTests và ArchitectureTests được thực thi.

### 6.4. Các file cấu hình, SQL và tài liệu

Giữ các file cục bộ như .editorconfig, global.json, appsettings.Checkout.json,
appsettings.Development.json, coding-standards.md, SQL nâng cấp/seed và tài liệu ba UC.
Việc khôi phục file không đồng nghĩa đã thực thi script SQL hoặc xác nhận mọi cấu
hình môi trường trong các file đó đang đúng với máy triển khai.

## 7. Kết quả xác minh đã thực hiện

| Kiểm tra | Kết quả | Phạm vi xác nhận |
| --- | --- | --- |
| Restore NuGet cho solution | Thành công. | Các package cần cho lượt build đã được khôi phục. |
| Build solution backend | Thành công, 0 lỗi, 1 cảnh báo có sẵn. | Project ứng dụng và ba project test biên dịch được. |
| Build frontend | Thành công. | Vite đóng gói được frontend sau ghép. |
| Unit tests | 25/25 đạt, 0 failed, 0 skipped. | Các trường hợp domain/validator và EF model trong bộ test hiện có. |
| Architecture tests | 3/3 đạt, 0 failed, 0 skipped. | Các ràng buộc source import và dependency controller được bộ test kiểm tra. |
| git diff --check | Đạt. | Không phát hiện lỗi whitespace theo phép kiểm tra Git. |
| Unmerged paths | Không còn. | Git đã ghi nhận xử lý xong conflict. |
| Dấu conflict trong source | Không còn. | Không còn marker conflict sau ghép. |
| HEAD so với origin/main | 0 ahead, 0 behind. | Commit nền đã đồng bộ; working tree vẫn có sửa đổi cục bộ. |

Các unit test hiện có kiểm tra điều kiện thao tác tài khoản, trạng thái quản trị,
chặn kích hoạt tài khoản Pending, giữ/hoàn tồn kho, ngưỡng giá bán sỉ, phân bổ tiền
không làm sai tổng, định dạng số điện thoại, validation danh mục và script EF model.

Ba architecture test kiểm tra:

1. Source Domain không import Application, Infrastructure, EF Core hoặc ASP.NET Core.
2. Source Application không import persistence/HTTP theo các chuỗi bị cấm trong test;
   AuthService.cs là ngoại lệ đã có trong bộ test.
3. Constructor của UsersController, CategoriesController, CheckoutController nhận
   interface thuộc AgriSustain.Application.Interfaces.

Đây là các kiểm tra cụ thể của bộ test hiện có, chưa phải kiểm định toàn bộ kiến trúc.

Lệnh đã dùng, chạy từ thư mục solution trừ npm:

```powershell
dotnet restore AgriSustain.sln --nologo --disable-parallel -v minimal
dotnet build AgriSustain.sln --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false
dotnet test tests/Unit/AgriSustain.UnitTests.csproj --no-build --no-restore --nologo -v minimal --blame-hang-timeout 45s
dotnet test tests/Architecture/AgriSustain.ArchitectureTests.csproj --no-build --no-restore --nologo -v minimal --blame-hang-timeout 45s
```

Lệnh frontend chạy tại
C:\Users\huyng_jgxkbi0\Downloads\SEP490\SEP490_V1.0\AgriSustain\frontend:

```powershell
npm run build
```

## 9. Rà soát định tuyến UC theo Report 3 và ảnh người dùng gửi

### 9.1. Căn cứ và cách hiểu

Report 3 mô tả UC-AUTH-01 đăng ký, UC-AUTH-04 đăng nhập, UC-AUTH-05 đăng xuất,
UC-AUTH-03 quản lý hồ sơ; luồng khách lẻ nối UC-RC-01 giỏ hàng → UC-RC-02 checkout
→ UC-RC-03 thanh toán QR → UC-RC-04 theo dõi đơn; luồng nông hộ nối UC-FAR-03
quản lý sản phẩm → UC-FAR-06 xử lý đơn. Màn quản trị nối UC-ADM-01 tổng quan
công việc với UC-ADM-03 quản lý tài khoản, UC-ADM-05 quản lý danh mục và UC-ADM-04
kiểm duyệt sản phẩm. Những phần không có code tương ứng được ghi là khoảng trống,
không coi tên route là bằng chứng nghiệp vụ đã hoàn chỉnh.

Ảnh người dùng cung cấp là một trích đoạn Screen Inventory. Nó liệt kê các màn
hình và mã UC, không chứa yêu cầu triển khai hay lệnh thay đổi quyền; nội dung ảnh
được dùng để lập danh sách đối chiếu.

### 9.2. Luồng đã có trong code sau khi đối chiếu

| Bước | Mã UC / màn hình | Route hoặc hành vi trong code | Tình trạng |
| --- | --- | --- | --- |
| Đăng ký RetailCustomer/Farmer | UC-AUTH-01, UC-AUTH-08 | `/register` → xác minh OTP → `/login` | Có form đăng ký và API OTP. Sau OTP, yêu cầu đăng nhập lại để tạo phiên JWT thật; không giả lập phiên bằng email. |
| Đăng nhập | UC-AUTH-04 | `/login` → ánh xạ `user.role` sang `ROLE_CONFIGS[].landingRoute` | Có ánh xạ riêng cho từng role được UI hỗ trợ. Chưa xác nhận bằng tài khoản thật cho mọi role. |
| Đăng xuất | UC-AUTH-05 | Xóa phiên trong bộ nhớ → `/login` | Có trong `App.jsx`. |
| Quản trị | UC-ADM-01, 03, 05 | `/administration/dashboard` → menu người dùng/danh mục | Có ba màn; hai UC quản lý gọi API. Dashboard là điểm vào và menu nối tiếp sang công việc cụ thể. |
| Kiểm duyệt | UC-ADM-04 | `/moderation/products` | Có giao diện; dữ liệu demo cục bộ vẫn trộn với API thật, cần hoàn thiện hợp đồng và bỏ fixture trước khi xem là production. |
| Nông hộ | UC-FAR-03, 06 | `/farmer/products` ↔ `/farmer/orders` | Có quản lý sản phẩm và xử lý đơn; menu vai trò liên kết hai route. |
| Khách lẻ | UC-RC-01, 02, 04 | `/retail/dashboard` → `/cart` → `/checkout` → `/retail/orders` | Có màn và điều hướng chính. Thanh toán bên ngoài chưa hoàn tất; đơn PendingPayment chưa nối callback QR. |
| Hồ sơ | UC-AUTH-03 | `/retail/profile`, `/farmer/profile` | Có form hồ sơ nhưng một số nội dung còn fixture/demo và hai bản component trùng chức năng. |
| Khách mua buôn | UC-WB-01 trở đi | `/wholesale/dashboard` | Component hiện là trang tổng quan; các màn tìm nhà cung cấp, đề nghị hợp đồng và đặt đơn theo Report 3 chưa có trong ảnh/code đối chiếu này. |
| Nhân viên còn lại | UC-VO, UC-CM, UC-CS, UC-PS, UC-FIN | route dashboard hoặc moderation | Có một số dashboard/console; chưa chứng minh đủ UC chi tiết trong Report 3, nhất là KYC, khiếu nại, payout và nghiệp vụ khuyến mại. |

Trong `App.jsx`, mã đang có sẵn route riêng như `/farmer/products`,
`/moderation/products`, `/retail/dashboard`, `/administration/dashboard` và các
route dashboard theo nhóm staff. Vì vậy bằng chứng code không hỗ trợ kết luận
rằng hàm đăng nhập hiện tại ép mọi role về cùng một route. Nếu người dùng quan sát
thấy tất cả đăng nhập mở cùng trang, các khả năng cần đối chiếu riêng là giá trị
`user.role` thực tế mà endpoint `/api/auth/login` trả về, role claim trong tài
khoản SQL, hoặc bản frontend cũ đang được chạy. Báo cáo này chưa đăng nhập vào
tài khoản thật và chưa gọi endpoint bằng token nên chưa thể xác nhận nguyên nhân
runtime. Các route dashboard của staff cũng có tên dashboard nhưng là route theo
role riêng; nhiều trang trong số đó hiện vẫn là placeholder, gây cảm giác mọi tài
khoản chỉ có dashboard.

### 9.3. Các file sửa trong lượt tiếp tục này

| File | Sửa gì | Lý do / liên kết UC |
| --- | --- | --- |
| `frontend/src/app/App.jsx` | Trước khi tạo phiên, yêu cầu login response có `user.role` và role ánh xạ được tới khu vực đã cấu hình; role thiếu/không hỗ trợ trả lỗi trên màn login thay vì âm thầm chuyển sang Administrator. | Bảo vệ luồng UC-AUTH-04 để không đưa tài khoản role sai vào dashboard quản trị; role hợp lệ tiếp tục tới `landingRoute` riêng. |
| `frontend/src/features/Guest/RegisterScreen.jsx` | Sau khi xác minh OTP, xóa dữ liệu phiên demo/user cũ và điều hướng về `/login`; không lưu user đăng ký như fixture demo. | Nối đúng UC-AUTH-01 → UC-AUTH-08 → UC-AUTH-04. Chỉ đăng nhập mới tạo phiên có access token và route được xác định theo role. |
| `docs/bao-cao-chi-tiet-sua-code-20261023.md` | Bổ sung đối chiếu định tuyến UC, bảng trạng thái role/route, bảng file sửa và danh sách phần còn thiếu theo ảnh. | Phân biệt code đã tồn tại, thay đổi vừa làm và các UC chưa triển khai. |

Các `ROLE_CONFIGS` trong `frontend/src/app/App.jsx` trước đó đã có `landingRoute`
khác nhau cho Administrator, Content Moderator, Farmer, WholesaleBuyer,
RetailCustomer, Verification Officer, Customer Support, Promotion Staff,
Finance Staff, Payment Gateway, Gmail service và Guest. Hàm `completeLogin` đã
đọc role trong response. Lượt này giữ các route riêng, đồng thời bỏ fallback role
lạ sang Administrator: role thiếu/không hỗ trợ bị từ chối ngay ở giao diện đăng
nhập để tránh mở nhầm khu vực quản trị. Chưa gọi API bằng tài khoản thật nên chưa
xác nhận role claim của từng tài khoản trong database; nếu mọi tài khoản vẫn có
cùng role trong response thì cần sửa dữ liệu/logic gán role phía backend.

### 9.4. Đối chiếu tất cả dòng nhìn thấy trong ảnh

| Dòng trong ảnh | Mã UC | Đánh giá theo code hiện tại |
| --- | --- | --- |
| User Management Screen | UC-ADM-03 | Đã có màn, API và route trong menu Administrator. |
| Product Categories Management Screen | UC-ADM-05 | Đã có màn quản lý danh mục và API. |
| Retail Checkout Screen | UC-RC-02 | Đã có quote/đặt đơn; thanh toán QR thực tế là UC-RC-03 riêng và chưa nối. |
| User Registration Screen | UC-AUTH-01 | Có đăng ký khách lẻ/nông hộ và OTP; vừa sửa đường đi sau OTP về đăng nhập thật. |
| Profile & Farm Management Screen | UC-AUTH-03 | Màn hồ sơ có, nhưng tích hợp dữ liệu hồ sơ/nông trại và phân biệt phiên thật với fixture cần rà soát tiếp. |
| Change Password Screen | UC-AUTH-06 | Chưa thấy route/màn đổi mật khẩu tích hợp trong App. |
| Sign In Screen | UC-AUTH-04 | Có đăng nhập thật; route theo role có trong `App.jsx`. Cần xác nhận giá trị role qua tài khoản thực nếu UI cho mọi tài khoản cùng thấy một trang. |
| Logout Handler | UC-AUTH-05 | Có nút đăng xuất và xóa phiên. |
| Order Tracking & History Screen | UC-RC-04 | Có màn theo dõi đơn và route `/retail/orders`; dữ liệu/profile còn một số đường đọc fixture cũ. |
| Shopping Cart Screen | UC-RC-01 | Có API và màn giỏ hàng; giỏ hàng đã dẫn sang checkout. |
| Product Listing Moderation Screen | UC-CM-01 trong ảnh; Report 3 ghi UC-ADM-04 | Có màn kiểm duyệt nhưng demo/API còn trộn. Mã trong ảnh cần thống nhất với Report 3. |
| Farmer Order Fulfillment Screen | UC-FAR-06 | Có màn xử lý đơn và API fulfillment. |
| Agricultural Marketplace Screen | UC-MKT-01 | Không có màn marketplace độc lập trong code hiện tại; route retail dashboard/cửa hàng không thay thế đầy đủ browse/search catalog. |
| Product Detail & Batches Screen | UC-CM-01 trong ảnh; Report 3 tách UC-MKT-02 View Product Detail | Không thấy màn marketplace/product detail/batch phù hợp; mã UC trong ảnh không khớp với Report 3. |
| Farmer Product Management Screen | UC-FAR-03 | Có màn và API quản lý sản phẩm nông hộ. |

**Vấn đề đối chiếu tài liệu:** ảnh gán UC-CM-01 cho cả kiểm duyệt và chi tiết sản
phẩm. Report 3 phân loại kiểm duyệt là UC-ADM-04, marketplace là UC-MKT-01 và
chi tiết sản phẩm là UC-MKT-02. Cần chuẩn hóa mã UC trong bảng Excel/traceability
theo Report 3 để màn hình cùng mã có cùng ý nghĩa.

### 9.5. Các vấn đề còn tồn tại cần xử lý theo thứ tự

1. **Xác nhận cùng-route sau đăng nhập:** quan sát người dùng chưa được tái hiện
   bằng API/login thật trong lượt này. Ghi lại response role cho từng tài khoản và
   route URL sau `completeLogin`; so sánh với các `landingRoute` trong
   `frontend/src/app/App.jsx`. Nếu role claim dữ liệu sai, sửa seed/role trong DB;
   nếu role đúng nhưng ánh xạ thiếu, bổ sung mapping cho đúng giá trị claim.
2. **UC-RC-03 thanh toán QR:** checkout hiện trả đơn PendingPayment và thông tin
   handoff; chưa có tạo QR thật, webhook xác thực, đối soát thanh toán hoặc xử lý
   thanh toán muộn.
3. **UC-MKT-01/02:** chưa có marketplace/catalog, trang chi tiết sản phẩm và lô
   phù hợp trong code; không nên coi dashboard retail là thay thế.
4. **UC-AUTH-06:** chưa nối trang đổi mật khẩu và quy tắc yêu cầu phiên hiện hành.
5. **UC-RC-04 và hồ sơ:** một số màn order/profile vẫn đọc sessionStorage cũ hoặc
   fixture; cần thống nhất nguồn session thật/JWT và dữ liệu từ API.
6. **UC-CM moderation:** bỏ trộn dữ liệu demo với API, thống nhất mã UC theo Report
   3 và ảnh; xác nhận quyền moderator ở backend.
7. **Wholesale/B2B:** trang hiện tại chủ yếu là dashboard; thiếu luồng tìm nhà cung
   cấp, capacity/profile, tạo đơn/đề nghị hợp đồng, ký và theo dõi như các UC B2B
   trong Report 3.
8. **Các role staff:** màn Verification, Customer Support, Finance, Promotion,
   Payment Gateway và Notification hiện chưa đủ bằng chứng triển khai quy trình
   KYC/khiếu nại/payout/webhook/thông báo trong code; dashboard không đồng nghĩa UC
   hoàn chỉnh. Một số actor còn là role kỹ thuật chứ không phải người dùng tương tác.
9. **Kiểm thử/runtime:** không chạy test theo yêu cầu người dùng. Luồng role, login,
   OTP và kết nối các UC vẫn cần xác minh thủ công hoặc kiểm thử khi người dùng yêu cầu.

Các mục 2–9 là giới hạn trong code và tài liệu đã đọc; mục 1 là nguyên nhân
runtime chưa được xác nhận. Không gán các nội dung này cho chỉ dẫn trong Report 3
hoặc ảnh; đây là đánh giá kỹ thuật dựa trên phạm vi code hiện tại.

Cảnh báo backend còn lại là #warning trong DbContext scaffold về việc đưa connection
string ra khỏi source. Không đưa giá trị connection string hoặc thông tin xác thực
vào báo cáo này.

## 8. Giới hạn và các điểm chưa được xác minh

| Nội dung | Trạng thái |
| --- | --- |
| SQL integration tests | Chưa chạy; dù project IntegrationTests đã build thành công. |
| Browser E2E | Chưa chạy. |
| HTTP smoke test/DI runtime | Chưa chạy cho toàn bộ ứng dụng sau ghép. |
| Schema SQL Server thực tế | Chưa đối chiếu và chưa nâng cấp trong đợt pull. |
| Thực thi SQL tạo/nâng cấp/seed | Chưa thực hiện trong đợt pull. |
| Media proxy runtime | Chưa kiểm tra bằng yêu cầu HTTP thực tế. |
| Chính sách lô hàng giữa các module | Hai quy tắc vẫn khác nhau như mục 5.7. |
| Nhiều ảnh cùng IsPrimary | Checkout chọn ảnh đầu tiên; chưa xác minh bất biến dữ liệu qua kiểm thử tích hợp. |
| Thanh toán và bàn giao sau đặt đơn | Chưa xác minh xuyên suốt với dịch vụ ngoài. |

Vì vậy, kết quả hiện tại xác nhận code đã ghép hết conflict, build được và đạt
28 test đã nêu. Kết quả đó chưa đủ để kết luận toàn bộ nghiệp vụ, database và
các tích hợp ngoài đã sẵn sàng triển khai.

## 9. Trạng thái bàn giao

Code hiện nằm trên nền main tại d2b64f7 cùng các sửa đổi cục bộ chưa commit.
Các file untracked vẫn cần được xem xét khi chuẩn bị commit; chỉ commit tracked diff
sẽ bỏ sót phần controller, service, repository, cấu hình và test cục bộ.

Bản sao lưu trước pull vẫn ở stash commit
52eea93d3d37ead41eb5a48c1ccece1362f87968. Không nên áp dụng lại stash nguyên khối lên
working tree hiện tại vì các thay đổi đã được ghép; snapshot này dùng để đối chiếu
hoặc khôi phục có chọn lọc khi cần.

Chưa tạo commit, chưa push và chưa triển khai. Tài liệu này mô tả các thay đổi đã
thực hiện, các thay đổi cũ được giữ lại và giới hạn của bằng chứng kiểm tra hiện có.
