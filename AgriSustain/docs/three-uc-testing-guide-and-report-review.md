# Hướng dẫn test và đối chiếu ba report

**Sau khi chuyển EF Core và sửa UI theo Report 3:** chưa chạy lại test chi tiết theo yêu cầu chờ duyệt code. Các kết quả/revision bên dưới là lịch sử trước lần chuyển này, không xác nhận bản EF/UI hiện tại. Xem [phạm vi kiểm tra mới](ef-core-database-first.md).

Ngày rà soát: 19/10/2026. Phạm vi: UC-ADM-03, UC-ADM-05, UC-RC-02.

## 1. Căn cứ và cách hiểu tài liệu

- `Foundations of software testing - ISTQB Certification.pdf`: chương 1.4 về quy trình,
  2.2 về test levels, 4.1 về condition/case/oracle/traceability, 4.3 về EP/BVA/decision
  table/state transition, 4.4 về các loại coverage. Số trang PDF đọc trực tiếp: 26–27,
  44–47, 80–83, 89–103, 107–109; không nhầm số trang PDF với số trang in trong sách.
- `Report-5.0_Test Documentation.docx`: Overall Plan, Test Strategy, Test Environment,
  Minimum Test Account/Domain Data, yêu cầu UT/IT tự chuẩn bị dữ liệu và không phụ thuộc
  dữ liệu từ lần chạy trước; phần Test Reports yêu cầu kết quả, thống kê, phân tích.
- Report 5.1/5.2/5.3 là biểu mẫu case và thống kê; Report 5.4 là NFR; Report 5.5 là UAT.
  HR Manager, Candidate, Staff, BF-01 trong ví dụ không tự trở thành yêu cầu AgriSustain.
- SRS/SDS và quyết định của người dùng xác định expected nghiệp vụ; code chỉ là test object.
  Quy tắc đã bị từ chối như category image/archive/Version/unique tên không được phục hồi
  chỉ vì tài liệu nguồn cũ còn ghi chúng.
- Project Tracking WBS hàng 10, 11, 13 là nguồn tên feature/screen/UC. `Consumer Ordering`
  giữ đúng tracking, còn actor là `RetailCustomer` theo quyết định cập nhật.

**Level** là phạm vi đối tượng (Unit, Integration, System, Acceptance).
**Type** là mục tiêu (functional, validation, security, performance, UI, background job).
Một type có thể xuất hiện ở nhiều level. Ma trận ví dụ trong Report 5.0 không cấm
validation ở UI hoặc kiểm tra security ở Unit khi có mục tiêu cụ thể.

## 2. Quy trình chung trước mỗi vòng test

1. Ghi build/commit, môi trường, mode dữ liệu, browser và tool version. Tách SQL thật khỏi
   mock; ghi policy nào chưa được duyệt. Seed SQL local để xem dữ liệu không phải setup
   dùng chung cho UT/IT tự động.
2. Chọn điều kiện theo rủi ro: quyền/đổi trạng thái, tiền, tồn, xóa reference, rollback.
3. Viết input, precondition, steps, expected và postcondition trước khi chạy.
   Expected bao gồm status/data/audit/reservation, không chỉ HTTP 200.
4. Chuẩn bị fixture trong từng test hoặc database thử riêng; không dùng ID tăng tự động
   của dữ liệu từ lần trước. Test có thể dùng ID cố định **bên trong fixture tự tạo**.
5. Chạy, lưu log và actual. Chỉ ghi Passed khi assertion đã thực thi và đúng.
   Không nhập ngày/tester cho case chưa chạy; chưa đủ điều kiện thì Pending kèm lý do.
6. Lỗi: tạo defect, ghi expected/actual và cách tái hiện; sửa rồi retest, sau đó regression
   các luồng có thể bị ảnh hưởng. Không đổi expected để khớp lỗi.
7. Tổng hợp cùng đơn vị đo: executed/planned cases khác requirement coverage và khác
   statement/branch coverage. Không dùng số test pass để suy ra đã hoàn thành mọi yêu cầu.

## 3. Unit / Component testing — Report 5.1

Mục tiêu: entity, validator và tính toán/cấu hình cô lập, không cần browser hay SQL.
Developer chạy bằng xUnit; test service có dependency thì dùng stub/mock có contract rõ.

Ví dụ ba UC:

- UserAccount: Active → Suspended → Active; old session version không hồi sinh;
  Unlock xóa lock/counter nhưng giữ business Status.
- Category: name 1/2/100/101; description 500/501; priority âm; parentId không hợp lệ.
- Checkout: phone hợp lệ/sai đầu số/sai độ dài; điểm âm; giữ đúng lượng tồn và phân bổ.
- DI guard: chặn đăng ký demo ngoài Development hoặc thiếu opt-in. Đây là component
  configuration test, dù file runner hiện nằm trong project có tên IntegrationTests.

Từ thư mục `AgriSustain`:

```powershell
dotnet test tests/Unit/AgriSustain.UnitTests/AgriSustain.UnitTests.csproj --configuration Release --logger trx --results-directory ../tmp/test-runs/unit
dotnet test tests/Integration/AgriSustain.IntegrationTests/AgriSustain.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName~AddInfrastructure_ProductionOrMissingOptIn_RejectsDemoRegistration" --logger trx --results-directory ../tmp/test-runs/component-configuration
```

Lệnh thứ nhất hiện có 22 case; lệnh thứ hai một case. Report Unit sau review phân loại
23 kết quả Passed và một case Pending cần chốt expiry convention; không nói project Unit
tự chạy ra 23 test. Một test framework/assembly không quyết định level của mọi case trong nó.

Thiết kế kỹ thuật:

- EP: chia input hợp lệ/không hợp lệ; chọn đại diện mỗi partition.
- BVA: thử ngay biên và giá trị sát hai phía. Với lượng tồn decimal, 6 khi stock=5 là
  đại diện vượt tồn, không chứng minh đã thử sát biên 5.001. Report sửa nhãn case này
  từ Boundary sang Abnormal; vẫn cần case sát biên theo độ chính xác số lượng đã duyệt.
- State transition: ghi trạng thái nguồn, event, trạng thái đích và side effect; thử cả
  chuyển sai trạng thái. Không gọi một phép validate chuỗi thông thường là state test.
- Decision table: lập điều kiện/kết quả cụ thể, chọn rule để test; không gắn nhãn chỉ
  vì tên case có chữ voucher/delete. Bản rà soát ghi rule minh họa ở mục 9.

Giới hạn: hiện chưa đo statement/branch coverage; số biên hoặc case pass không thay cho
coverage report. Case policy chưa chốt vẫn Pending, kể cả code hiện có một lựa chọn cụ thể.

## 4. Integration testing — Report 5.2

Mục tiêu: kiểm tra giao tiếp giữa các thành phần hoặc giữa hệ thống, theo từng phần.
API controller → validator/service → UoW/repository, auth pipeline và persistence là
các ranh giới khác nhau; ghi rõ adapter nào thực và adapter nào bị thay bằng fake.

Code hiện có: `ApiFactory.cs` tạo host Development và store riêng; `ScenarioUnitOfWork.cs`
chèn audit failure/price change; AdministrationTests/CheckoutTests gọi HTTP và đọc state
sau lệnh để kiểm tra audit, cart, tồn, điểm, quota và rollback.

```powershell
dotnet test tests/Integration/AgriSustain.IntegrationTests/AgriSustain.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName!~AddInfrastructure_ProductionOrMissingOptIn_RejectsDemoRegistration" --logger trx --results-directory ../tmp/test-runs/integration
```

Sau tách configuration component check, hiện có **28 kết quả HTTP component-integration
Passed** (không gồm bốn SqlPersistenceTests mới). Các case này hợp lệ cho phạm vi API/Application + demo adapters. Chúng không
chứng minh SQL transaction, cross-process concurrency, JWT signature/revocation hay carrier.

Muốn kiểm chứng SQL thật:

1. Tạo database thử tách biệt; dùng schema 47 bảng và fixtures tối thiểu.
2. Khởi tạo API bằng SQL adapter và đúng connection string của database thử.
3. Gọi HTTP; đọc lại bảng liên quan bằng một connection khác sau commit.
4. Inject lỗi sau business mutation nhưng trước audit; kiểm tra cả hai đã rollback.
5. Chạy hai process tranh last stock/quota, không chỉ hai Task chia cùng semaphore.
6. Restart app rồi kiểm tra persistence và retry theo contract DB-06 được duyệt.
7. Dọn database do test tự tạo. Không reset dữ liệu local người dùng để làm fixture.

**Ngày 19/10 đã bổ sung SQL adapter**, dùng cấu hình SqlDemo; bốn SQL integration tests
thực thi thành công trên database GUID riêng, tạo schema+seed và tự dọn. Đây là bằng chứng
HTTP→SQL thực, khác kiểm tra seed/schema. Hai API hosts có gate độc lập nhưng cùng OS process;
không thay case hai OS process. Chạy riêng bằng:

```powershell
$env:THREE_UC_SQL_TESTS='1'
dotnet test tests/Integration/AgriSustain.IntegrationTests -c Release --filter FullyQualifiedName~SqlPersistenceTests --logger trx --results-directory ../tmp/sql-runs
Remove-Item Env:THREE_UC_SQL_TESTS
```

Bốn case gồm: list/paging/escape + status/audit/restart; category list/CRUD/reference/hide;
audit-failure rollback toàn checkout rồi đặt quote sau restart; hai host tranh 5 kg tồn cuối.
Các case SQL gốc có expected rộng hơn không tự đổi Passed từ bằng chứng một phần.

DB-06, DB-09 và auth/policy chưa chốt vẫn là blocker cụ thể, không phải lỗi test runner.

## 5. System Functional testing — Report 5.3

Mục tiêu: hành vi người dùng xuyên React → HTTP API → persistence trong môi trường được
mô tả, theo use case. Người phụ trách test nên review độc lập expected và kết quả.

Từ `AgriSustain` build backend trước; từ frontend chạy:

```powershell
dotnet build AgriSustain.sln --configuration Release
npm --prefix frontend ci
npm --prefix frontend run build
npm --prefix frontend run test:e2e
```

Suite `frontend/tests/three-uc.spec.js` dùng Edge headless và tự bật/tắt server 5179/5173.
Trước khi chạy, dừng server test do mình quản lý hoặc chọn cổng riêng; không tự kill
process lạ hoặc Visual Studio của người dùng. Không để server test chiếm cổng sau khi xong.

Bốn kịch bản đã chạy: account Cancel/Suspend/Activate; category Create/Edit/Delete trống;
voucher feedback + order PendingPayment tổng 214.500; địa chỉ ngoài coverage giữ input
và chặn đặt. Expected được assert tại UI, không suy ra từ ảnh chụp hay build thành công.

Đây là **System FR trên bản demo**, chưa phải toàn bộ app dùng DB/JWT/payment thật.
18 case Pending còn gồm case có thể thực hiện thêm và case bị chặn bởi dependency.
Phải chạy chúng trước khi kết luận đủ chức năng. Không dùng bốn case làm UAT sign-off.

## 6. System NFR — Report 5.4 (hướng dẫn, chưa sửa report ngoài phạm vi)

### Security

- Xác định assets và boundary: quyền quản trị, quote của buyer khác, account bị ngưng,
  dữ liệu nhạy cảm, trực tiếp gọi endpoint thay vì chỉ nhìn nút ẩn trên UI.
- Thử no token/wrong role/other buyer; với auth thật thử expired/tampered token, phiên đã revoke.
- Kiểm tra response không chứa password/hash/token/stack trace; audit đủ actor/target.
- Chạy HTTPS trên staging theo cấu hình đã thống nhất; kiểm tra mixed content bằng DevTools.
- Demo Authorization chỉ kiểm tra fixture policy; không chứng minh JWT/HTTPS/security toàn hệ thống.

### Performance

- Chốt workload, dataset, concurrency, warm-up/duration, p95/p99 và error threshold từ SRS/
  test plan. Các con số mẫu `<2 giây`, `<10 giây`, `N user` trong template chưa phải SLA.
- Với list/search: dùng dataset đủ lớn, kiểm tra server-side paging khi SQL adapter có.
- Với checkout: tải hợp lệ, contention last stock/quota; tách lỗi nghiệp vụ dự kiến khỏi lỗi hệ thống.
- Dùng DevTools cho quan sát đơn, k6/JMeter cho tải có kiểm soát; lưu script/raw metrics/tool version.
- Runtime 7–8 giây của Playwright là thời gian chạy suite, không phải latency SLA của checkout.

Chưa có evidence NFR thật nên không ghi Passed hoặc tự dời sang v2 chỉ vì template có ví dụ.

## 7. Acceptance / UAT — Report 5.5 (hướng dẫn)

Người nghiệm thu/đại diện nghiệp vụ quyết định business outcome; developer hỗ trợ dữ liệu.
Môi trường phải đủ đại diện mục tiêu chấp nhận, và mọi giả lập phải được nêu trước.

| UC | Script phạm vi ba UC | Business outcome cần người dùng xác nhận |
| --- | --- | --- |
| UC-ADM-03 | Quản trị mở list, chọn account khác, hủy một lần, ngưng có lý do, kiểm tra trạng thái/audit, kích hoạt lại hoặc unlock tài khoản khóa | Đổi đúng tài khoản/trạng thái, có truy vết; phiên cũ theo auth contract đã thống nhất |
| UC-ADM-05 | Tạo/sửa category trống, xóa trống; category có product thử xóa rồi ẩn | Dữ liệu danh mục dùng được; không mất product/historical reference; không yêu cầu ảnh |
| UC-RC-02 | Buyer xem selected cart/address/summary, kiểm tra giá/phí/giảm giá; hủy rồi đặt và xác nhận; nhận mã đơn PendingPayment | Chỉ selected items đặt, tổng được chấp nhận, không bán vượt; chuyển tiếp payment qua contract |

Không thêm tạo Staff, approve/deliver order, QR/webhook hoặc tracking/refund vào ba UC
chỉ vì script mẫu có những bước đó. Có thể dùng exploratory charter theo screen và ghi
observation/feedback/defect; không điền UAT Passed thay cho người nghiệm thu.

## 8. Background jobs, regression và architecture

- Quote-expiry check trong `PlaceAsync()` là kiểm tra đồng bộ tại request, không phải
  bằng chứng worker tự release stock/điểm/voucher khi hết hạn. Worker thuộc module khác;
  chỉ test contract khi module đó có, đánh dấu Pending nếu chưa tích hợp.
- Retest chạy case từng fail sau fix. Regression chạy các case liên quan sau thay đổi;
  không tự điền Round 2/3 Passed khi chưa thực thi các vòng đó.
- Architecture test kiểm tra dependency assemblies, hiện có một Passed trong project riêng;
  không cộng vào UT/IT/ST-FR để tăng tỷ lệ, không coi nó là kiểm tra nghiệp vụ.

## 9. Rule/state minh họa làm cơ sở thiết kế case

### Category delete decision rules

| Rule | Confirmed | Có product bất kỳ | Có reference khác | Kết quả |
| --- | --- | --- | --- | --- |
| C1 | Không | Bất kỳ | Bất kỳ | Không xóa / không audit thành công |
| C2 | Có | Có | Bất kỳ | Chặn delete; cho phép ẩn theo state hợp lệ |
| C3 | Có | Không | Có | Chặn delete bởi child/policy/FK |
| C4 | Có | Không | Không | Delete + audit atomic |

Product chưa publish vẫn thuộc C2. `Delete_AnyProductReference...` cover biến thể đó;
`Delete_ChildOrPolicyReference...` cover C3; delete trống cover C4. Không tuyên bố đã
test mọi combination/audit-failure bằng một case success.

### Account states

| Source | Event | Result |
| --- | --- | --- |
| Active | Suspend + valid reason + confirm | Suspended; revoke theo auth contract |
| Suspended | Activate + valid reason + confirm | Active; không phục hồi old session |
| Active hoặc Suspended | Unlock khi LockedUntil > now | Giữ Status; lock/counter cleared |
| Suspended | Suspend lại | Conflict, không thêm audit thành công |
| Bất kỳ | Hủy / self-action | Không mutation |

Khóa tạm là metadata khác business Status; không gộp Locked và Suspended thành một trạng thái.

## 10. Đối chiếu bản report trước và bản sửa

| Hạng mục | Bản trước | Đánh giá / sửa |
| --- | --- | --- |
| Tên module/tab | ModuleName1, Feature Name1/2, Workflow Name1/2 | Chưa khớp tracking; đổi Administration / Consumer Ordering, giữ UC/screen chính xác |
| Kết quả chạy | 22 UT + 29 suite Integration + 4 E2E Passed, 27 Pending | Số log đúng; không tự nâng Pending thành Passed |
| DI guard trong IT | Một direct component/configuration test tính cùng IT | Chuyển report sang Unit/component: UT-RC02-011, alias IT-RC02-001; code vẫn ở IntegrationTests |
| Nhãn Boundary cho reserve(6), stock=5 | Gọi Boundary dù chưa là giá trị sát biên decimal | Sửa Abnormal/EP; không thay kết quả assertion |
| Technique | Gán heuristic từ tên test | Sửa ghi kỹ thuật theo condition thực; decision rules/state bảng trên hỗ trợ review |
| Execution coverage | Passed/executed trên planned cases | Hợp lý nếu ghi rõ denominator; không phải statement/branch/requirement coverage |
| Actual và evidence | Có method/date/build, nhưng một method Theory lặp cho nhiều input | Thêm runner container/source và input ở từng row, giữ ngày chạy thực 18/10 |
| Scope SQL | 8 case Pending ở build cũ | Bổ sung 4 case SQL thực Passed ở R2; cập nhật lý do còn Pending của case gốc, không tự nâng kết quả từ bằng chứng một phần |
| System FR | 4 demo browser flows Passed; 18 Pending | Dùng được cho tiến độ demo, chưa đủ kết luận system FR production complete |
| UAT/NFR | Không có evidence thực trong ba report | Không được suy ra đã nghiệm thu hoặc đạt security/performance |
| Traceability | Có UC/build; chưa có ma trận mọi requirement/BR | Chưa thể tuyên bố 100% yêu cầu; cần review BF/GBR cụ thể và thêm case còn thiếu |

Sau sửa cách phân loại, report Unit: **24 case = 23 Passed + 1 Pending**;
Integration: **36 case = 28 Passed + 8 Pending**; System FR: **22 case = 4 Passed + 18 Pending**.
Đây là baseline sau đổi nhãn: **82 case,55 Passed,27 Pending**; riêng việc phân loại không
sinh kết quả chạy mới.

**Bản giao v1.1, Round 2 ngày 19/10:** Unit 24=23 Passed+1 Pending;
Integration 40=32 Passed+8 Pending; System FR 22=4 Passed+18 Pending.
Tổng **86 case=59 Passed+27 Pending**. Có 4 case SQL mới;55case Passed lịch sử đã regression
lại (52 .NET gồm1 Architecture báo riêng, cộng 4 Edge). Summary trong workbook tính cột
**Round 2**, giữ cột Round 1 và ngày18/10. Round 3 chưa chạy; không nhập ngày/tester cho Pending.
R2 source là working tree19/10, SHA-256 manifest `tmp/three-uc-source-20261019.sha256.json`,
không gán nhầm code SQL mới vào commit905a14e. Kết quả: `tmp/report-r2-fixed-tests/*.trx`,
`tmp/sql-r2-final-list/*.trx`, `tmp/test-report-work/e2e-r2-result.json`.

Các phần gốc vẫn Pending cụ thể: lỗi audit riêng account; direct FK/category audit rollback;
hai OS process; tranh quota cuối; wrong-owner/expiry quote SQL với TTL đã duyệt; JWT thật;
DB-06 retry bền vững; DB-09 point reservation. Có SQL adapter không đồng nghĩa đã thực thi các biến thể đó.

Architecture một Passed được báo riêng. Các kết quả lịch sử gắn build `905a14e`, ngày
18/10/2026; ngày sửa tài liệu 19/10 không được dùng làm test execution date.

**Kết luận:** báo cáo trước phản ánh khá đúng tiến độ bản demo và không giả mạo kết quả,
nhưng chưa hoàn chỉnh để nghiệm thu. Sau sửa tên/phân loại/nhãn kỹ thuật, chúng phù hợp
cho báo cáo tiến độ có giới hạn; cần chạy pending cases, tích hợp thật và review coverage
yêu cầu trước khi tuyên bố ba UC hoàn tất hoặc đủ điều kiện release.

## 11. Phát hiện trong lượt rà soát và trạng thái file giao

- Sửa fixture API cũ: thay rõ IThreeUcUnitOfWork và IDemoIdentityReader bằng InMemory,
  không chỉ ghi configuration khi DI đã chạy. Lượt đầu bị ảnh hưởng seed demo đã được
  backup/hoàn nguyên; kết quả thất bại cũ không được đưa vào cột Passed của lượt sau.
- Sửa thiếu khoảng trắng khi nối SQL SELECT/filter, bổ sung assertion list/paging và keyword
  có ký tự phần trăm. Browser kiểm tra lại dữ liệu SQL sau sửa.
- User self-action trên UI dùng actor ID từ server, không hardcode1.
- Module feature trong hai tab mỗi workbook và index thống kê khớp tracking. Role giữ RetailCustomer.
- Các report giao là **local XLSX** trong `outputs/three-uc-test-reports`. Google Sheets
  gốc **chưa được cập nhật trực tuyến** vì Google Drive connector chưa kết nối; không
  tuyên bố bản local đã đồng bộ lên Google. Người dùng có thể import ba XLSX đã kiểm tra.
- Tên/trạng thái/formula/date/link điều hướng đã kiểm tra bằng đọc lại workbook; source
  security template chưa dùng giữ nguyên, không nhập Passed cho NFR/UAT chưa thực thi.
