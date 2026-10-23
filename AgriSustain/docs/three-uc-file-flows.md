# Luồng chạy theo file của ba UC

Cập nhật EF Core Database First: [cấu hình và scaffold](ef-core-database-first.md), [đối chiếu màn hình Report 3](three-uc-srs-screen-review.md). Bản EF/UI mới chỉ build và xem nhanh; kết quả test chi tiết cũ chưa xác nhận bản mới.

Đối chiếu ngày 19/10/2026 với code hiện tại và Project Tracking, sheet WBS:
https://docs.google.com/spreadsheets/d/1I72FB2bm9xNbIlKtbviD3jwGYIxZHkjf5g9fWST_3UI/edit#gid=1712403342

| Feature trong tracking | Function/Screen | UC | Chức năng |
| --- | --- | --- | --- |
| Administration | User Management Screen | UC-ADM-03 | Manage User Account Status |
| Administration | Product Categories Management Screen | UC-ADM-05 | Manage Product Categories |
| Consumer Ordering | Retail Checkout Screen | UC-RC-02 | Place Retail Order |

`Consumer Ordering` là tên feature trong tracking. Actor trong code vẫn là
`RetailCustomer`; nhãn feature không đổi role thành Consumer. Package C# `Orders` và
`Administration` giữ cấu trúc Report 4, không đổi namespace chỉ để thay nhãn báo cáo.

Các đường dẫn bên dưới tính từ thư mục `AgriSustain/`.

## 1. Khởi động và đường đi chung

1. `frontend/index.html` nạp `frontend/src/main.jsx`.
2. `frontend/src/main.jsx` gắn React vào DOM và nạp stylesheet.
3. `frontend/src/app/App.jsx` chọn UsersScreen / CategoriesScreen / CheckoutScreen.
   Selector hiện là phiên giả lập, không thực hiện đăng nhập thật.
4. `src/AgriSustain.Api/Program.cs` khởi tạo host, controllers, DI, middleware, auth và policy.
   Nếu có `frontend/dist`, API phục vụ bản React build cùng cổng của API (preview lần này 5181, profile VS 5179).
5. `src/AgriSustain.Api/ApplicationServiceCollectionExtensions.cs` đăng ký service và
   FluentValidation cho ba UC.
6. `src/AgriSustain.Infrastructure/InfrastructureServiceCollectionExtensions.cs` hiện yêu cầu
   Development + `Demo:Enabled`; chọn `SqlServer` mặc định Development, policy mẫu và catalog địa giới.
   Test fixture ép UoW/identity về InMemory riêng; không phụ thuộc connection mặc định.
7. Mỗi HTTP request đi qua `Security/Authentication/DemoAuthenticationHandler.cs`,
   `HttpCurrentUser.cs` và policy trước khi vào controller. Middleware trả 401/403 nếu
   sai phiên/quyền. Service vẫn kiểm tra actor Active/lock trong cùng UoW với nghiệp vụ.
8. `Controllers/ApiResults.cs` đổi `Result<T>` thành HTTP response; lỗi không xử lý được
   đi qua `Middlewares/ApiExceptionHandler.cs`. Frontend nhận ProblemDetails và hiển thị
   thông báo. Không hiển thị stack trace cho người dùng.

`frontend/src/api/client.js` thêm `/api`, gắn fixture Authorization, serialize JSON,
parse lỗi; `components/ui/Common.jsx` cung cấp form/modal/paging/format tiền, thời gian.
Ẩn nút trên UI không thay thế kiểm tra quyền trên server.

## 2. UC-ADM-03 — Manage User Account Status

### Đọc danh sách / chi tiết

| Thứ tự | File | Vai trò |
| --- | --- | --- |
| 1 | `frontend/src/features/administration/UsersScreen.jsx` | Effect tải danh sách; `search()` áp dụng draft khi nhấn Tìm kiếm và về trang 1; chi tiết mở hash route riêng |
| 2 | `frontend/src/api/client.js` | GET `/api/users?keyword=...&role=...&status=...&page=...` hoặc GET `/api/users/{id}` |
| 3 | `src/AgriSustain.Api/Controllers/UsersController.cs` | Nhận query/id; chỉ route Administration |
| 4 | `src/AgriSustain.Application/Services/Administration/AccountStatusService.cs` | `SearchAsync()` / `GetAsync()` validate, authorize rồi đọc repository |
| 5 | `Validators/Administration/UserQueryValidator.cs` và `Common/CurrentUserAccess.cs` trong Application | Kiểm tra page/filter và actor SystemAdmin Active, không khóa |
| 6 | `Interfaces/Repositories/IAccountRepository.cs` → `Infrastructure/Repositories/EfCore/EfThreeUcSession.Administration.cs` | EF projection/filter/count/page trên SQL; KYC đọc từ profile, không đọc PasswordHash |
| 7 | `Application/Mappings/UserMappings.cs`, `DTOs/Administration/UserResponse.cs` | Chỉ trả read model được phép, tính trạng thái khóa theo server clock |

### Suspend / Activate / Unlock

1. `UsersScreen.prepare()` mở dialog. Hủy chỉ đóng dialog; không gửi lệnh thay đổi.
2. `UsersScreen.change()` POST `/api/users/{id}/status-actions` với action, reason,
   `confirmed=true`.
3. `UsersController` gọi `AccountStatusService.ChangeAsync()`.
4. `AccountActionValidator` kiểm tra action, confirmation, reason khi suspend/activate.
5. `ChangeAsync()` mở `IThreeUcUnitOfWork.ExecuteAsync()`, đọc lại actor và target,
   chặn tự thao tác, kiểm tra trạng thái nguồn.
6. `Domain/Entities/Accounts/UserAccount.cs`: `Suspend()` đổi Active → Suspended và
   tăng mock SessionVersion; `Activate()` đổi Suspended → Active; `Unlock()` xóa
   LockedUntil và FailedLoginCount, giữ business Status.
7. `session.Accounts.SaveAsync()` rồi `session.AddAuditAsync()` lưu trạng thái và before/after.
   Chỉ khi cả callback thành công UoW mới commit. Lỗi audit phải rollback thay đổi.
8. Response quay về UI; UI cập nhật chi tiết và tải lại danh sách.

SQL tương ứng: `dbo.User`, `dbo.FarmerProfile`/`dbo.WholesaleBuyerProfile` cho thông tin
hiển thị nếu adapter hỗ trợ, và `dbo.AuditLog`. Thu hồi JWT thật thuộc module auth;
SessionVersion trong entity hiện chỉ là giả lập, không phải cột đã thêm vào dbo.User.

## 3. UC-ADM-05 — Manage Product Categories

### Đọc / tạo / sửa

1. `frontend/src/features/administration/CategoriesScreen.jsx` tải danh sách và parent options;
   `search()` áp dụng filter draft và về trang 1; thêm/sửa dùng trang riêng; `change()` chỉ đổi draft.
2. `save()` gửi POST `/api/categories` hoặc PUT `/api/categories/{id}`. Payload gồm
   name, parentId, description, priority, isActive và confirmed khi đổi trạng thái.
3. `Controllers/CategoriesController.cs` chuyển sang `CategoryService.SearchAsync()` /
   `SaveAsync()` trong `Application/Services/Administration/CategoryService.cs`.
4. `CategoryQueryValidator` / `SaveCategoryValidator` kiểm tra query và tên 2–100,
   description ≤500, priority không âm, parentId dương nếu có.
5. `CategoryService` kiểm tra SystemAdmin, parent tồn tại và Active; đọc target khi sửa.
   Không áp unique tên, ảnh, archive hoặc Version vì người dùng đã từ chối các quy tắc đó.
6. `Domain/Entities/Catalog/Category.cs`: `Update()` đổi trường được duyệt.
7. Category repository lưu category; UoW lưu audit cùng transaction. Tạo mới trả ID.
8. UI trở về danh sách và refresh khi thành công; giữ draft/hiện lỗi nếu thất bại.

### Ẩn / kích hoạt / xóa

`CategoriesScreen.execute()` → POST `/api/categories/{id}/{deactivate|activate|delete}`
→ `CategoriesController.Change()` → `CategoryService.ChangeAsync()`.

- Deactivate/activate gọi `Category.SetActive()`; không xóa product.
- Delete yêu cầu confirmation và kiểm tra **mọi** product reference, kể cả chưa publish.
- Có product: không cho xóa, vẫn có thể ẩn. Không có product nhưng còn category con,
  benchmark/commission reference: cũng không xóa vì FK còn tồn tại.
- Hoàn toàn không có reference: xóa vật lý, giữ target ID và before-state trong audit.
- Lỗi audit: rollback delete; không để category mất mà audit chưa được ghi.

SQL tương ứng: `dbo.Category`, `dbo.Product`, `dbo.RegionalPriceBenchmark`,
`dbo.CommissionPolicy`, `dbo.AuditLog`. FK là lớp bảo vệ cuối cùng của adapter SQL,
không được tắt FK để xóa dữ liệu.

## 4. UC-RC-02 — Place Retail Order

### Mở checkout

1. `frontend/src/features/orders/CheckoutScreen.jsx` GET `/api/checkout` và
   `/api/checkout/provinces`; lấy địa chỉ mặc định và selected cart.
2. `Controllers/CheckoutController.cs` gọi `CheckoutService.GetContextAsync()`.
3. `Common/CurrentUserAccess.cs` yêu cầu RetailCustomer Active, không khóa.
4. `ICheckoutRepository.GetContextAsync()` trả cart theo buyer, addresses, điểm/voucher context.
   Đọc giỏ là tiêu thụ UC-RC-01; không có thêm/sửa/xóa giỏ trong màn này.
5. `Infrastructure/Data/Geography/VietnamAddressCatalog.cs` đọc JSON tỉnh/xã local.
   Khi chọn tỉnh, UI GET `/api/checkout/provinces/{code}/wards`; chọn tỉnh khác phải bỏ xã cũ.
6. Nếu có selected items và saved address, UI tự lấy quote lần đầu.

### Tính lại tổng tiền

1. `CheckoutScreen.update()` / `address()` sửa draft, hủy quote cũ và đổi demo retry key.
2. `preview()` POST `/api/checkout/quotes`.
3. `CheckoutInputValidator.cs` kiểm tra recipient, phone đã thống nhất, address fields,
   delivery/payment enum, điểm không âm, notes ≤300.
4. `CheckoutService.PreviewAsync()` gọi `CalculateAsync()` trong UoW.
5. `CalculateAsync()` đọc đúng cart của buyer, lấy selected lines; normalize code/tên địa giới
   từ JSON; đọc product/category/seller eligibility và current prices.
6. `AllocateBatches()` trong cùng file service lọc lô bán được, phân bổ FEFO.
   Một product line có thể lấy 3 kg từ lô A và 2 kg từ lô B; tier price tính theo 5 kg
   của product line. Bộ remaining dùng chung ngăn hai dòng cart cấp cùng phần tồn hai lần.
7. `DemoCheckoutPolicy.cs` trả phí giao/coverage và giới hạn demo. Service kiểm tra
   voucher, tính discount/points/total; voucher sai trả warning MSG116 và bỏ giảm giá.
8. Đoạn SHA-256 trong `CalculateAsync()` băm giá, địa chỉ và allocation. `SaveQuoteAsync()` lưu quote
   theo buyer/expiry. **Preview không reserve stock**.
9. UI hiển thị summary; chỉ cho Place khi quote khớp draft hiện tại.

### Xác nhận đặt hàng

1. UI mở confirmation; Hủy không tạo order. Xác nhận gọi `place()` gửi
   POST `/api/checkout/orders` với quoteId, requestKey, confirmed.
2. `PlaceOrderValidator` → `CheckoutService.PlaceAsync()` → UoW transaction.
3. Đọc lại actor/owned quote; xử lý retry theo contract demo; chặn quote hết hạn.
4. Tính lại toàn bộ summary. Price/cart/allocation khác fingerprint cũ: MSG118,
   không tạo order; người mua phải xem và xác nhận lại summary mới.
5. Tạo `Domain/Entities/Ordering/RetailOrder.cs` và các snapshot trong `Domain/ValueObjects/`.
   `EnsureValid()` kiểm tra số lượng/phân bổ/tổng seller bằng parent total.
6. Gom nhu cầu theo BatchId, đọc lại stock, `HarvestBatch.Reserve()` chuyển available → reserved.
   `EfThreeUcSession.Checkout.cs/FindBatchAsync()` ghi nhận counter trước reserve;
   `ReserveBatchAsync()` conditional UPDATE đúng delta, kiểm tra counter/status/expiry/affected rows.
7. Save order → reserve benefits → remove đúng selected cart rows → add audit.
   Bất kỳ bước nào lỗi đều rollback. Không xóa cart item chưa chọn.
8. UI hiện order code, total và PendingPayment. Không tự tạo QR hoặc kết luận thanh toán xong.

SQL tương ứng: `dbo.Cart`, `dbo.CartItem`, `dbo.DeliveryAddress`, `dbo.Product`,
`dbo.Category`, `dbo.FarmerProfile`, `dbo.User`, `dbo.HarvestBatch`, `dbo.Voucher`,
`dbo.VoucherRedemption`, `dbo.RetailCheckoutQuote`, `dbo.Order`, `dbo.SellerOrder`,
`dbo.OrderItem`, `dbo.OrderItemBatchAllocation`, `dbo.AuditLog`.
Không có bảng số dư/giữ điểm đã được duyệt (DB-09). Không có SQL request-key contract
đã được duyệt (DB-06). Các mapping này đã chuyển sang EF; bản mới chưa chạy test chi tiết theo yêu cầu chờ duyệt; xem mục 5.

## 5. Adapter SQL và giới hạn mock

Luồng persistence mặc định Development:

`IThreeUcUnitOfWork` → `Infrastructure/UnitOfWork/EfThreeUcUnitOfWork.cs` →
`Infrastructure/Repositories/EfCore/EfThreeUcSession.cs` và hai partial:
`EfThreeUcSession.Administration.cs`, `EfThreeUcSession.Checkout.cs` → SQL Server.

- `RunAsync()` dùng scoped AgriSustainDbContext + Serializable transaction. Execute lấy SQL application
  lock theo transaction để serialize writer local demo qua các host; không coi đây là
  thiết kế throughput production. Không có commit riêng từng repository.
- Callback thành công mới commit; failure Result hoặc exception rollback toàn bộ.
  ReadAsync rollback cuối đọc, không publish bất kỳ thay đổi mock nào.
- Administration partial: EF LINQ paging/filter/count, mapper User/profile, lưu status/lock,
  CRUD Category và kiểm tra Product/child/benchmark/commission references.
- Checkout partial: context/eligibility/FEFO candidates/voucher/quote, stock conditional
  update và ghi parent/seller/item/allocation/reservation/cart. `AddAuditAsync()` trong
  partial chung ghi actor/target/before-after/reason/time/trace cùng transaction.
- `EfDemoIdentityReader.cs` map selector theo **username seed**, query status/lock SQL;
  không dựa vào IDENTITY hay đọc mật khẩu. DemoAuthenticationHandler chỉ nhận local socket.
- `Data/Demo/SqlDemoDependencies.cs`: điểm, session revocation và request-key cache chỉ
  trong process; detached changes chỉ publish sau SQL commit. Restart reset các mock này.
- Category và Order lấy IDENTITY bằng placeholder **chưa commit** rồi SaveChanges snapshot
  trong cùng transaction; nếu lỗi rollback cả placeholder, không dùng MAX(id)+1.
- Development config chọn SqlServer; test cũ thay UoW/identity rõ ràng bằng DemoUnitOfWork/
  DemoSession/DemoState. InMemory chỉ là chế độ kiểm tra tách biệt, không tự fallback khi SQL lỗi.

`/health` trả mode; `/health/database` mở SQL và đọc Category qua EF. App.jsx chỉ báo kết nối SQL sau readiness thành công. Thông báo không
khẳng định auth/điểm/payment thật. Hướng dẫn seed/run/query: `database/demo-data-and-web.md`.

Không viết thêm UC đăng nhập, cart CRUD, quản lý product/voucher, thanh toán, loyalty hay
background worker. Không bổ sung schema DB-06/DB-09 chưa chốt.
