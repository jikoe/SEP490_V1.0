# Báo cáo rà soát trùng lặp và đề xuất gộp file

Ngày lập: 24/10/2026. Trạng thái: **đã gộp G1–G5, G7 và xóa 3 dashboard không dùng**
theo quyết định duyệt. **G6 (gộp tài liệu) không thực hiện** theo quyết định của người dùng.
Kết quả xem mục 6.

## 1. Phạm vi và căn cứ

| Nội dung | Giá trị |
| --- | --- |
| Nhánh | `main` local tại `612e4c0` ("ghep noi code"); `origin/main` tại `d778c28` |
| File được rà | 98 file mới/chưa commit so với `main`, cùng các file `main` mà chúng trùng chức năng |
| Quy tắc áp dụng | `coding-standards.md` v2.0: §1, §2, §4, §5, §7, §11, §12, §13 và Phụ lục A |

Nguyên tắc khi đánh giá việc gộp:

1. Chỉ gộp khi hành vi sau gộp giữ nguyên với dữ liệu hợp lệ. Khác biệt nào còn
   lại được ghi rõ trong mục 3.
2. Không gộp nhiều public type vào một file (§2: "Một public type chính trong một
   file; tên file trùng tên type").
3. Không đụng các thành phần mà quy tắc chỉ định giữ riêng: `UseCaseResult<T>`,
   `Page<T>`, `UseCaseControllerBase`, `ExceptionMiddleware`,
   `ActiveAccountRequirement/Handler`, các file ráp DI (§4, §12, §13).
4. Không format diện rộng code cũ (§1: tách thành PR riêng).

## 2. Đã làm trước báo cáo (không phải gộp)

| Việc | File | Kết quả |
| --- | --- | --- |
| Sửa điều kiện hiển thị marketplace `"Verified"` → `"Approved"` theo yêu cầu | `Domain/Entities/MarketplaceRules.cs` | Marketplace trả về 5/5 sản phẩm trên DB local; trang chi tiết có lô để chọn |
| Đặt tên field static readonly theo `s_camelCase`, đưa field lên trước method (§2, §3) | `OrderTrackingService.cs`, `OrderTrackingRepository.cs` | Không đổi hành vi |
| Thêm `{ }` cho mọi `if`, ngắt dòng ≤ 100 ký tự ở phần code mới (§2) | `AuthService.cs`, `AuthController.cs`, `IAuthService.cs`, `ProfileService.cs`, `ProfileRepository.cs`, `OrderTracking*.cs` | Chỉ sửa dòng viết trong đợt nối code |
| Bỏ toán tử `!` bằng pattern matching (§7) | `ProfileRepository.cs` | Không đổi hành vi |
| Tách type mới ra file riêng (§2) | `ChangePasswordRequestValidator.cs`, `PasswordRules.cs`, `DTOs/Orders/OrderItemBatchDto.cs` | Không đổi hành vi |

Kiểm tra sau các việc trên: build thành công; unit test 27/27; architecture test 3/3.

## 3. Đề xuất gộp (chờ duyệt)

### G1. Lấy danh tính người gọi: 3 class cùng một việc

| File | Nguồn | Nội dung |
| --- | --- | --- |
| `Api/Security/ActorContext.cs` | mới | `UserId` từ claim `NameIdentifier` (0 nếu thiếu), `TraceId` |
| `Api/Security/CurrentActor.cs` | main | `UserId` từ cùng claim (0 nếu thiếu), `Role` |
| `Api/Security/Authentication/HttpCurrentUser.cs` | main | `UserId` từ cùng claim (`null` nếu thiếu) |

- Cách gộp: `CurrentActor` triển khai cả `ICurrentActor`, `IActorContext` và `ICurrentUser`.
  Kiểu `long?` của `ICurrentUser.UserId` được giữ bằng explicit interface implementation.
  Xóa `ActorContext.cs` và `HttpCurrentUser.cs`; cập nhật đăng ký DI ở `Program.cs` và
  `Api/UseCaseRegistration.cs`.
- Giữ nguyên: 3 interface và mọi constructor đang dùng chúng; `TestActor` trong integration test.
- Hành vi: giữ nguyên (cùng claim, cùng giá trị mặc định cho từng interface).

### G2. Hành vi giữ chỗ của `HarvestBatch`: 2 partial, 1 method trùng

| File | Nguồn | Nội dung |
| --- | --- | --- |
| `Domain/Entities/HarvestBatch.Reservation.cs` | mới | `IsReservableForCheckout`, `Reserve`, `Release` |
| `Domain/Entities/HarvestBatch.Behavior.cs` | main | `IsSaleable`, `ConsumeReservation`, `ReleaseReservation` |

- `Release(q)` và `ReleaseReservation(q)` cùng logic: q phải > 0 và ≤ `ReservedQuantity`,
  rồi chuyển q từ `ReservedQuantity` về `AvailableQuantity`; cả hai ném `InvalidOperationException`.
- Cách gộp: chuyển nội dung `Behavior.cs` vào `Reservation.cs` (file §6 chỉ định cho hành vi
  `HarvestBatch`), giữ một method `ReleaseReservation`, xóa `Release`. Cập nhật
  `ReservationExpiryService` và `BusinessRuleTests` sang tên này.
- Giữ riêng `IsSaleable` và `IsReservableForCheckout` vì khác nghiệp vụ (báo cáo 23/10, mục 5.7).
- Hành vi: giữ nguyên; chỉ nội dung message của exception ở luồng hết hạn giữ chỗ đổi.

### G3. Mapping thủ công của `AgriSustainContext`: 2 partial, khai báo trùng

| File | Nguồn | Nội dung |
| --- | --- | --- |
| `Infrastructure/Data/AgriSustainContext.UseCases.cs` | mới | Mapping ba UC, gồm token `HarvestBatch.AvailableQuantity/ReservedQuantity` |
| `Infrastructure/Data/FeatureConcurrencyConfiguration.cs` | main | `OnModelCreatingPartial` gọi hàm trên rồi khai báo lại hai token đó và các token commerce |

- Cách gộp: §11 quy định mapping thủ công nằm ở `AgriSustainContext.UseCases.cs`. Chuyển
  `OnModelCreatingPartial` và các token commerce vào file này, bỏ hàm trung gian
  `ConfigureUseCaseModel` và hai dòng token trùng, rồi xóa `FeatureConcurrencyConfiguration.cs`.
- Hành vi: model EF không đổi (khai báo token lặp lại là idempotent). Unit test sinh script
  từ EF model sẽ xác nhận điều này.

### G4. API client frontend: 2 file cùng chức năng

| File | Nguồn | Nội dung |
| --- | --- | --- |
| `frontend/src/api/threeUseCases.js` | mới | `request` (bắt buộc có phiên, gộp lỗi theo trường), `queryString` |
| `frontend/src/api/client.js` | main | `login`, `request` (hỗ trợ upload `FormData`, trả `error.fields`), `queryString` |

- `queryString` trùng 100%. Hai hàm `request` trùng phần gửi request (fetch, Bearer token,
  parse JSON); chỉ khác cách tạo thông báo lỗi.
- Cách gộp: đưa phần gửi chung vào một hàm nội bộ của `client.js`. Giữ `request` như cũ và
  thêm export `requestUseCase` với đúng cách báo lỗi của `threeUseCases.js`. Đổi import của
  `UsersScreen`, `CategoriesScreen`, `CheckoutScreen` rồi xóa `threeUseCases.js`.
- Không gộp hai cách báo lỗi làm một: màn commerce dùng component `Errors` để liệt kê
  `error.fields`. Gộp chúng sẽ làm lỗi theo trường hiển thị hai lần.
- Hành vi: giữ nguyên cho cả hai nhóm màn hình.

### G5. Đăng ký DI lặp lại sau khi ghép

- `Program.cs` gọi lại `AddSingleton(TimeProvider.System)` và `AddHttpContextAccessor()`,
  trong khi `AddThreeUseCases` đã đăng ký cả hai.
- Cách gộp: xóa hai dòng lặp trong `Program.cs`. `AddHttpContextAccessor` là TryAdd;
  `TimeProvider` vẫn là cùng một instance `System`, nên hành vi giữ nguyên.

### G6. Tài liệu trùng nội dung

| Gộp từ | Vào | Lý do |
| --- | --- | --- |
| `docs/pull-comparison-20261023.md` | `docs/bao-cao-chi-tiet-sua-code-20261023.md` | Cùng đợt pull; mọi nội dung (commit, stash, xung đột, kết quả kiểm tra) đã có trong bản chi tiết. Đồng thời sửa đánh số mục (mục 9 xuất hiện hai lần, mục 8 nằm sau mục 9). |
| `database/README_Create_DB_First_Time.md` | `database/README.md` (thêm một mục) | Cả hai là hướng dẫn dựng database; §11.1 trỏ người đọc tới `database/README.md`. |

Phụ lục A của `coding-standards.md` liệt kê tên thành phần (`ActorContext`,
`api/threeUseCases.js`). Nếu gộp G1/G4, chỉ cập nhật tên trong bảng tham chiếu này;
không sửa nội dung quy tắc.

### G7. Công thức giá sỉ: 2 bản khác điều kiện

| File | Nguồn | Điều kiện áp dụng giá sỉ |
| --- | --- | --- |
| `Domain/Entities/RetailPricing.cs` (`UnitPrice`, checkout dùng) | mới | Có giá sỉ và ngưỡng; số lượng ≥ ngưỡng |
| `Application/Services/CartPricingService.cs` (`UnitPrice`, giỏ hàng dùng) | main | Như trên, thêm: ngưỡng > 1, giá sỉ > 0, giá sỉ ≤ giá lẻ |

- `ProductRequestValidator` đã ép đúng ba điều kiện thêm này, nên với sản phẩm nhập qua
  UC-FAR-03, hai công thức cho cùng kết quả.
- Với dữ liệu chèn thẳng bằng SQL và vi phạm các điều kiện đó (ví dụ giá sỉ > giá lẻ),
  giỏ hàng hiện tính giá lẻ còn checkout tính giá sỉ cao hơn.
- Cách gộp: đưa điều kiện chặt vào `RetailPricing.UnitPrice` (Domain), cho
  `CartPricingService.UnitPrice` gọi lại hàm này. Test hiện có vẫn đạt.
- Hành vi: giữ nguyên với dữ liệu hợp lệ. Với dữ liệu vi phạm, checkout chuyển sang tính
  như giỏ hàng; **đây là thay đổi nghiệp vụ cần duyệt riêng**.

## 4. Cùng chức năng nhưng không gộp

| Nhóm | Lý do giữ nguyên |
| --- | --- |
| `UseCaseResult<T>` ↔ `Result<T>`/`FeatureResult<T>` | §12: giữ contract Result cũ, không tạo/trộn Result mới |
| `Page<T>` ↔ `PageResult<T>`/`PagedResponse<T>` | §12; JSON khác (`pageNumber`, `currentActorId`), gộp sẽ đổi API của UC-ADM-03/05 |
| `IUseCaseUnitOfWork` + `ITransaction` + `SqlUnitOfWork` ↔ `IUnitOfWork` + `UnitOfWork` | Hợp đồng transaction không tương thích (báo cáo 23/10, mục 5.4); §11 ghi riêng mức Serializable |
| `ExceptionMiddleware` + `PersistenceErrorClassifier` ↔ `FeatureExceptionMiddleware` | Mã lỗi và thông điệp khác; bộ phân loại tính cả lỗi SQL 547, gộp sẽ đổi lỗi khóa ngoại của API cũ từ 500 sang 409; §12 ghi rõ phạm vi middleware |
| `ActiveAccountRequirement/Handler` ↔ `ActiveFeatureAccountMiddleware` | Kiểm tra khác nhau (khóa tạm thời so với email đã xác minh + role); §13 chỉ định Requirement/Handler |
| `UseCaseControllerBase` ↔ `FeatureControllerBase`/`ApiResults` | §12: controller mới kế thừa `UseCaseControllerBase`; mỗi lớp map một kiểu Result khác |
| `UseCaseRegistration.cs`, `Infrastructure/DependencyInjection.cs` ↔ `Program.cs` | §4 chỉ định ba nơi ráp ứng dụng |
| Các DTO nhỏ (`ProvinceOption`, `WardOption`, …) | §2: một public type một file |
| `IsReservableForCheckout` ↔ `IsSaleable` | Khác nghiệp vụ (mục 5.7), cần nhóm thống nhất chính sách trước |
| `Create_DB_First_Time.sql` ↔ `Update_DB.sql`; các migration thành phần | §11.1 giữ file thành phần để truy vết; chưa thực thi trên SQL Server nên không xác minh được bản gộp |
| `Geography/README.md` ↔ `docs/checkout-address-catalog.md` | Bổ sung nhau: nguồn/checksum dữ liệu so với hợp đồng API |
| `docs/kiem-tra-loi-runtime-20261023.md` | Sự kiện riêng (lỗi runtime), không trùng báo cáo pull |

## 5. Ngoài phạm vi file mới (chỉ ghi nhận)

- `features/Farmer/FarmerDashboard.jsx`, `features/Guest/GuestDashboard.jsx`,
  `features/RetailCustomer/RetailCustomerDashboard.jsx` không còn được import ở đâu
  (code chết, §7). Đây là file của `main`; chỉ xóa khi nhóm đồng ý.
- `PageResult<T>` và `PagedResponse<T>` (đều thuộc `main`) giống hệt nhau.
- Code cũ còn nhiều dòng > 100 ký tự và `if` không có `{ }`; theo §1 nên xử lý ở PR format riêng.

## 6. Kết quả sau khi gộp

### 6.1. File thay đổi

| Nhóm | File bị xóa | File nhận nội dung / được sửa |
| --- | --- | --- |
| G1 | `Api/Security/ActorContext.cs`, `Api/Security/Authentication/HttpCurrentUser.cs` | `Api/Security/CurrentActor.cs`; đăng ký DI ở `Program.cs`, `Api/UseCaseRegistration.cs` |
| G2 | `Domain/Entities/HarvestBatch.Behavior.cs` | `Domain/Entities/HarvestBatch.Reservation.cs`; `ReservationExpiryService.cs`, `tests/Unit/BusinessRuleTests.cs` |
| G3 | `Infrastructure/Data/FeatureConcurrencyConfiguration.cs` | `Infrastructure/Data/AgriSustainContext.UseCases.cs` |
| G4 | `frontend/src/api/threeUseCases.js` | `frontend/src/api/client.js` (thêm `requestUseCase`); import ở `UsersScreen.jsx`, `CategoriesScreen.jsx`, `CheckoutScreen.jsx` |
| G5 | — | `Program.cs`: bỏ `AddHttpContextAccessor()`, `AddSingleton(TimeProvider.System)` lặp và `using Microsoft.EntityFrameworkCore;` không dùng |
| G7 | — | `Domain/Entities/RetailPricing.cs` (điều kiện chặt); `CartPricingService.UnitPrice` gọi `RetailPricing.UnitPrice` |
| Mục 5 | `features/Farmer/FarmerDashboard.jsx`, `features/Guest/GuestDashboard.jsx`, `features/RetailCustomer/RetailCustomerDashboard.jsx` | — |

Tổng cộng xóa 9 file. Không tạo file mới cho việc gộp.

### 6.2. Kiểm tra đã chạy

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet build AgriSustain/AgriSustain.csproj` | Thành công, 0 lỗi |
| Unit test | 27/27 đạt; gồm test sinh script EF model (G3), `UnitPrice_WholesaleThreshold_UsesCurrentPolicy` (G7), `Reserve_BatchStateAndQuantity_ProtectsInventoryInvariant` (G2) |
| Architecture test | 3/3 đạt |
| `npm run build` | Thành công |
| Khởi động backend Development (DI được kiểm tra khi khởi động) | Thành công; `/health/database` 200; marketplace trả 5 sản phẩm |
| API cần đăng nhập, không token (`/api/orders`, `/api/profile`, `/api/cart`, kể cả gửi `X-User-Id`) | 401 |
| Quét tham chiếu tới file đã xóa trong code C#/JS | Không còn |

### 6.3. Giới hạn và việc còn lại

- Chưa chạy luồng có đăng nhập (giỏ hàng, checkout, đổi mật khẩu) trên giao diện.
- Bổ sung sau báo cáo: đã sửa `tests/Integration/SqlUseCaseTests.cs`.
  1. Lỗi biên dịch CS0173: `var fee = … ? null : decimal` → `decimal? fee`.
  2. Test `Quote_UnsupportedAddress_PreservesCart` viết trước khi có danh mục địa giới và GHTK:
     dùng mã phường không tồn tại (nay trả `ADDRESS_INVALID`), và provider giả trả `null`
     (nay nghĩa là `SHIPPING_RATE_UNAVAILABLE`). Đã đổi sang phường có thật ngoài vùng phục vụ
     (`523`, Xã Ngọc Hồi) và cho provider giả trả `IsSupported = false` như GHTK.
  Integration test chạy trên DB tạm `AgriSustain_UcTests_<guid>` (tự xóa khi xong): 17/17 đạt.
  Toàn solution: 47/47 test đạt.
- Vì không gộp G6, Phụ lục A của `coding-standards.md`, `docs/three-use-cases-implementation.md`
  và `docs/bao-cao-chi-tiet-sua-code-20261023.md` vẫn nhắc tên cũ (`ActorContext`,
  `FeatureConcurrencyConfiguration.cs`, `api/threeUseCases.js`).
- Trạng thái Git: 5 file thuộc `main` đã được stage xóa (`HarvestBatch.Behavior.cs`,
  `FeatureConcurrencyConfiguration.cs`, 3 dashboard). `HttpCurrentUser.cs` bị xóa nhưng chưa stage.
  `ActorContext.cs` và `threeUseCases.js` vốn chưa được Git theo dõi. Các thay đổi khác chưa
  stage. Chưa commit, chưa push.
