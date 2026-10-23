# Tests

## Detailed run — 19 October 2026

Latest backend run: **121 passed, 0 failed, 0 skipped**: 33 Unit, 76 cases in the
HTTP component test container (including one direct DI configuration component),
11 actual SQL cases and one Architecture case. The report classifies that DI case
under Unit/component: Report 5.1 has34 cases and Report 5.2 has86.

`DetailedBoundaryTests`, `DetailedThreeUcTests` and `DetailedSqlTests` extend adjacent
boundaries, invalid offers/cart, consistent cart/quote pricing, SQL audit rollback,
real FK, exact quote expiry, safe database-unavailable responses and contention
between **two distinct operating-system API processes** for final stock/voucher quota.
Database/session guards reject any connection outside the exact generated test catalog.

Desktop Edge checks exercised31 functional checkpoints over the three UC flows on a
separate SQL database. Twelve local security checks and four100-request HTTP burst
measurements passed. UAT business sign-off, integrated auth/payments/rewards and complete
staging/mobile/load coverage remain open. Existing historical results below are not
the current EF/UI test result. No statement/branch coverage percentage is claimed.

The Playwright selectors were aligned with the current UI; its servers now use5193/5194.
The existing CLI Playwright suite was **not rerun** in this detailed run. The browser
evidence was captured through actual desktop Edge interactions/assertions against SQL.
Keep5179/5181 for the user's application. Vite accepts `AGRI_API_ORIGIN` and `AGRI_WEB_PORT`
for an isolated manual environment; ordinary dev defaults remain5179/5173.

Các project xUnit đã có:

- Unit/AgriSustain.UnitTests: domain invariant và validator.
- Integration/AgriSustain.IntegrationTests: pipeline HTTP/Application với demo adapters,
  mỗi test tự tạo server/fixtures. Các case cũ ép InMemory; nhóm SqlPersistenceTests riêng kiểm chứng SQL, không có JWT thật.
- Architecture/AgriSustain.ArchitectureTests: chiều phụ thuộc assembly.
- ../frontend/tests/three-uc.spec.js: bốn luồng trình duyệt cho ba UC qua API localhost.

Từ thư mục solution: `dotnet test AgriSustain.sln --configuration Release`.
Từ frontend: `npm run test:e2e` sau khi backend đã build Release và Edge đã cài;
để trống port 5193/5194 vì suite tự chạy/dừng server riêng.

Xem [phạm vi và giới hạn kiểm chứng](../docs/three-uc-demo-and-integration.md).

Sau review DB: xem [code/comment và kết quả kiểm tra](../docs/three-uc-code-review.md).
Các hàm đã có XML/JSDoc; contract mới bỏ ảnh/archive/Version, dùng RetailCustomer và FEFO.

## SQL integration opt-in

```powershell
$env:THREE_UC_SQL_TESTS='1'
dotnet test tests/Integration/AgriSustain.IntegrationTests -c Release --filter FullyQualifiedName~SqlPersistenceTests --logger trx --results-directory ../tmp/sql-runs
Remove-Item Env:THREE_UC_SQL_TESTS
```

Mỗi SQL test tự tạo database `AgriSustain_ThreeUcTest_<GUID>` từ schema+seed và tự dọn;
không dùng AgriSustain/AgriSustainDB. Cần LocalDB và sqlcmd. Không bật biến thì bốn SQL
case Skip có lý do; không được ghi chúng Passed. Hai API host trong concurrency test có
mock gate độc lập nhưng cùng OS process, chưa thay case hai OS process độc lập.
Bằng chứng 19/10:22Unit+29suiteIntegration+1 Architecture cũ đạt,4SQL đạt,4Edge E2E đạt.
Report phân loại DI guard vào component nên số theo report khác tên runner assembly.
Xem [hướng dẫn và review report](../docs/three-uc-testing-guide-and-report-review.md).
