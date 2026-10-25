# Triển khai ba use case trong project AgriSustain

Code UC-ADM-03, UC-ADM-05 và UC-RC-02 nằm trong các folder có sẵn của một project
`AgriSustain/AgriSustain.csproj`, theo yêu cầu mới nhất của người dùng.
Bốn project riêng đã được bỏ khỏi solution và các thư mục project đó đã được dọn sau khi chuyển code.
Không chạy test trong lần chỉnh sửa này.

## Cấu trúc hiện tại

```text
AgriSustain/
  Api/
    Controllers/        UsersController, CategoriesController, CheckoutController
    Security/           ActorContext, ActiveAccountHandler, ActiveAccountRequirement
    Middlewares/        ExceptionMiddleware
    UseCaseRegistration.cs
  Application/
    Common/             UseCaseResult, Error, Page
    DTOs/               Administration, Checkout
    Interfaces/         service, repository, unit of work, actor
    Services/           Administration, Checkout
    Validators/         Administration, Checkout
  Domain/Entities/      entity cũ và các partial hành vi mới
  Infrastructure/
    Data/               một AgriSustainContext, mapping mở rộng, phân loại lỗi SQL, Geography JSON
    Repositories/       AdministrationRepository, CheckoutRepository
    UnitOfWork/         SqlUnitOfWork
    BackgroundWorkers/  ReservationExpiryWorker
    DependencyInjection.cs
  Program.cs
  appsettings.json
  appsettings.Checkout.json
```

Bốn tầng được phân chia bằng folder và namespace trong cùng một assembly. Controller mới gọi
interface Application; Application mới không truy vấn DbContext hoặc tham chiếu Infrastructure;
Domain mới không thực hiện I/O. Infrastructure chứa persistence và transaction.
Vì dùng một project, compiler không áp đặt ProjectReference giữa các tầng.
Quy định bốn project trong coding-standards.md được thay bằng yêu cầu trực tiếp mới nhất của người dùng.

## Nguồn yêu cầu

- [Report 3](https://docs.google.com/document/d/1nPGNTGhL7M8G2MKMOsU-jz2NDxBCFiZMBoz82Z7ScMU/edit):
  II.4.2, II.4.3, II.6.2; III.4.1, III.6.1; quy tắc dữ liệu 3.3;
  GBR-01/02/06/07/09/10/13–17 và OR-01–14.
- [Report 4](https://docs.google.com/document/d/1gRUQwJAMfWAywcQd9lA82P-DaHC4E1s9/edit):
  kiến trúc/package, schema liên quan, sơ đồ lớp và trình tự của ba UC.
- coding-standards.md người dùng cung cấp tại thư mục SEP490.

## Các vấn đề liên quan khi chuyển code

1. Project cũ đã có Result và Result<T>, với Success/Data/StatusCode khác contract của ba UC mới.
   Giữ nguyên file cũ; kiểu mới được đổi tên thành UseCaseResult<T>. Controller cũ và các bên đang
   dùng Result<T> không phải đổi contract.
2. Người dùng đã chuyển DbContext vào Infrastructure/Data và có namespace chưa lưu trong Visual
   Studio. Thay đổi đang có đã được lưu trước khi tích hợp. Giữ file đó làm DbContext duy nhất;
   không ghi đè nó bằng bản sao từ project Infrastructure riêng. Mapping bổ sung nằm ở partial
   AgriSustainContext.UseCases.cs cùng namespace AgriSustain.Infrastructure.Data.
3. AuthService và ProductModerationController đã có thay đổi import Infrastructure.Data của người
   dùng trước lần chuyển này; các thay đổi đó được giữ nguyên. Không viết lại nghiệp vụ hai chức năng.
4. Program.cs cần thay đăng ký DbContext trực tiếp bằng AddThreeUseCases. Extension này đăng ký
   DbContext, repository, service, unit of work, actor, policy và worker trong cùng ứng dụng.
   Giữ cơ chế đăng nhập/JWT hiện có. Exception middleware mới chỉ áp dụng ba nhóm route mới để
   tránh thay đổi cách xử lý lỗi của API khác.
5. AgriSustain.csproj cần dependency FluentValidation 11.11.0 vì validator mới dùng thư viện này.
6. Tất cả API dùng backend hiện tại cổng 5179. Không chạy API thứ hai ở 5189 và không đọc chéo
   appsettings giữa hai project nữa. Checkout config được chuyển sang appsettings.Checkout.json.
7. Các project test đã tạo trước đó được giữ và đổi reference về project chính; kiểm tra kiến trúc
   được điều chỉnh cho folder/namespace của code mới. Không chạy các test này theo yêu cầu hoãn test.

## Những điểm tài liệu và tích hợp cần lưu ý

| Vấn đề | Cách triển khai và ảnh hưởng |
| --- | --- |
| Report 3 có ảnh/archive danh mục, bảng Category của Report 4 không có cột tương ứng | Bảng phụ CategorySettings giữ ảnh và trạng thái archive; không thêm các cột này vào Category cũ. Ảnh PNG/JPEG tối đa 5 MB. |
| UC mô tả lẫn delete/archive; GBR-10 cấm xóa danh mục có sản phẩm hoạt động | Cấm delete/archive khi còn sản phẩm published. Nếu có tham chiếu lịch sử thì delete chuyển thành archive; chỉ hard-delete khi không còn sản phẩm, danh mục con, commission hoặc benchmark. |
| SRS dùng Consumer/RetailCustomer và một số role không có trong model hiện tại | Checkout dùng RetailCustomer; admin chấp nhận SystemAdmin/Administrator/Admin. Không sửa vai trò trong dữ liệu cũ. |
| SRS mặc định 10 bản ghi, coding standard mặc định 20 trừ contract riêng | Ba UC theo SRS: mặc định 10, tối đa 100. |
| Model scaffold có quote, request key, ledger điểm và snapshot nhiều hơn schema giản lược Report 4 | Giữ schema đang scaffold. Migration có preflight và dừng khi thiếu baseline; không tự sửa cấu trúc database thật. |
| Thuật ngữ FIFO/FEFO dùng chung | Ưu tiên hạn dùng, ngày thu hoạch, khóa lô; hạn dùng phải sau ngày giao dự kiến. Mục chỉ định lô được cấp trước mục linh hoạt. |
| Phí giao, vùng phục vụ và tỷ lệ đổi điểm chưa chốt con số | Đưa vào cấu hình. Thiếu vùng phục vụ thì không đặt đơn; thiếu chính sách điểm vẫn có thể tiếp tục với 0 điểm. |
| Token đã cấp còn hiệu lực khi đình chỉ | Ba UC kiểm tra trạng thái/khóa hiện hành ở authorization và Application, không tin danh tính từ request body. |
| Giỏ hàng UC-RC-01 và thanh toán UC-RC-03 chưa có implementation tích hợp | Checkout đọc giỏ SQL hiện có. Tạo đơn trả thông tin bàn giao paymentMethod/paymentPath/mã/số tiền; UI báo chờ thanh toán, không giả lập QR hay xác nhận thanh toán. |
| README và Playwright cũ dùng cấu trúc demo khác | Giữ file cũ, dùng hướng dẫn hiện tại dưới đây. Test demo cũ không xác minh implementation mới. |

## Luồng chính

UC-ADM-03: tìm kiếm/phân trang/chi tiết; chỉ admin Active không khóa tạm được thao tác. Cấm tự
suspend/activate/unlock. Suspend từ Active, activate từ Suspended, unlock xóa LockedUntil và reset
FailedLoginCount. Thay đổi và audit commit cùng transaction; hủy không ghi DB/audit.

UC-ADM-05: kiểm tra tên trong cùng parent, chống vòng lặp parent, validation trường và ảnh;
đổi trạng thái cần xác nhận. Lưu category/audit cùng transaction. Archive giữ tham chiếu lịch sử
và chặn checkout mới với sản phẩm trực tiếp thuộc danh mục đó.

UC-RC-02: quote lưu snapshot nhưng không giữ hàng. Server lấy giá SQL hiện hành, kiểm tra hàng,
farm, danh mục, địa chỉ, voucher platform/farm và điểm. Voucher sai bị loại với MSG116 để tiếp tục.
Place revalidate trong transaction Serializable, chia SellerOrder theo farm, giữ batch, ghi allocation/
movement, hold điểm, reserve voucher, tạo PendingPayment order, audit và xóa đúng mục giỏ đã chọn.
Unique index của request key và quote bảo vệ retry qua restart. Giá/giỏ/chính sách thay đổi trả MSG118.
Conflict SQL trả 409, client giữ request key để thử lại mà không tạo thêm đơn.

Worker xử lý tối đa 25 đơn PendingPayment thuộc implementation này mỗi 30 giây. Hết hạn thì
cancel, nhả batch/voucher/điểm, ghi history/audit cùng transaction. Chỉ nhả giữ chỗ, không biến lô
recalled/expired thành hàng được bán. Module thanh toán phải xử lý cập nhật có điều kiện/callback muộn.

## Chạy trong Visual Studio

1. Mở AgriSustain.sln, đặt AgriSustain làm startup project, dùng profile http hoặc https hiện có.
2. Với database có sẵn, làm theo database/README.md và chạy bốn migration theo
   thứ tự đã ghi. Máy mới dùng database/Update_DB.sql; file này từ chối DB có sẵn.
   Ứng dụng không tự chạy migration, seed hoặc EnsureCreated lên database thật.
3. Development dùng appsettings.Development.json với Windows Authentication tới
   (localdb)\MSSQLLocalDB, database AgriSustain, theo instance hiển thị trong SSMS trên máy này.
   appsettings.json vẫn giữ cấu hình chung; biến môi trường có thể ghi đè kết nối.
4. Cấu hình Checkout trong AgriSustain/appsettings.Checkout.json: Areas, phí VND mỗi farm, số ngày
   giao, PointValueVnd và MaximumPointDiscountPercent. Areas là vùng phục vụ, độc lập với danh mục
   tỉnh/phường JSON đóng gói. Xem [catalog địa chỉ](checkout-address-catalog.md).
5. Chạy một backend cổng 5179. Từ frontend chạy npm ci và npm run dev; giao diện cổng 5173.
   Đăng nhập thật; demo selector không được cấp quyền vào ba UC mới.
6. Khi deploy, reverse proxy /api về cùng backend AgriSustain; Vite proxy chỉ dùng development.

## Xác minh và phạm vi chưa xác nhận

Lần chuyển vào folder đã build project chính thành công: 0 lỗi, 1 cảnh báo CS1030 có sẵn từ
scaffold DbContext. Frontend chỉ đổi địa chỉ proxy về backend chung; không chạy test.

Kết quả unit/architecture test ở bản bốn project trước đây không được dùng để khẳng định cấu trúc
mới đã được kiểm thử. Integration SQL chưa được xác nhận: trước đó localhost không kết nối được,
LocalDB báo SQL Server process failed to start. Không thay đổi database thật, không tắt trigger
hoặc bỏ constraint để che lỗi. Vẫn cần đối chiếu các trạng thái PendingPayment/Reserve/Release/Hold
và quy tắc quota với database triển khai khi đến giai đoạn test.

## Chẩn đoán lỗi kết nối khi Debug

Output ban đầu báo SQL error 2/state 0/class 20 khi ReservationExpiryWorker gọi
SqlUnitOfWork.BeginAsync. Cấu hình chung trỏ Server=localhost nhưng SSMS hiển thị
database AgriSustain dưới (localdb)\MSSQLLocalDB. Đã thêm override Development cho đúng
instance, không đổi SqlUnitOfWork hoặc tắt worker. Lỗi lặp do worker chạy mỗi 30 giây;
các API cần database cũng chịu ảnh hưởng từ kết nối sai này.

Lệnh sqlcmd chỉ đọc từ môi trường công cụ vẫn timeout với LocalDB, nên kết quả CLI
không xác nhận được kết nối của tiến trình Visual Studio. Không chạy test suite hoặc áp
dụng script SQL trong bước chẩn đoán này.

Sau khi restart Debug trong Visual Studio, kết nối thành công: lỗi đã chuyển từ error 2
sang error 207/state 1/class 16 tại CheckoutRepository.ExpiredOrdersAsync. Output nêu
ba cột không hợp lệ: CheckoutCommittedAt, CheckoutRequestKey, HarvestBatchId. Hai cột
đầu có trong entity Order; câu truy vấn còn Include SellerOrders/OrderItems/allocations.
Đây là chênh lệch giữa database hiện tại và scaffold, cần đối chiếu schema thật trước
khi sửa mapping hoặc bổ sung cột. Script 20261022_three_use_cases.sql hiện chỉ preflight
CheckoutRequestKey, không bổ sung các cột thiếu này; chạy script đó chưa giải quyết được
lỗi 207. Chưa sửa schema, mapping hoặc nghiệp vụ để né lỗi.

### Bản sửa lỗi 207 sau khi đối chiếu metadata LocalDB

Đã kết nối chỉ đọc qua named pipe của instance và xác nhận Order thiếu hai cột
CheckoutRequestKey/CheckoutCommittedAt, OrderItem không có HarvestBatchId. Đã áp dụng
20261022_checkout_schema_alignment.sql trên AgriSustain: thêm hai cột nullable, tạo
unique index lọc theo BuyerUserId/CheckoutRequestKey và RetailQuoteId. Không cập nhật
dữ liệu đơn cũ; NULL cho phép giữ các đơn chưa có request key. Script có transaction,
rollback khi lỗi và lock timeout. Lần đầu thiếu QUOTED_IDENTIFIER đã rollback; sau khi
bổ sung SET options, lần chạy lại thành công và metadata xác nhận đủ cột/index.

AgriSustainContext.UseCases.cs bỏ mapping OrderItem.HarvestBatch/HarvestBatchId và
HarvestBatch.OrderItems; liên kết qua OrderItemBatchAllocation vẫn giữ nguyên. Đây là
mapping scaffold liên quan trực tiếp câu truy vấn worker, không cần thêm cột thừa vào
OrderItem. Mapping request key cho phép NULL và dùng filtered unique index tương ứng.
Script ba UC cũng bổ sung SET options cho filtered index. Chưa áp dụng script ba UC
trong bước sửa này. Build Release thành công, 0 lỗi/1 cảnh báo scaffold cũ; không chạy test.

Sau khi hết lỗi 207, worker báo SQL 208 do RewardPointEntry chưa tồn tại. Metadata cho
thấy database không có bảng điểm nào. Đã mở rộng script alignment và chạy lại thành
công để tạo RewardPointEntry theo entity hiện có: AvailableDelta/HeldDelta tính tự
động, request key unique, FK User/Order, index tra cứu, kiểm tra loại bút toán và số
điểm dương, trigger cấm UPDATE/DELETE. Không seed hoặc tự cấp điểm cho người dùng.

Output Visual Studio sau bản sửa ghi Executed DbCommand (31ms) cho truy vấn worker
bao gồm RewardPointEntry; không còn lỗi 207/208 ở lần chạy này. Database hiện có 0 đơn,
nên quan sát này chỉ xác nhận truy vấn đọc chạy được, chưa xác minh nhả giữ chỗ với
đơn thực tế. Không chạy test suite.

### Chia sẻ schema qua Git

Đã cập nhật file Downloads/Update_DB (1).sql và lưu bản cùng nội dung tại
database/Update_DB.sql. Full script đã bỏ DROP DATABASE, từ chối DB tồn tại bằng
RAISERROR và NOEXEC. Tách baseline upgrade 20261018 từ file gốc, bổ sung migration
checkout input (CartItem.HarvestBatchId, InventoryMovement.RequestKey), runner SQLCMD
và README hướng dẫn bốn bước. Không chạy các script này lên DB trong lần cập nhật
file. Chưa commit/push, chưa chạy test hoặc tạo DB thử. Phạm vi vẫn là ba UC;
các chênh lệch scaffold của module khác được ghi nhận trong database/README.md.

### Bản gộp UpdateDB và dữ liệu mẫu từ Insert.sql

Theo yêu cầu tiếp theo, Update_DB.sql hiện tự tạo DB nếu chưa có, hoặc bổ sung
schema thiếu nếu DB đã có, rồi nạp demo một lần. Hướng dẫn nhiều file/tạo mới
ở các ghi nhận trước đã được thay bằng database/README.md hiện tại.

File Insert.sql người dùng cung cấp gọi nhiều usp_* chưa có trong baseline,
tham chiếu các bảng/cột của model rộng hơn và giả định identity 1..n. Bản sửa
giữ bộ fixture 54 bảng, 340 dòng; ánh xạ ID thật, chuẩn hóa role/verification,
bỏ OrderItem.HarvestBatchId và bỏ UPDATE password/lock toàn bộ User. Bổ sung
schema compatibility theo model EF để hỗ trợ bộ dữ liệu này, bao gồm các bảng
ngoài ba UC. Không sửa C# hoặc thực thi SQL lên DB trong bước cập nhật file.

Seed là dữ liệu demo trực tiếp, không phải implementation các workflow payment,
contract, payout hoặc invariant procedure của schema khác. Có transaction,
sp_getapplock và marker để không nạp lặp; collision dừng thay vì ghi đè dữ liệu.
Đã kiểm tra cú pháp bằng ScriptDom và đối chiếu cột/giá trị/ID theo metadata;
chưa thực thi bản gộp trên SQL Server, chưa chạy test suite. Hai file Downloads
đã được cập nhật, bản gốc giữ trong tmp của workspace. Chi tiết ở database/README.md.
