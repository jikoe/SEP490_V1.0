# QUY TẮC KIỂM THỬ — AGRISUSTAIN

Phiên bản 1.0 — ngày 18/10/2026.

Phạm vi: backend ASP.NET Core .NET 8, React SPA, SQL Server và các tích hợp của AgriSustain.
Đối tượng sử dụng: developer, người phụ trách kiểm thử, reviewer và người nghiệm thu nghiệp vụ.

## 1. Cơ sở và mức độ áp dụng

Bộ quy tắc này được đề xuất từ sáu tài liệu Report 5.0–5.5, kết hợp với
[coding-standards.md](coding-standards.md), cấu trúc code và
[ma trận quyền của dự án](Permission_Matrix_AgriSustain.md).

- **BẮT BUỘC**: các quy định trong tài liệu khi áp dụng cho dự án, trừ nội dung ghi rõ là
  khuyến nghị, đề xuất tổ chức/công cụ hoặc cần chốt.
- **KHUYẾN NGHỊ**: lựa chọn mặc định; có thể thay bằng cách phù hợp hơn và ghi rõ lý do.
- **CẦN CHỐT**: thông số hoặc quyết định chưa có căn cứ yêu cầu chính thức. Phải xác định
  người quyết định và thời hạn trước mốc kiểm thử liên quan; chưa được dùng để kết luận đạt.
- Ngoại lệ phải ghi phạm vi, lý do, rủi ro, người chấp nhận và hạn xử lý trong Test Plan/PR.
  Người viết test không tự loại bỏ test thất bại để đạt điều kiện hoàn thành.

Các file nguồn là biểu mẫu có hướng dẫn và ví dụ. Những dòng HR Manager, Candidate,
Customer/Staff, số liệu mẫu và trạng thái mẫu không được coi là yêu cầu hay kết quả của
AgriSustain. Các quy định bổ sung trong tài liệu này là đề xuất cho nhóm, không phải trích
nguyên văn hoặc xác nhận rằng nhóm đã phê duyệt hay triển khai.

## 2. Nguyên tắc chung

1. Kiểm thử theo requirement, acceptance criteria, business rule và rủi ro. Mỗi assertion
   phải chứng minh một hành vi có ý nghĩa, không chỉ chạy qua một dòng code.
2. Viết test ở cấp thấp nhất đủ chứng minh hành vi. Lỗi ràng buộc SQL cần integration test;
   trải nghiệm nhiều màn hình cần system test; chấp nhận nghiệp vụ cần UAT.
3. Không ép số lượng test hoặc tỷ lệ UT/IT/ST cố định. Ưu tiên tiền, tồn kho, quyền truy cập,
   chuyển trạng thái và dữ liệu riêng tư.
4. Test tự động phải lặp lại được, độc lập với thứ tự chạy và có dữ liệu tự chuẩn bị.
5. Expected result phải có trước khi chạy test. Không đổi expected chỉ để khớp actual;
   thay đổi requirement cần được thống nhất và lưu lịch sử.
6. Mỗi bug đã xác nhận phải có regression test ở cấp phù hợp khi khả thi. Nếu chỉ kiểm tra
   thủ công được, lưu kịch bản tái hiện và đưa vào checklist hồi quy.
7. Build thành công, API trả 200 hoặc một ảnh chụp màn hình không đủ chứng minh nghiệp vụ đúng.
8. Chỉ ghi Passed sau khi đã thực thi trên build xác định và có bằng chứng kiểm tra.

## 3. Phân chia cấp độ và trách nhiệm

| Cấp độ | Phạm vi chính | Người chịu trách nhiệm | Bằng chứng tối thiểu |
| --- | --- | --- | --- |
| Unit — UT | Entity, invariant, validator, tính toán và application service được cô lập dependency | Developer | Test code, assertion, kết quả runner |
| Integration — IT | Ranh giới API/Application/Infrastructure, SQL Server, transaction, auth pipeline và adapter | Developer; người phụ trách test rà soát rủi ro | Request/response, trạng thái lưu trữ, kết quả runner |
| System FR — ST-FR | Luồng chức năng xuyên React/API/DB trên bản triển khai tích hợp | Người phụ trách test; developer hỗ trợ | Steps, actual, ảnh/trace khi cần, build và môi trường |
| System NFR — ST-NFR | Security toàn hệ thống, hiệu năng và các thuộc tính chất lượng đã cam kết | Người phụ trách test và hạ tầng | Cấu hình phép đo, dữ liệu gốc, threshold và kết luận |
| Acceptance — UAT | Kết quả nghiệp vụ mà từng actor cần đạt | Đại diện nghiệp vụ hoặc người nghiệm thu được chỉ định | Script, kết quả, feedback và xác nhận |
| Architecture | Chiều phụ thuộc và ranh giới tầng | Developer/reviewer | Test tự động hoặc biên bản review trước khi có test project |

Functional, validation, security và background job là **loại kiểm thử**; UT/IT/ST/UAT là
**cấp độ**. Một loại có thể được kiểm tra ở nhiều cấp với mục tiêu khác nhau.
Ma trận trong Report 5.0 là điểm xuất phát, không phải lệnh cấm kiểm tra validation ở UI
hoặc kiểm tra kết quả worker trong luồng hệ thống.

Mỗi nhóm có thể kiêm nhiệm nhiều vai trò. Người nghiệm thu và quyền quyết định release
phải được ghi tên trong kế hoạch, không tự mặc định là tác giả code.

## 4. Test Plan trước mỗi đợt kiểm thử

Report 5.0 phải xác định được:

- Phạm vi requirement/feature được test, ngoài phạm vi và lý do.
- Cấp độ, loại test, kỹ thuật, người phụ trách và tiêu chí kết thúc cho từng nhóm.
- Commit/build, cấu hình môi trường, DB/schema, phiên bản công cụ và trình duyệt mục tiêu.
- Cách chuẩn bị/reset dữ liệu, tài khoản theo role, quyền truy cập và dịch vụ sandbox.
- Mốc viết case, review, chạy, sửa lỗi, retest, regression và UAT.
- Rủi ro, giả định, phụ thuộc và cách xử lý khi môi trường/provider không sẵn sàng.
- Điều kiện bắt đầu/kết thúc, mức lỗi chặn và người chấp nhận phần hoãn kiểm thử.

Không tự chuyển load test, security test hoặc luồng quan trọng sang phiên bản sau chỉ vì
biểu mẫu có câu ví dụ cho phép deferred. Nếu đó là yêu cầu đã cam kết, phải có quyết định
thay đổi phạm vi và rủi ro được chấp nhận.

## 5. Định danh và truy vết

### 5.1 Mã test

Chuẩn đề xuất:

```text
UT-<AREA>-<NNN>       Ví dụ UT-ORD-001
IT-<AREA>-<NNN>       Ví dụ IT-PAY-001
ST-FR-<AREA>-<NNN>    Ví dụ ST-FR-ORD-001
ST-NFR-SEC-<NNN>     Ví dụ ST-NFR-SEC-001
ST-NFR-PERF-<NNN>    Ví dụ ST-NFR-PERF-001
UAT-<FLOW>-<NNN>      Ví dụ UAT-RETAIL-001
ARCH-<NNN>           Ví dụ ARCH-001
```

- Mã duy nhất trong toàn bộ bộ hồ sơ, không khởi động lại ở từng sheet và không tái sử dụng.
- AREA/FLOW có danh mục chung. Không dùng tên sheet hoặc số hàng làm khóa định danh.
- Di chuyển/đổi tên test không làm mất lịch sử và liên kết defect.

### 5.2 Ma trận truy vết

Mỗi requirement trong phạm vi phải liên kết tới test ở cấp phù hợp. Bản ghi cần có
`Requirement ID`, `UC/BF ID nếu có`, `Test Case ID`, `Level`, `Priority`, `Automation/Test name`
và kết quả của test run liên quan. Một case có thể liên kết nhiều requirement nếu thật sự
chứng minh được từng requirement; không gán hàng loạt để tăng tỷ lệ bao phủ.

Nguồn expected gồm SRS, acceptance criteria, business rule, permission matrix và API contract
đã được thống nhất. Khi các nguồn mâu thuẫn, ghi issue và chốt trước khi kết luận test.
Không tự đổi actor của ví dụ Customer/Staff thành actor dự án bằng cách thay chữ đơn thuần.

## 6. Quy tắc viết test case

Mỗi case phải có các nội dung sau, bằng cột riêng hoặc metadata liên kết:

| Nội dung | Quy định |
| --- | --- |
| ID, tiêu đề, requirement | Nêu đúng hành vi và nguồn yêu cầu |
| Level, type, priority | Tách cấp độ, kỹ thuật và mức ưu tiên |
| Precondition | Role, trạng thái, dữ liệu và môi trường cần có |
| Input/test data | Giá trị cụ thể hoặc fixture có thể tái tạo |
| Steps/action | Đủ rõ để người khác hoặc runner thực hiện lại |
| Expected | Kết quả quan sát được và side effect cần kiểm tra |
| Execution | Run ID, build, môi trường, ngày giờ, người/runner, trạng thái |
| Actual/evidence/defect | Kết quả thực tế, liên kết bằng chứng, Defect ID khi lỗi |

- Một case có một mục tiêu chính. Có thể dùng nhiều assertion để chứng minh cùng hành vi.
- Không viết expected kiểu “đúng”, “thành công”, “hoạt động bình thường” mà thiếu tiêu chí.
- Với API: kiểm tra status, schema/field/error code và thay đổi hoặc không thay đổi dữ liệu.
- Với UI: kiểm tra dữ liệu, hành động, thông báo và kết quả nghiệp vụ. Ẩn nút không chứng minh
  API đã chặn quyền.
- Với worker: kiểm tra trạng thái sau chạy, số tác vụ phát sinh và hành vi khi chạy lại.
- Precondition mô tả fixture/setup. Không bắt UT/IT chạy case A trước để case B có dữ liệu.
  Một luồng E2E nhiều bước có thể có thứ tự nội bộ và phải tự dựng trạng thái ban đầu.
- UT phân loại chính `Normal`, `Abnormal` hoặc `Boundary` theo template. Để tổng N/A/B không
  đếm trùng, mỗi case có một loại chính; tag phụ được ghi riêng.
- Dùng equivalence partitioning và boundary value cho miền giá trị; decision table cho tổ
  hợp điều kiện; state transition cho trạng thái; use case testing cho luồng nghiệp vụ.
- Kiểm tra biên dưới/trên và ngay hai phía khi có ý nghĩa. Với decimal, dùng bước theo độ
  chính xác nghiệp vụ; không mặc định mọi biên đều là cộng/trừ 1.
- Parameterized test phải thể hiện từng bộ dữ liệu. Nếu báo cáo đếm mỗi bộ là một case,
  mỗi bộ phải có mã hoặc khóa biến thể ổn định và quy tắc đếm thống nhất.

## 7. Dữ liệu, môi trường và tính ổn định

- UT không dùng DB, filesystem, HTTP hoặc provider thật. Thay dependency bên ngoài bằng
  fake/stub/mock tại interface phù hợp; không mock chính logic đang kiểm thử.
- IT tự seed dữ liệu và dọn bằng rollback, reset DB hoặc database riêng. Chỉ dùng rollback
  khi mọi thao tác thực sự tham gia transaction đó; request API/worker dùng connection khác
  cần cơ chế cô lập hoặc cleanup riêng.
- Không dùng ID tăng tuần tự hoặc dữ liệu còn sót từ test trước. Lấy ID từ fixture/kết quả tạo.
- Fixture dùng chung phải bất biến hoặc được reset có kiểm soát. Test chạy song song không
  được tranh chấp cùng bản ghi, storage key hoặc hàng đợi.
- Tạo tối thiểu tài khoản theo role trong ma trận quyền: Admin, Farmer/Cooperative, Buyer B2B,
  Consumer. Guest là trạng thái chưa xác thực. Bổ sung hai chủ sở hữu A/B cùng role để test
  truy cập chéo, cùng trạng thái khóa/KYC cần cho tính năng liên quan.
- Trạng thái KYC/đơn hàng/hợp đồng phải lấy từ domain đã thống nhất, không tự sáng tạo enum.
- Chỉ tạo dữ liệu cần cho kịch bản. Không sao chép dữ liệu cá nhân thật, secret hoặc mật khẩu
  môi trường dùng chung vào Git, Excel, ảnh và log. Kế hoạch tham chiếu cách cấp tài khoản test.
- Cố định thời gian bằng `TimeProvider`/clock có thể điều khiển. Test hạn dùng gồm trước,
  đúng và sau hạn; không dựa vào ngày hiện tại của máy chạy.
- Không dùng sleep cố định để chờ UI/job. Dùng điều kiện quan sát được với timeout hữu hạn.
- Test flaky phải có defect, người xử lý và hạn sửa. Retry phục vụ chẩn đoán; không xóa dấu
  lần lỗi đầu. Test đang quarantine không được tính Passed hoặc thỏa gate bắt buộc.
- Mỗi run ghi timezone; timestamp kỹ thuật ưu tiên ISO 8601 UTC. Ghi rõ Asia/Saigon khi
  kiểm tra mốc ngày nghiệp vụ tại Việt Nam.

## 8. Unit test và component test

### 8.1 C#/.NET

- Chuẩn theo coding standards: xUnit; tên `MethodName_Scenario_ExpectedBehavior`;
  bố cục Arrange–Act–Assert; test async trả `Task`, không `async void` hoặc chặn `.Result`.
- Domain: tạo object hợp lệ/không hợp lệ, invariant, chuyển trạng thái và trạng thái không đổi
  sau thao tác bị từ chối.
- Application: validation, ownership, phối hợp dependency, Result/Error và điều kiện commit.
- Tiền: `decimal`, quy tắc làm tròn, giảm giá, phí và giới hạn theo contract. Expected được
  tính độc lập; không gọi lại chính công thức production để sinh expected.
- Dùng `[Theory]` khi nhiều bộ dữ liệu cùng hành vi; `[Fact]` khi kịch bản riêng biệt.
- Assert giá trị, trạng thái và side effect có ý nghĩa. Chỉ verify số lần gọi repository/gateway
  khi đó là contract cần bảo vệ, chẳng hạn không commit khi validation thất bại.
- Exception test phải khớp kiểu lỗi và trạng thái liên quan. Không chấp nhận mọi exception
  như bằng chứng test thành công.
- Không yêu cầu test getter/setter thuần hoặc logging từng dòng. Logging chỉ là oracle chính
  khi có yêu cầu vận hành cụ thể; audit nghiệp vụ cần kiểm tra bản ghi bền vững.

### 8.2 React

- Khi frontend được triển khai, kiểm tra component qua hành vi người dùng: nhập dữ liệu,
  submit, hiển thị kết quả/lỗi và quyền thao tác. Không gắn test vào tên biến state hoặc
  cấu trúc nội bộ không thuộc contract.
- Test component cô lập HTTP bằng mock ở ranh giới API; bổ sung integration/E2E để kiểm tra
  contract và hành vi với backend thật.
- Cover loading, empty, error, disabled/submitting và validation của component có logic
  liên quan; không tạo test hình thức cho mọi phần tử giao diện tĩnh.
- Tương tác và assertion bất đồng bộ phải chờ điều kiện cụ thể. Không dùng sleep hoặc
  snapshot toàn trang làm bằng chứng duy nhất cho một luồng nghiệp vụ.
- Kiểm tra giao diện ẩn/hiện theo quyền chỉ chứng minh hành vi UI. Authorization backend
  phải được kiểm tra độc lập theo mục 12.
- Công cụ và vị trí test frontend **CẦN CHỐT** khi khởi tạo ứng dụng; xUnit là chuẩn backend,
  không bắt buộc dùng cho test component React.

## 9. Integration và architecture test

- API integration khuyến nghị `WebApplicationFactory`/test host của ASP.NET Core. Kiểm tra
  binding, validator, DI, middleware, serialization và error mapping qua pipeline thật [T1].
- DB integration cho SQL Server phải dùng SQL Server test cô lập và schema/migration tương
  ứng. EF InMemory, mock `DbSet` hoặc SQLite không chứng minh đầy đủ SQL Server translation,
  constraint, collation, transaction hoặc concurrency [T2].
- Kiểm tra unique constraint khi request cạnh tranh, precision tiền, quan hệ dữ liệu,
  rollback, điểm commit và query/phân trang theo yêu cầu.
- Auth integration phải có nhóm kiểm tra authentication thật: token sai chữ ký, hết hạn,
  issuer/audience sai. Test handler giả có thể hỗ trợ case authorization, nhưng không được
  dùng để tuyên bố đã kiểm chứng JWT.
- Adapter test dùng fake HTTP server hoặc sandbox: payload, timeout, lỗi provider,
  signature theo tài liệu nhà cung cấp và dữ liệu chuyển về application.
- CI thông thường không gửi mail thật, thanh toán thật hoặc gọi storage production.
  Sandbox test cần cấu hình riêng và credential từ secret store.
- Architecture test bảo vệ chiều: Api → Application/Infrastructure;
  Application → Domain; Infrastructure → Application/Domain; Domain không phụ thuộc tầng khác.
  Kiểm tra project reference và kiểu phụ thuộc quan trọng, không chỉ tên namespace.
- Application không lộ EF/HTTP/SDK type trong contract; Domain không I/O. Trước khi tự động
  hóa được, reviewer kiểm tra và ghi kết quả; không ghi architecture test đã chạy.

## 10. System test chức năng

- Chạy trên bản triển khai có React, API và DB tích hợp. Test host nội bộ không thay thế
  kiểm tra deployment, cấu hình HTTPS và hành vi trình duyệt.
- Mỗi business flow trong phạm vi có luồng chính, lỗi quan trọng và điều kiện quyền/trạng thái.
- Kiểm tra giao diện và dữ liệu backend sau thao tác: refresh, đăng nhập lại và actor liên quan
  phải nhìn thấy trạng thái phù hợp theo chính sách.
- Giao diện xử lý loading, dữ liệu rỗng, lỗi validation, lỗi mạng, thao tác gửi lặp và phiên hết hạn.
- Xác định trình duyệt/kích thước màn hình mục tiêu trong Test Plan. Không suy từ desktop
  thành kết luận mobile đạt.
- Với luồng nhiều người bán, cần kiểm tra cách chia dữ liệu và quyền từng bên theo SRS;
  không kết luận một đơn của một người bán đã bao phủ mọi trường hợp.

## 11. Các nhóm rủi ro bắt buộc khi tính năng liên quan nằm trong phạm vi

| Nhóm | Kịch bản tối thiểu cần xem xét |
| --- | --- |
| Quyền và ownership | Chưa đăng nhập; sai role; A truy cập resource của B; tài khoản bị khóa; role đúng nhưng trạng thái không cho phép |
| Đặt hàng/tồn kho | Số lượng không hợp lệ; biên tồn kho; request cạnh tranh phần tồn cuối; gửi trùng; hủy/hết hạn và giải phóng giữ chỗ |
| Thanh toán | Callback hợp lệ; sai chữ ký/số tiền/đơn/currency; trùng, đến trễ, sai thứ tự; đồng thời; replay sau khởi động lại |
| Hợp đồng/B2B | Chỉ các bên được phép; trạng thái ký/chấp nhận; điều kiện đặt cọc và thời hạn theo SRS |
| KYC/file riêng tư | Sai chủ sở hữu; URL trực tiếp; link hết hạn; vượt dung lượng; nội dung giả MIME; path traversal |
| Worker/email/audit | Chạy lại; nhiều instance; crash/retry; shutdown; nhận việc trùng; audit đúng actor và kết quả |
| Voucher/điểm | Biên giá trị; thời hạn; số lần dùng; quyền áp dụng; request đồng thời theo contract |
| QR/truy xuất | Lô công khai/riêng tư; hiệu lực; dữ liệu Guest được xem; không lộ dữ liệu nội bộ |

Danh sách trên là yêu cầu thiết kế test theo rủi ro, không khẳng định các tính năng đã tồn tại.
Những kịch bản không áp dụng phải có lý do, không tự tạo nghiệp vụ để lấp bảng.

Thanh toán phải chứng minh tính idempotent ở dữ liệu bền vững, số lần ghi nhận tiền và
side effect; test gọi hai lần trên một object in-memory chưa đủ. Tồn kho cần assert tổng số
phân bổ không vượt tồn và kết quả từng request theo contract, không ép cả hai request thành công.

## 12. Security test

- JWT API chưa xác thực trả 401; đã xác thực nhưng thiếu quyền trả 403, hoặc 404 nếu chính
  sách endpoint cần che sự tồn tại. Không lấy ví dụ MVC redirect 302 của template làm chuẩn API.
- Gửi request trực tiếp tới API, không chỉ kiểm tra nút/link trên React.
- Kiểm tra dữ liệu nhạy cảm trong JSON/network response, HTML/DOM khi có, URL, lỗi và log.
  Trường nội bộ không được chỉ ẩn bằng CSS hoặc bị lọc sau khi đã gửi xuống client.
- Đọc file riêng tư phải qua kiểm tra quyền hoặc URL ký theo chính sách. Xác minh link hết
  hạn và truy cập chéo tài khoản. File công khai được đánh giá theo chính sách riêng.
- Kiểm tra input injection, output encoding/XSS ở nơi render dữ liệu người dùng và upload
  theo kiểu tích hợp thực tế. Nếu dùng cookie, bổ sung CSRF; không áp dụng máy móc test cookie
  cho mọi endpoint Bearer JWT.
- Kiểm tra HTTPS trên staging, mixed content và CORS theo allowlist đã cấu hình.
- Security test có thể dùng công cụ và kiểm tra thủ công. Không có lỗ hổng được công cụ báo
  không đồng nghĩa hệ thống an toàn; phải giữ các case quyền và rò rỉ dữ liệu cụ thể.

## 13. Performance và NFR khác

Các mốc trong Report 5.4 — trang dưới 2 giây, upload 5 MB dưới 10 giây, N user đồng thời,
error rate dưới 1% — là **giá trị mẫu cần chốt theo NFR của AgriSustain**, không tự động
trở thành cam kết hoặc bằng chứng đạt.

Mỗi phép đo phải định nghĩa:

- Metric và điểm bắt đầu/kết thúc: API latency, trang sẵn sàng tương tác hay hoàn tất upload.
- Dataset, số user/arrival rate, tỷ lệ thao tác, ramp-up, warm-up và thời lượng đo.
- Môi trường, CPU/RAM, DB/index, mạng và provider thật hay giả.
- Threshold: p95/p99 hoặc tiêu chí phù hợp, error rate và quy tắc phân loại response lỗi.
- Kết quả thực tế, số mẫu, lỗi, tài nguyên sử dụng và script/log của run.

Một lần quan sát DevTools với một user chỉ là kiểm tra đơn lẻ. Kiểm tra concurrent user phải
có tải đồng thời được tạo và đo được. k6 là công cụ khuyến nghị khi cần load test; cấu hình
threshold để run thất bại khi vượt ngưỡng, thay vì chỉ xem biểu đồ [T3].

Không kết luận đạt khi `N` hoặc threshold còn chưa xác định. Các NFR khác như khả năng
phục hồi, backup/restore hoặc accessibility phải có case nếu SRS cam kết; không giới hạn
toàn bộ NFR vào hai sheet Security và Performance của mẫu.

## 14. Acceptance test và exploratory test

- Script UAT liên kết business flow/requirement, actor thật, điều kiện đầu, các bước nghiệp vụ
  và kết quả cuối. Hạn chế thuật ngữ implementation.
- Tiêu đề và outcome phải được chứng minh bởi các bước. Ví dụ “đến giao hàng” phải kiểm tra
  giao/hoàn tất nếu đó là phạm vi; dừng ở duyệt đơn không đủ chứng minh tiêu đề đó.
- Chỉ yêu cầu realtime khi SRS có contract tương ứng. Nếu có, xác định cách đo/quan sát và
  giới hạn thời gian; không kế thừa từ câu ví dụ trong mẫu.
- Người nghiệm thu thực hiện hoặc chứng kiến, ghi ngày, build, kết quả và feedback.
  Developer chạy ST thành công không tự thay cho xác nhận UAT.
- Feedback được phân loại thành defect, câu hỏi hoặc yêu cầu thay đổi; yêu cầu mới không
  tự đổi thành bug nếu nằm ngoài baseline đã chốt.
- Exploratory có phạm vi/mục tiêu phiên, thời gian, người thực hiện, quan sát và Defect ID
  khi có lỗi. Không dùng một dòng feedback thay cho toàn bộ acceptance script.

## 15. Trạng thái test và lịch sử thực thi

Chuẩn nội bộ đề xuất, dùng chung giữa các báo cáo:

| Trạng thái | Ý nghĩa |
| --- | --- |
| Not Run | Chưa thực hiện trên run/build đang báo cáo |
| Passed | Đã thực hiện và toàn bộ expected đạt, có bằng chứng |
| Failed | Đã thực hiện và ít nhất một expected không đạt |
| Blocked | Không thể thực hiện/hoàn tất do phụ thuộc hoặc môi trường; có lý do và người xử lý |
| Not Applicable | Không áp dụng cho phạm vi đã được chấp nhận; có lý do |

- `Pending`, `Untested`, `Not Executed` trong mẫu được chuẩn hóa thành `Not Run` khi không
  có ghi chú cho thấy đang bị chặn. Trường hợp bị chặn phải dùng `Blocked`.
- `N/A` trong ô kết quả là Not Applicable; `Abnormal` trong loại UT là một khái niệm khác.
- Run có mã riêng, commit/build và môi trường. Round 1/2/3 chỉ là cách trình bày lịch sử,
  không phải yêu cầu luôn phải chạy đúng ba lần.
- Test không nằm trong phạm vi chạy của round phải đánh dấu ngoài phạm vi round, không sao
  chép Passed từ round trước. Snapshot kết quả toàn release phải nói rõ kết quả nào được
  kế thừa, từ build nào và vì sao còn hợp lệ.
- Retest ghi thêm lần chạy, không ghi đè kết quả cũ hoặc xóa Defect ID. Thay đổi build sau
  UAT cần đánh giá ảnh hưởng và nghiệm thu lại phần liên quan.

## 16. Defect và phân biệt severity/priority

Defect cần ID, tiêu đề, requirement/case, build/môi trường, dữ liệu/role, steps, expected,
actual, evidence, severity, priority, người phụ trách và kết quả retest.

| Severity | Hướng dẫn đánh giá |
| --- | --- |
| Critical | Mất/sai tiền nghiêm trọng, lộ dữ liệu, vượt quyền nghiêm trọng, mất dữ liệu hoặc luồng cốt lõi không thể tiếp tục và không có cách xử lý khả thi |
| Major | Nghiệp vụ quan trọng sai hoặc chức năng chính hỏng; tác động đáng kể nhưng chưa thuộc Critical |
| Minor | Lỗi phạm vi hạn chế, còn cách sử dụng phù hợp, tác động nghiệp vụ thấp |
| Trivial | Lỗi trình bày/ngôn từ nhỏ, không làm sai dữ liệu hoặc chặn thao tác |

Severity phản ánh mức tác động. Priority phản ánh thứ tự xử lý theo rủi ro và mốc bàn giao.
Nhãn Critical ở cột Priority của mẫu không tự là severity của defect.

Chuẩn priority đề xuất cho case và defect: `P0` cần xử lý/kiểm chứng trước mốc hiện tại;
`P1` nghiệp vụ quan trọng; `P2` nghiệp vụ thông thường; `P3` tác động thấp. Khi chuyển từ
nhãn Critical/High/Medium/Low trong mẫu, review ý nghĩa từng dòng trước khi ánh xạ. Priority
không tự quyết định case có bắt buộc cho release; tập case bắt buộc phải được Test Plan nêu rõ.

Luồng đề xuất: `New → Triaged → In Progress → Ready for Retest → Closed`;
retest không đạt chuyển `Reopened`. `Duplicate`, `Rejected`, `Deferred` cần lý do và người
quyết định. Developer báo sửa xong không tự đóng lỗi khi chưa có xác nhận kiểm tra.

## 17. Báo cáo và công thức thống kê

Chốt một tập test **S** cho một run/round xác định, đếm mỗi Case ID/biến thể một lần:

```text
Total = Passed + Failed + NotRun + Blocked + NotApplicable
Applicable = Total - NotApplicable
Executed = Passed + Failed
Execution rate = Executed / Applicable × 100%
Pass rate among executed = Passed / Executed × 100%
Passed share of applicable = Passed / Applicable × 100%
```

Mẫu số bằng 0: hiển thị “Không áp dụng”/“Chưa có dữ liệu” phù hợp, không hiển thị 100% hoặc
che lỗi bằng `IFERROR(...,0)`. Blocked vẫn thuộc Applicable và không phải Executed.

- “Test coverage” của các mẫu hiện thiên về tỷ lệ thực thi; “Test successful coverage” là
  tỷ lệ Passed trên số applicable. Không gọi hai số này là line/branch coverage.
- Requirement coverage phải nêu định nghĩa riêng: tỷ lệ requirement trong scope có test
  được review; có thể báo thêm tỷ lệ requirement đã có bằng chứng đạt. Requirement có nhiều
  case bắt buộc chỉ được đánh dấu đạt khi tất cả case bắt buộc đạt.
- Code coverage là số đo do công cụ cung cấp theo phạm vi code; không tự đặt ngưỡng 80%/90%
  nếu chưa thống nhất baseline. Coverage cao không thay việc kiểm tra nhánh rủi ro.
- Đếm defect theo Defect ID duy nhất. Một defect được nhiều case phát hiện không thành nhiều
  lỗi; báo cáo “case thất bại” và “số defect” là hai số khác nhau.
- Tổng defect theo UT/IT/ST/UAT phải dùng cấp phát hiện gốc hoặc có quy tắc chống trùng.
  Review/document defect được phân loại riêng theo work product để không đếm lặp tổng.
- Báo cáo phân tách thiết kế test, thực thi và tồn đọng; có người chạy, ngày, build, môi trường,
  rủi ro còn lại và liên kết evidence.

## 18. Sửa biểu mẫu trước khi dùng làm báo cáo chính thức

Các phát hiện sau là từ file nguồn, chưa chỉnh sửa các file đó:

| Nguồn/vị trí | Vấn đề | Quy tắc xử lý |
| --- | --- | --- |
| Report 5.2 và 5.3, `Test Cases!D3:D4` | Công thức là `=#REF!` | Liên kết đúng metadata dự án, kiểm tra lại sau đổi tên sheet |
| Report 5.2, `Feature Name1/2!B7:E8`; Report 5.3, `Workflow Name1/2!B7:E8` | Round 2/3 vẫn đếm cột F của Round 1 | Round 1 dùng F, Round 2 dùng I, Round 3 dùng L và giữ cùng tập Case ID |
| Các sheet feature/workflow, công thức `B4` | `COUNTA` cột A bao gồm cả dòng tiêu đề nhóm, ví dụ `Function B` hoặc `Scenario B` | Chỉ đếm bản ghi test hợp lệ theo ID; loại heading, placeholder và dòng mẫu |
| Report 5.1, `Test Cases!D10` | Tham chiếu `ModuleName2` nhưng không có sheet tương ứng | Tạo sheet thật hoặc bỏ entry ngoài scope; không giữ link giả |
| Report 5.1, `Test Statistics!C13:E13` và `I13` | Số mẫu 1 Passed + 1 Failed + 1 Untested nhưng Total là 1 | Tính lại từ case thật; đối soát tổng trạng thái bằng tổng case |
| Report 5.2, `Test Cases!D9:D13` | Tên `Feature1/Feature2` không khớp `Feature Name1/Feature Name2` | Danh mục phải trỏ đúng sheet hiện có |
| Report 5.2, `SecurityTest`; Report 5.4/5.5 | Role, endpoint, target, ngày và outcome là ví dụ; status dùng nhiều tên | Thay bằng baseline dự án và chuẩn hóa trạng thái; không công nhận số liệu mẫu |

Khi sửa phải kiểm tra công thức, data validation, hyperlink và tổng hợp sau đổi tên/nhân bản
sheet. Trong round đã chạy, ô status trống là dữ liệu thiếu, phải đối soát và gán trạng thái
thực tế; không âm thầm bỏ khỏi mẫu số. Mỗi báo cáo phải lưu rõ round đang tổng hợp.

## 19. Điều kiện bắt đầu và hoàn thành

### 19.1 Bắt đầu

- UT/IT: requirement và expected đủ rõ, build chạy được, fixture/dependency sẵn sàng.
- ST: bản triển khai nhận diện được, smoke test đạt, dữ liệu và tài khoản đủ, không có lỗi
  môi trường làm kết quả không đáng tin.
- UAT: phạm vi nghiệm thu được chốt, luồng liên quan qua ST, người nghiệm thu và script sẵn sàng.

### 19.2 Merge một thay đổi

- Build và các test bắt buộc của phần thay đổi đều đạt; không có test bị skip/quarantine
  chưa xử lý được dùng để lách gate.
- Nhánh nghiệp vụ/rủi ro thay đổi có test phù hợp; bug fix có kiểm tra hồi quy khi khả thi.
- PR ghi rõ test đã chạy, test chưa chạy, lý do và ảnh hưởng API/schema/data.
- Có review theo quy định dự án. Không mặc định PR phải chạy mọi UAT hoặc load test.

### 19.3 Đóng mốc kiểm thử/release

- Tất cả case bắt buộc trong scope đã thực thi và đạt; không còn Failed/Blocked/Not Run
  trong tập bắt buộc. Việc giảm phạm vi cần quyết định có ghi nhận, không đổi status cho đẹp.
- Không còn defect Critical/Major mở trong phạm vi release. Đây là gate đề xuất của nhóm;
  ngoại lệ phải do người có quyền chấp nhận release quyết định và ghi rủi ro cụ thể.
- Các NFR cam kết có target đã chốt và bằng chứng đạt trên môi trường được ghi nhận.
- UAT có xác nhận và feedback được xử lý hoặc có quyết định rõ ràng.
- Báo cáo, defect và evidence đối soát được với build bàn giao.

## 20. Tổ chức test và thực thi trong CI

Giữ cấu trúc hiện có và bổ sung khi triển khai:

```text
AgriSustain/tests/Unit/            Domain và Application unit tests
AgriSustain/tests/Integration/     API, SQL Server và adapter integration tests
AgriSustain/tests/Architecture/    Kiểm tra chiều phụ thuộc
AgriSustain/tests/System/          Đề xuất: kịch bản E2E/NFR và cấu hình chạy
AgriSustain/tests/Acceptance/      Đề xuất: script và liên kết evidence UAT
```

Các thư mục System/Acceptance là đề xuất; có thể lưu script/hồ sơ ở vị trí riêng được
thống nhất. Không yêu cầu mọi cấp kiểm thử phải trở thành một project xUnit.

- Khi triển khai test backend, thêm test project thật vào solution và pin phiên bản package.
  Dùng xUnit theo coding standards; công cụ coverage/E2E/load được lựa chọn theo nhu cầu và
  ghi rõ phiên bản. Không cài nhiều công cụ trùng vai trò mà thiếu lý do.
- CI cho PR: restore, format verification, Release build, UT, architecture và IT phù hợp
  với phần thay đổi. IT cần SQL Server phải có DB/service sẵn sàng và cleanup tin cậy.
- Nhóm test nhanh và nhóm cần hạ tầng được lọc rõ bằng project/trait. Nếu một nhóm bắt buộc
  không chạy được, pipeline phải thể hiện thiếu kiểm chứng, không báo toàn bộ test đạt.
- Khi đã có test project, lưu kết quả máy đọc được như TRX/JUnit tương thích runner,
  số test discover/executed/skipped, build SHA và artifact cần thiết.
- Zero discovered tests là lỗi cấu hình khi pipeline kỳ vọng có test. Exit code 0 của
  `dotnet test` trên solution không có test project không phải chứng cứ kiểm thử.
- Trước release: chạy ST/regression, NFR và UAT theo kế hoạch. Không tự động hóa chữ ký
  nghiệm thu thay cho người có trách nhiệm.

**Cập nhật 18/10/2026:** solution có bốn project backend và ba test project cho UT,
API/demo integration và architecture; frontend React có E2E cho ba UC được giao.
Các phụ thuộc/persistence hiện dùng giả lập theo yêu cầu; chưa có test SQL Server/JWT/provider
thật. Việc biên soạn quy tắc không tự là kết quả thực thi; xem hồ sơ kiểm thử của phiên bản code.

## 21. Checklist áp dụng cho mỗi tính năng

- [ ] Requirement, actor, trạng thái và expected đã rõ, không lấy ví dụ template làm yêu cầu.
- [ ] Case có ID duy nhất, traceability, priority, dữ liệu và precondition tái tạo được.
- [ ] Bao phủ luồng chính, dữ liệu lỗi, biên, quyền và rủi ro đồng thời khi có liên quan.
- [ ] Chọn đúng cấp; dependency thật/giả được ghi rõ; không dùng fake DB để kết luận SQL đúng.
- [ ] Test độc lập; thời gian kiểm soát được; cleanup và chạy song song không gây nhiễu.
- [ ] Actual/evidence gắn build và run; Failed có defect; Blocked có lý do/người xử lý.
- [ ] Round và công thức thống kê đúng; không đếm heading, trùng ID hoặc dùng status mẫu.
- [ ] Retest giữ lịch sử; regression phần bị ảnh hưởng đạt; không che flaky bằng retry.
- [ ] NFR có phép đo và target thống nhất; UAT có người xác nhận khi tới mốc nghiệm thu.
- [ ] Báo cáo nêu đúng phần đã kiểm chứng và phần còn thiếu; gate được đánh giá đúng scope.

## 22. Nguồn đối chiếu

### Hồ sơ người dùng cung cấp

- [Report 5.0 — Test Documentation](<../Report-5.0_Test Documentation.docx>):
  I. Overall Plan; II. Implementation & Schedule; phạm vi, loại/cấp test, dữ liệu và báo cáo.
- [Report 5.1 — Unit Test](<../Report-5.1_Unit Test.xls>):
  Cover; Test Cases; Test Statistics; ModuleName1; case Normal/Abnormal/Boundary và ba round.
- [Report 5.2 — Integration Test](<../Report-5.2_Integration Test.xlsx>):
  Cover; Test Cases; Test Statistics; Feature Name1; Feature Name2; SecurityTest.
- [Report 5.3 — System Test FRs](<../Report-5.3_System Test_FRs.xlsx>):
  Cover; Test Cases; Test Statistics; Workflow Name1; Workflow Name2.
- [Report 5.4 — System Test NFRs](<../Report-5.4_System Test_NFRs.xlsx>): Security; Performance.
- [Report 5.5 — Acceptance Test Scripts](<../Report-5.5_Acceptance Test Scripts.xlsx>):
  UAT; Exploratory.

### Tài liệu kỹ thuật bổ sung

- [T1 — Microsoft: Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0).
- [T2 — Microsoft: Choosing a testing strategy for EF Core](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy).
- [T3 — Grafana: k6 thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/).

Tra cứu ngày 18/10/2026. Các nguồn kỹ thuật hỗ trợ lựa chọn công cụ và cách kiểm chứng;
không quyết định thay dự án về acceptance criteria, SLA, coverage threshold hoặc business rule.
