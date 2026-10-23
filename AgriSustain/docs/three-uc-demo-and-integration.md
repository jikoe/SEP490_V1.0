# Chạy và tích hợp ba UC — phiên bản sau review code

Cập nhật EF Core Database First: [cấu hình và scaffold](ef-core-database-first.md), [đối chiếu màn hình Report 3](three-uc-srs-screen-review.md). Bản EF/UI mới chỉ build và xem nhanh; kết quả test chi tiết cũ chưa xác nhận bản mới.

Ngày 18/10/2026. Chỉ UC-ADM-03, UC-ADM-05, UC-RC-02. Quyết định người dùng trong
database-decisions-round-3.md được ưu tiên hơn nội dung SRS/SDS cũ về ảnh/archive/Version
và batch chọn trong giỏ. Xem [kiểm tra hàm/comment](three-uc-code-review.md).

## 1. Trạng thái chạy

Database `(localdb)\MSSQLLocalDB / AgriSustain` đã có 47 bảng, xem
[bộ SQL](../database/README.md). Cập nhật 19/10: Development mặc định SqlServer qua EF Core, ba UC đọc/ghi
SQL bền vững. Auth, điểm, phí/phạm vi giao, retry cache vẫn giả lập; restart reset các mock.
Địa giới dùng JSON local thật. [Seed/run chi tiết](../database/demo-data-and-web.md).

Không có login, cart CRUD, product/voucher/loyalty management, QR/webhook, expiry worker
hoặc refund. Không đăng ký demo trong Production hoặc tự bỏ Development guard.

## 2. Chạy và test

Từ AgriSustain, dùng .NET SDK theo global.json và Node tương thích lockfile:

```powershell
dotnet restore AgriSustain.sln
npm --prefix frontend ci
npm --prefix frontend run build
dotnet run --project src/AgriSustain.Api --configuration Release
```

Mở http://localhost:5179. Visual Studio: chọn AgriSustain.Api, profile Development, F5.
Chỉ chạy một API trên port 5179; dừng terminal demo trước khi F5 để tránh lỗi trùng cổng.
Phát triển frontend: `npm --prefix frontend run dev`, URL http://127.0.0.1:5173.
Suite E2E tự chạy/dừng hai server, cần port 5179/5173 trống.

```powershell
dotnet test AgriSustain.sln --configuration Release
npm --prefix frontend run test:e2e
dotnet run --project tools/CommentAudit -- check .
./tools/check-frontend-comments.ps1
```

## 3. Fixture và quyền

| Header | ID / role | Phạm vi |
| --- | --- | --- |
| Authorization: Demo admin | SQL username demo3uc_admin / SystemAdmin | Hai UC quản trị; không tự thao tác trạng thái chính mình |
| Authorization: Demo retail-customer | SQL username demo3uc_retail / RetailCustomer | Giỏ sản phẩm mẫu hai seller và địa chỉ đã lưu |
| Authorization: Demo other-retail-customer | SQL username demo3uc_retail_other / RetailCustomer | Giỏ đối chứng để kiểm tra ownership/stock cạnh tranh |
| Authorization: Demo support | SQL username demo3uc_support / CustomerSupport | Không tự được cấp quyền hai UC quản trị; chờ ma trận auth được chốt |

Fixture selector không phải credential. SQL ID được lookup bằng username, không cố định.
Danh sách seed cụ thể và voucher `DEMO3UC-WELCOME10` / `DEMO3UC-RAUXANH20` xem hướng dẫn seed.
SessionVersion chỉ là trạng thái thu hồi phiên mock, không phải cột SQL Version.
Tỷ lệ điểm/cap 20%, vùng phục vụ 11/12, shipping 30.000/60.000 + seller thêm 10.000 và hạn
5/15 phút vẫn là fixture. Commission thường 2% đã ghi nhận nhưng khác shipping; chưa tự
cộng vào tiền khách hoặc tạo policy gói khi cơ sở/bên chịu phí chưa chốt.

## 4. Hợp đồng dữ liệu mới

- SaveCategoryRequest: name, parentId, description, priority, isActive, confirmed. Không
  có ảnh, createdAt, archive hoặc Version. Tên trùng được phép theo lựa chọn bỏ DB-02.
- CategoryActionRequest chỉ có confirmed. Delete kiểm tra mọi product và liên kết khác;
  không có product chưa đủ nếu còn child/benchmark/commission references. IsActive=false
  phục vụ ẩn/khóa theo quy tắc hiện tại, không đổi product status hàng loạt.
- AccountActionRequest: action, reason, confirmed; không Version. Vẫn kiểm tra self-action,
  trạng thái hiện tại và actor Active/unlocked; không tự cấp quyền theo tiền tố Admin.
- CartLine: id, productId, quantity, selected, không BatchId. OrderLineSnapshot giữ một
  dòng sản phẩm với Allocations gồm batchId/batchCode/quantity; tổng lượng allocation
  phải bằng lượng dòng. FEFO dùng remaining dùng chung để tránh cấp tồn hai lần.
- AddressSnapshot: recipientName, phone, province, ward, detail, provinceCode, wardCode,
  catalogVersion. Không District. Mã 12/268 là Hồ Chí Minh/Thạnh An trong bộ JSON đã chọn;
  không đồng nhất với mã của carrier khác. Server thay tên/version theo catalog.
- Province/ward hợp lệ vẫn phải vượt qua policy service area. Province 13/ward 269 là
  địa chỉ hợp lệ trong catalog nhưng ngoài hai province-code fixture đang phục vụ.
- Quote fingerprint chứa địa chỉ chuẩn, money, seller/product và phân bổ batch; Place
  tính lại trước giữ hàng. Khác giá/phân bổ phải xem lại quote, không âm thầm thay lô.

## 5. API hiện tại

| Method / route | Nội dung |
| --- | --- |
| GET /api/users | Tra cứu/lọc/phân trang tài khoản |
| GET /api/users/{id} | Chi tiết tài khoản được phép xem |
| POST /api/users/{id}/status-actions | suspend/activate/unlock, reason, confirmed |
| GET /api/categories | keyword, isActive, parentId, rootOnly, page, pageSize |
| POST /api/categories | Tạo danh mục bằng các field đã chốt |
| PUT /api/categories/{id} | Sửa field hiện có và xác nhận thay đổi trạng thái |
| POST /api/categories/{id}/activate hoặc deactivate | confirmed |
| POST /api/categories/{id}/delete | Xóa vật lý sau khi kiểm tra references, cùng transaction audit |
| GET /api/checkout | Context thuộc RetailCustomer hiện tại |
| GET /api/checkout/provinces | 34 tỉnh/thành từ catalog |
| GET /api/checkout/provinces/{code}/wards | Xã/phường theo parent_code |
| POST /api/checkout/quotes | Input đã chuẩn hóa, FEFO allocation và summary/expiry/warning |
| POST /api/checkout/orders | quoteId, requestKey demo, confirmed; 201 mới hoặc 200 retry demo |

Không còn route archive, upload ảnh hoặc filter includeArchived. Request key là mô phỏng
đang có để thử luồng retry; SQL chưa có cột tương ứng vì DB-06 chưa chốt. Không dùng cơ
chế này làm cam kết idempotency bền vững hoặc nhiều server.

## 6. Chỗ nhóm cần thay mock

| Adapter | Hợp đồng cần giữ / việc cần thay |
| --- | --- |
| DemoAuthenticationHandler/HttpCurrentUser/DemoUnitOfWork.FindAsync | JWT/session thật và ma trận permission/target, vẫn recheck trạng thái và ownership; chưa viết auth UC |
| EfThreeUcUnitOfWork/EfThreeUcSession | Đã có SQL paging/transaction/identity/audit/reservation; production cần review lock ordering, timeout và throughput |
| SQL cart/catalog read seam | Đọc bảng thật, chỉ xóa selected cart trong Place; module cart CRUD/product management vẫn ngoài scope |
| SQL category references | Đã query child/Product/RegionalPriceBenchmark/CommissionPolicy thật, giữ NO ACTION FK |
| VietnamAddressCatalog | Đã dùng JSON thật. Khi cập nhật dữ liệu phải giữ source/license/hash, không tự đổi snapshot đơn cũ hoặc đoán mapping từ tên |
| DemoCheckoutPolicy delivery | Coverage/carrier/fee/ETA thật; mã địa giới catalog không tự là mã carrier |
| Points mock / SQL voucher | DB-09 còn chờ; voucher đọc SQL, ghi Reserved có expiry và quota UsedCount+liveReserved; apply/release/payment worker chưa triển khai |
| Demo retry | Chốt DB-06 trước khi viết SQL lookup/unique; không tự giả định RequestKey/RequestHash tồn tại |
| GetPaymentDestination | Null tới khi module thanh toán cung cấp handoff; không tạo QR hoặc gọi PayOS ở UC-RC-02 |
| Money/durations | Chốt rounding, cap, stacking và các thời hạn; không xem cấu hình fixture là đồng ý nghiệp vụ |

Mỗi hàm trong các abstraction/adapter/service có XML summary; các hàm frontend có
JSDoc. Comment DEMO-* và [review code](three-uc-code-review.md) nêu rõ giới hạn/thay thế.
Không suy từ test demo đạt thành persistence/JWT/payment thật đã hoàn thành.
