# Kiểm tra code sau cập nhật database

Cập nhật EF Core Database First: [cấu hình và scaffold](ef-core-database-first.md), [đối chiếu màn hình Report 3](three-uc-srs-screen-review.md). Bản EF/UI mới chỉ build và xem nhanh; kết quả test chi tiết cũ chưa xác nhận bản mới.

Ngày 18/10/2026. Phạm vi chỉ UC-ADM-03, UC-ADM-05, UC-RC-02 và các adapter được cho phép
giả lập. Comment trong code bằng tiếng Anh theo coding-standards.md; tài liệu này giải thích
lại bằng tiếng Việt để nhóm biết thứ tự đọc và chỗ cần thay khi tích hợp.

## 1. Các điểm cũ đã sửa

| Phát hiện | Kết quả sửa |
| --- | --- |
| Category còn ảnh, ngày tạo không có trong DB, archive và Version | Bỏ khỏi entity/DTO/validator/API/UI; xóa adapter ảnh và dependency SkiaSharp không còn dùng |
| Code còn chặn trùng tên/cycle/Version dù người dùng không chọn DB-02/03/04 | Không áp các quy tắc ghi này; parent vẫn phải tồn tại như FK gốc. Checkout giữ visited-set để dữ liệu cây lỗi không gây vòng lặp vô hạn khi đọc |
| Xóa danh mục chỉ kiểm tra product active | Kiểm tra mọi product, kể cả chưa publish/chưa approve; có product thì chỉ ẩn/khóa. Không có product vẫn phải kiểm tra child/benchmark/commission references |
| User request/response còn Version | Bỏ Version; vẫn kiểm tra trạng thái hiện tại, confirmation, reason và self-action. SessionVersion chỉ là mô phỏng thu hồi phiên, không phải cột SQL Version |
| Role còn Consumer/Administrator chung | Dùng RetailCustomer/SystemAdmin; thêm fixture CustomerSupport để chứng minh không tự được quyền quản trị khi ma trận chưa chốt |
| Giỏ chọn sẵn batch, một dòng chỉ có một lô | CartLine chỉ giữ product/số lượng. Một OrderLineSnapshot chứa nhiều BatchAllocationSnapshot; phân bổ FEFO, tính giá bậc theo lượng của cả dòng, giữ tồn tổng từng batch |
| Nhiều dòng giỏ cùng product có thể tranh cùng tồn lúc preview | Dùng remaining theo BatchId xuyên suốt quá trình phân bổ, không cấp lại cùng số tồn cho dòng tiếp theo |
| Địa chỉ ba cấp/tên tự nhập | Tỉnh–xã theo code, dropdown liên kết; backend kiểm tra parent_code và lấy tên/phiên bản từ JSON tin cậy, không nhận tên client làm nguồn sự thật |
| Comment mock còn nói DB chưa sửa/địa giới chỉ so tên | Cập nhật comment về schema đã áp, phạm vi mock, shipping khác commission, policy/contract còn chưa chốt |

## 2. Hướng đọc hàm và hợp đồng

1. `Api/Program.cs`, `ApplicationServiceCollectionExtensions.cs` và
   `InfrastructureServiceCollectionExtensions.cs`: đọc nơi đăng ký DI, role/policy và guard
   demo. Controller mỏng chuyển request/Result, không chứa SQL hoặc công thức tính tiền.
2. `Application/Services/Administration/AccountStatusService.cs`: Search/Get/Change kiểm
   actor/target và ghi audit cùng thay đổi trạng thái; Domain UserAccount bảo vệ transition.
3. `Application/Services/Administration/CategoryService.cs`: Save chỉ dùng field DB đã có;
   Change phân biệt hide/activate/delete, bảo vệ references và audit. Interface repository
   ghi rõ adapter SQL phải kiểm tra những bảng nào.
4. `Application/Services/Orders/CheckoutService.cs`: Preview → Calculate → AllocateBatches;
   Place tái tính fingerprint trước khi giữ từng batch. BuildSellerOrders/Split phân bổ
   discount/phí và phần dư làm tròn. Money/Today ghi rõ quy tắc demo/múi giờ.
5. `Infrastructure/Data/Geography/VietnamAddressCatalog.cs`: đọc JSON object theo mã,
   kiểm tra cặp tỉnh/xã, thay tên client và tạo dataset hash. Đây là catalog thật, không
   phải xác nhận carrier giao được. Service area/phí vẫn là adapter mẫu.
6. `Infrastructure/Repositories/Demo/DemoSession.cs`, `UnitOfWork/DemoUnitOfWork.cs`:
   mọi write/audit/reservation chung snapshot; chỉ publish khi Result thành công.
   Semaphore chỉ bảo vệ một process, không thay SQL transaction nhiều instance.
7. Frontend `UsersScreen`, `CategoriesScreen`, `CheckoutScreen`: JSDoc giải thích mục đích
   từng hàm; comment hook/callback giải thích abort request, vô hiệu quote và trạng thái
   xác nhận. Shared client giữ code/status lỗi để phục hồi đúng luồng.

## 3. Độ phủ comment và cách kiểm tra

- 231 declaration C# gồm method public/private, interface, constructor tường minh/primary
  constructor và test helper: đều có XML summary. Roslyn parse source để kiểm tra, không
  dùng regex nhận diện method C#.
- 25 hàm JavaScript có tên gồm component/helper và named arrow: đều có JSDoc. Audit nhỏ
  của frontend kiểm tra những declaration này, không tuyên bố parse tổng quát mọi JavaScript.
- Anonymous LINQ/validation/JSX callbacks được giải thích trong hàm/hook chứa chúng;
  getter/setter tự sinh và method do compiler tạo không được tính thành hàm viết tay.
- Comment nêu dữ liệu, điều kiện, side effect, rollback và cách thay mock; không coi
  comment là bằng chứng chức năng đã tích hợp SQL hoặc payment thật.

Từ thư mục AgriSustain:

```powershell
dotnet run --project tools/CommentAudit -- check .
./tools/check-frontend-comments.ps1
dotnet format AgriSustain.sln --no-restore --verify-no-changes
dotnet test AgriSustain.sln --configuration Release
```

## 4. Kiểm thử hồi quy

- 22 unit + 29 API/Application với demo adapters + 1 architecture = **52 .NET test đạt**.
  Bổ sung 13 case validator cho report: biên tên/mô tả danh mục, reason/action tài khoản,
  partition/độ dài phone và điểm âm. Đây là input contract hiện tại, không thay policy.
- **4 Edge headless E2E đạt**, không retry: account cancel/confirm, category create/edit/
  delete, voucher và đặt đơn, địa chỉ hợp lệ nhưng ngoài service area mẫu.
- Test mới bao gồm tham chiếu product chưa publish vẫn chặn xóa, policy/child references,
  audit fail khôi phục category đã xóa, FEFO 3+2 kg trên hai lô, cùng product nhiều dòng
  không cấp tồn hai lần, cặp tỉnh/xã sai bị chặn và tên client bị thay bằng tên từ catalog.
- Dữ liệu test là fixture; không tạo đơn hoặc seed account vào database AgriSustain thật.

## 5. Phần chưa được suy đoán hoặc triển khai sang UC khác

Khi nhóm viết adapter SQL, cần mapping tường minh thay vì suy đoán tên property = cột:

| Read model / entity | Cột hoặc bảng SQL |
| --- | --- |
| UserAccount.Name / Phone | User.FullName / PhoneNumber |
| Category.Name / ParentId | Category.CategoryName / ParentCategoryId |
| AuditRecord.ActorId / TargetId | AuditLog.PerformedByUserId / EntityId; TraceId lấy từ tracing context đáng tin cậy |
| RetailOrder.Subtotal / Total | Order.TotalAmount (tiền hàng) / FinalAmount (phải trả) |
| VoucherDiscount + PointsDiscount | Order.DiscountAmount; lưu riêng VoucherDiscount/PointDiscount, không gộp mất nguồn giảm |
| Address.CatalogVersion | Order.AddressCatalogVersionSnapshot; snapshot code/tên địa giới đi cùng đơn |
| OrderLineSnapshot.Allocations | Các hàng OrderItemBatchAllocation của cùng OrderItem |
| SellerId / SellerUserId | FarmerProfileId / UserId tương ứng; không dùng việc ID fixture tình cờ bằng nhau để map production |

API vẫn dùng DemoUnitOfWork; **chưa đọc/ghi LocalDB**. DB đã có 47 bảng là bước riêng.
Việc thay adapter SQL cần mapping/transaction và kiểm thử thực, không chỉ thêm connection
string vào appsettings. Không gỡ guard demo hoặc công bố API đã lưu dữ liệu thật.

- DB-06: request key vẫn là cơ chế demo hiện có, được comment rõ chưa có cột SQL tương
  ứng. Chưa tự tạo unique quote/order hoặc coi phương án đó được duyệt.
- DB-09: số dư/giữ điểm là mock; không tự thêm RewardAccount/RewardPointReservation và
  không mặc định tỷ lệ 100 đồng/điểm, cap 20% thành policy được duyệt.
- Auth: SystemAdmin là quyền fixture tạm; CustomerSupport không tự kế thừa mọi quyền.
  Không viết login/refresh/role management hoặc ma trận mới thay cho nhóm auth.
- Thời hạn 5/15 phút, service area mã 11/12 và shipping fee là cấu hình demo có comment.
  Phí giao dịch thường 2% khác shipping; không cộng 2% vào tiền khách khi bên chịu phí chưa chốt.
- Null expiry được xếp sau lô có hạn trong fallback FEFO demo; nhóm inventory cần xác
  nhận policy này. Tồn hết hạn/không bán được bị loại khỏi phân bổ.
- Cart CRUD, quản lý product/voucher/điểm, gateway/QR, webhook, worker expiry và refund
  vẫn thuộc module khác; ba UC chỉ tiêu thụ các hợp đồng cần thiết.

## Cập nhật SQL 19/10/2026

Đã nối EfThreeUcUnitOfWork/EfThreeUcSession cho đúng ba UC. Bảng và luồng chi tiết ở
[luồng theo file](three-uc-file-flows.md). Các phát biểu chưa SQL ở phần lịch sử18/10
không còn mô tả runtime Development hiện tại. Auth/points/fee/retry vẫn mock, có giới hạn
restart và hướng thay trong XML/JSDoc. Audit comment:288 khai báo C# có tài liệu,0 thiếu;
25 hàm frontend có JSDoc,0 thiếu. Bộ52test cũ và4SQL/4E2E đã đạt sau sửa lỗi tìm/phân trang.
