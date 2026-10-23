# Đánh giá ERD Thẻ 8 theo Gomaa và Report 3

Ngày đánh giá: 15/10/2026. Phạm vi: đọc và đánh giá; chưa sửa Google Docs.

Nguồn đã đối chiếu:

- Hassan Gomaa, *Software Modeling and Design*: §7.5, §7.9–7.9.1, trang in 103–104 và 111–113; §8.1–8.3, trang 116–117; §8.6, trang 123; §15.7.2–15.7.5, trang 269–272. Trang PDF tương ứng lớn hơn trang in 28 trang.
- [Report 3](https://docs.google.com/document/d/1nPGNTGhL7M8G2MKMOsU-jz2NDxBCFiZMBoz82Z7ScMU/edit?tab=t.0): bản tải trực tiếp từ Google Docs trong phiên đánh giá; trọng tâm phần II Use Case Specifications, I.3.2–3.3, permission matrix, functional requirements và business rules.
- [ERD — Thẻ 8](https://docs.google.com/document/d/1rvgCy_v54vSUWUonw5xUAy8zsJ1oEshBNfgWnFKx2GQ/edit?tab=t.j6sgjmtqbwuo): nội dung “AgriSustain ERD”, gồm danh sách entity/thuộc tính và bảng quan hệ. Nhận xét không áp dụng mặc nhiên cho các thẻ khác.

## 1. Kết luận

Thẻ 8 có **46 entity**, bao phủ đủ **27 entity đã mô tả tại I.3.2 của Report 3**, đồng thời bổ sung **19 entity**. Đây là một bản nháp mô hình dữ liệu có độ bao phủ tốt, nhưng **chưa đủ để chốt làm logical ERD/schema đáp ứng toàn bộ yêu cầu**.

Vấn đề chính là một số quan hệ khác UC spec, một số dữ liệu bắt buộc còn thiếu, và các ràng buộc liên bảng chưa được xác định. Không có căn cứ để kết luận rằng 46 tự nó là “quá nhiều”, hoặc ép mô hình xuống một con số cố định.

Report 3 gọi phần I.3 là “Conceptual Data Model”. Thẻ 8 lại chứa nhiều chi tiết của logical/design model: snapshot, phân bổ tồn kho, lịch sử phiên bản, quyết toán và dữ liệu kỹ thuật. Nên trình bày conceptual view gọn hơn trong SRS, đồng thời giữ logical view chi tiết ở tài liệu thiết kế. Việc ẩn một bảng khỏi conceptual view không đồng nghĩa xóa bảng đó khỏi thiết kế triển khai.

## 2. Hiểu đúng phương pháp Gomaa

Nhận định “dựa vào UC spec để xác định entity” đúng về hướng tiếp cận, nhưng UC spec là một nguồn, không phải nguồn duy nhất và cũng không có phép chuyển từng danh từ thành một bảng.

Gomaa §7.9 xác định entity class là lớp thiên về dữ liệu, lưu thông tin và cung cấp truy cập; nhiều entity tồn tại lâu dài và cần lưu trong file hoặc database. Cần tìm khái niệm trong mô tả bài toán, xác định thuộc tính và quan hệ, rồi kiểm tra khả năng đáp ứng các UC. §8.1 nói việc chia lớp phụ thuộc bản chất bài toán và phán đoán của người phân tích; không có duy nhất một cách chia.

§8.2–8.3 phân biệt entity, boundary, control và application logic. Một màn hình, một hành động thanh toán hoặc một dịch vụ gửi email không tự động trở thành entity. Ngược lại, bản ghi giao dịch thanh toán hoặc thông báo cần lưu có thể là entity.

§7.9.1 lưu ý một lớp chỉ có một thuộc tính thì đáng xem xét liệu có nên là thuộc tính của lớp khác. Đây là dấu hiệu để kiểm tra, không phải quy tắc cấm: §15.7.4 còn đưa ví dụ association class Hours có một thuộc tính hoursWorked và được ánh xạ thành bảng liên kết. Vì vậy không nên bỏ Cart hoặc bảng liên kết chỉ vì có ít trường nghiệp vụ.

§15.7.2–15.7.4 phân biệt bước thiết kế relational database: xác định PK, ánh xạ association thành FK và association class thành bảng liên kết. Do đó số bảng khi triển khai có thể lớn hơn số khái niệm chính trong conceptual model.

Một quy trình phù hợp là: mô tả nghiệp vụ + UC + business rules → entity ứng viên → thuộc tính/định danh/quan hệ/bản số → chạy thử normal và alternative flows → thiết kế PK/FK/unique/check và các bảng liên kết khi cần. Chuỗi này là cách áp dụng các nguyên tắc của sách vào dự án, không phải trích nguyên văn một quy trình ERD của tác giả.

## 3. Đối chiếu toàn bộ 46 entity

### 3.1. 27 entity đã có trong I.3.2 của Report 3

| Nhóm | Entity | Căn cứ và đánh giá |
|---|---|---|
| Tài khoản | User, FarmerProfile, WholesaleBuyerProfile, DeliveryAddress | AUTH-01/02/03, KYC, checkout. Giữ; còn thiếu một số dữ liệu hồ sơ người mua lẻ và dữ liệu xử lý KYC. |
| Nông trại | CropSeason, CultivationLog | FAR-01/02. Giữ; nhật ký phải bất biến sau khi ghi. |
| Sản phẩm | Category, Product, ProductImage | ADM-05, FAR-03, MKT-01/02. Giữ; có cây danh mục, gallery nhiều ảnh và một ảnh chính. |
| Truy xuất/chứng nhận | HarvestBatch, TraceabilityQRCode, FarmCertificate | FAR-04/07, TRC-01, VO-01. Giữ; QR có ScanCount và trạng thái thu hồi riêng nên tách bảng có căn cứ. |
| Đặt trước | PreOrderCampaign | FAR-05, WB-04. Giữ nhưng sửa quan hệ với HarvestBatch cho khớp UC hiện tại. |
| Bán hàng | Order, OrderItem, Voucher, ProductReview | CON-02, RC-05, ADM-07/08. Giữ; cần định nghĩa rõ cấp Order và SellerOrder khi chia đơn. |
| Hợp đồng | B2BContract, ContractMilestone | WB-05/06/07, FAR-08 và JOB-05. Giữ. |
| Hội viên | B2BSubscriptionPackage, B2BSubscription | ADM-09, B2B-08. Giữ; quy định không chồng lấn quyền lợi/gói đang hiệu lực. |
| Tài chính | PaymentTransaction, FarmerPayout, CommissionPolicy | RC-03 và danh mục FIN-02/04/05, I.3.3. Giữ; phần FIN mới có mô tả tổng quan, còn thiếu đặc tả chi tiết cho một số quyết định thiết kế. |
| Khiếu nại | DisputeTicket | RC-06, ADM-06. Giữ; còn thiếu dữ liệu theo dõi yêu cầu bổ sung chứng cứ. |
| Giá thị trường | RegionalPriceBenchmark | Có trực tiếp tại I.3.2 và quy tắc historical price immutability ở I.3.3. Không được kết luận dư chỉ vì chưa thấy UC riêng; cần làm rõ nguồn nhập/đồng bộ dữ liệu. |
| Thông báo | Notification | Nhiều UC và JOB-05/06/09. Giữ; cần định danh bản ghi đích nếu thông báo mở chi tiết nghiệp vụ. |

### 3.2. 19 entity bổ sung trong Thẻ 8

| Entity | Căn cứ | Khuyến nghị |
|---|---|---|
| Cart | CON-01 có giỏ hàng và trạng thái giỏ được đồng bộ | Giữ trong logical model. Một thuộc tính UpdatedAt không đủ để kết luận bảng thừa. Có thể ẩn ở conceptual view. |
| CartItem | CON-01 lưu nhiều sản phẩm, số lượng, lựa chọn mua | Giữ. Phải bổ sung liên kết lô nếu tiếp tục thực hiện đúng UC hiện tại. |
| SellerOrder | CON-01 nhóm theo Farmer; FAR-06 xử lý đơn của nông trại | Có cơ sở thiết kế. Giữ nếu một checkout chứa nhiều Farmer và mỗi Farmer xử lý/hủy/giao độc lập; bổ sung quy tắc chia đơn vào SRS. Nếu nghiệp vụ giới hạn một Farmer/đơn thì có thể gộp với Order. |
| OrderItemBatchAllocation | I.3.3 yêu cầu giữ/trừ tồn theo lô; một dòng sản phẩm có thể được cấp từ nhiều lô trong thiết kế này | Giữ khi cho phép nhiều lô trên một dòng. Nếu mỗi dòng chỉ ứng với đúng một lô như mô tả OrderItem tại I.3.2, FK HarvestBatch ở OrderItem đủ. FIFO là quyết định mới, chưa có căn cứ rõ trong UC đã đọc. |
| Shipment | FAR-06 có tracking, giao nhận; permission matrix có contract delivery lot | Có căn cứ. Giữ nếu theo dõi nhiều đợt/kiện; có thể gộp thuộc tính vào đơn khi chắc chắn chỉ một lần giao. |
| ShipmentItem | Permission matrix yêu cầu ghi lượng giao, xác nhận lượng nhận, đối soát chênh lệch | Nên giữ nếu đối soát theo dòng/lô hoặc giao từng phần. Cần đặc tả chi tiết luồng B2B tương ứng. |
| InventoryMovement | Giữ/trừ/hoàn tồn trong CON-02, FAR-06 và JOB-02 | Bảng sổ biến động là lựa chọn thiết kế hợp lý cho đối soát, không bị bắt buộc chỉ từ tên UC. Xác định nó hay số dư lưu sẵn là nguồn dữ liệu gốc. |
| VoucherRedemption | ADM-07, CON-02 và quota voucher | Hợp lý để lưu lịch sử, giữ quota và xử lý hủy. Quy tắc voucher của Farmer và cộng nhiều voucher chưa được SRS hiện tại mô tả rõ. |
| OrderStatusHistory | Theo dõi đơn, audit và mốc Delivered/Received | Hợp lý khi cần timeline; xác định cấp đơn được theo dõi. AuditLog vẫn có vai trò khác, không mặc nhiên trùng. |
| ContractLine | WB-05/FAR-08 có sản phẩm, sản lượng, quy cách và khung giá | Hợp lý nếu hợp đồng có nhiều dòng hoặc cần ánh xạ thực hiện từng phần. Không bắt buộc tách khi mỗi hợp đồng chỉ có một sản phẩm và một bộ điều khoản. |
| ContractVersion | FAR-08 cho yêu cầu điều chỉnh, đàm phán lại | Có ích khi cần giữ lịch sử nội dung; chưa thấy UC quy định rõ lịch sử phiên bản. Giữ nếu phải chứng minh nội dung từng lần ký, kèm bất biến nội dung đã ký. |
| ContractSignature | WB-06/FAR-08 yêu cầu hai bên xác nhận | Có căn cứ mạnh. Giữ để lưu ai ký, ký nội dung nào và thời điểm xác nhận. |
| ContractTerminationRequest | FAR-08 có yêu cầu chấm dứt, lý do và bằng chứng | Có căn cứ mạnh. Giữ; liên kết hoàn tiền/settlement phải nhất quán. |
| Invoice | WB-07 yêu cầu xem hóa đơn và SCR-20 liệt kê invoices | Có căn cứ. Cần bổ sung quy tắc phát hành và quan hệ với nghĩa vụ thanh toán. |
| InvoiceLine | Cần nếu hóa đơn có nhiều dòng hoặc lập theo từng phần giao nhận | Có điều kiện; UC chỉ nhắc hóa đơn chưa đủ để chốt mọi bản số hiện tại. |
| RefundTransaction | ADM-06 và FIN-03 có yêu cầu hoàn tiền, xác nhận chuyển khoản | Có căn cứ mạnh. Giữ tách với giao dịch thu tiền; cần giới hạn tổng số đã hoàn. |
| PayoutItem | Phân bổ khoản trả Farmer theo dòng hàng hoặc kỳ hợp đồng | Giữ nếu gom nhiều nguồn trong một payout. Nếu payout luôn 1:1 với đơn như I.3.2 thì có thể đơn giản hơn; cần giải quyết mâu thuẫn trước. |
| DisputeEvidence | RC-06 và ADM-06 có bằng chứng, yêu cầu bổ sung | Có căn cứ mạnh. Giữ; một ticket có thể có nhiều file và nhiều người nộp. |
| AuditLog | AUTH-01/02, ADM-03/05/06/07/08/09 yêu cầu ghi audit | Có căn cứ mạnh. Giữ ở logical/technical view. Cần thêm định danh bản ghi bị tác động. |

Không nên bỏ CartItem, ContractSignature, RefundTransaction, DisputeEvidence hoặc AuditLog để đạt một số lượng entity mong muốn. Đây là các ví dụ bổ sung có thể truy được về yêu cầu.

## 4. Các điểm cần sửa trước khi chốt

### 4.1. Quan hệ campaign–batch khác đặc tả hiện tại

FAR-05 precondition 3 yêu cầu đã có HarvestBatch ở trạng thái Planned. Normal flow 2 chọn đúng linked batch; alternative flow sửa chiến dịch khóa lựa chọn batch. I.3.3 kiểm tra TargetYield và EndDate theo chính batch đó.

Thẻ 8 ghi `PreOrderCampaign 0..1 — 0..* HarvestBatch`: mỗi campaign có thể có không có hoặc nhiều batch. Đây là thay đổi nghiệp vụ chứ không chỉ sửa kỹ thuật.

Theo SRS hiện tại, mỗi campaign phải liên kết đúng một HarvestBatch. Chưa đủ thông tin để quyết định một batch được mở một hay nhiều campaign trong toàn bộ lịch sử. Nếu nhiều campaign được phép, phải giới hạn tổng lượng cam kết đang hiệu lực để không bán quá sản lượng. Product và CropSeason của campaign có thể suy ra từ batch; nếu vẫn lưu trực tiếp, phải kiểm tra nhất quán.

### 4.2. CartItem thiếu batch trong khi UC sử dụng batch đã xác định

CON-01 normal flow 4 hiển thị Linked Harvest Batch Code; flow 9 và các exception kiểm tra tồn/thu hồi của từng batch. CartItem trong Thẻ 8 chỉ liên kết Product và PreOrderCampaign.

Phải chọn một cách nhất quán: bổ sung batch được chọn cho CartItem, hoặc sửa UC để giỏ chứa sản phẩm và batch chỉ được hệ thống phân bổ khi đặt đơn. Phương án thứ hai cần giải thích batch code hiển thị trong giỏ và xử lý đổi batch. Allocation sau khi tạo Order không tự giải quyết việc batch đã xuất hiện trong giỏ.

### 4.3. Thiếu dữ liệu Consumer và điểm thưởng

AUTH-01 khởi tạo Consumer profile; AUTH-03 yêu cầu Gender, DateOfBirth, Reward Points Balance và UpdatedAt. CON-02 cùng màn hình checkout yêu cầu đổi điểm thành discount.

Thẻ 8 chưa có các dữ liệu này ở User hoặc entity khác; Order cũng chưa tách số điểm đã dùng và giá trị giảm do điểm. Có thể thêm thuộc tính vào User hoặc tạo RetailCustomerProfile. Nếu điểm có tích/tiêu/hoàn và cần lịch sử đối soát, cân nhắc bảng giao dịch điểm. Chưa thể bắt buộc tên bảng LoyaltyTransaction khi SRS chưa định nghĩa chính sách tích điểm. Cần làm rõ quy tắc giữ, trừ và hoàn điểm khi payment hết hạn/hủy/hoàn tiền.

### 4.4. KYC chưa đáp ứng đầy đủ VO-01

VO-01 hiện bao gồm cả RetailCustomer, xem documents/images và Request Additional Information. Thẻ 8 chỉ có VerificationStatus trên hai loại profile; liên kết User thẩm định mới thể hiện cho FarmCertificate.

Cần dữ liệu người duyệt, lần nộp/thời điểm nộp, nội dung yêu cầu bổ sung và tài liệu. Dashboard pending work còn yêu cầu Submission Time/Wait Time. Có thể mở rộng profile cho quy trình một hồ sơ hiện hành; nếu lưu nhiều lần nộp hoặc nhiều tài liệu thì KYCApplication/KYCDocument có cơ sở. Trước hết phải chốt RetailCustomer có thực sự cần KYC vì các phần SRS chưa nhất quán.

### 4.5. Thiếu dữ liệu cho thời hạn bổ sung chứng cứ

ADM-06 postconditions cho bên được yêu cầu 48 giờ để bổ sung, có xử lý timeout và trường hợp cả hai bên không phản hồi. DisputeEvidence lưu file nhưng không thể hiện đầy đủ ai đã bị yêu cầu, thời điểm yêu cầu, deadline và việc đáp ứng.

Cần bổ sung dữ liệu này, bằng thuộc tính nếu chỉ một vòng hoặc entity yêu cầu phản hồi nếu có nhiều vòng/nhiều bên. Không nên chỉ dùng TicketStatus để suy ra toàn bộ lịch sử.

### 4.6. QR optional khác quy tắc hiện tại

Thẻ 8 cho HarvestBatch có 0..1 QR. FAR-04 postconditions và I.3.3 lại yêu cầu một QR duy nhất cho mỗi batch. Nếu đây là invariant của bản ghi được tạo thành công, quan hệ phải là 1:1. Chỉ giữ 0..1 khi SRS cho phép một trạng thái chưa tạo QR; cần quy định trạng thái đó rõ ràng.

### 4.7. Thiếu định danh và ràng buộc nếu coi đây là logical ERD

Danh sách trường chưa thể hiện PK/FK, kiểu dữ liệu, nullability và unique constraints. Ở conceptual model có thể lược bớt, nhưng chưa thể dùng danh sách hiện tại như schema đã sẵn sàng triển khai.

Đặc biệt AuditLog có EntityType nhưng thiếu EntityId/RecordId; Notification có RelatedRecordType nhưng thiếu RelatedRecordId hoặc quan hệ đích tương đương. Cần chỉ rõ đối tượng bị thay đổi và đối tượng được mở khi bấm thông báo. Cách lưu định danh đa kiểu cần được thiết kế cùng kiểm tra tham chiếu.

Các unique cần mô tả gồm Email/Username, PhoneNumber theo AUTH-01/02, BatchCode, QRContentString, VoucherCode, ContractNumber, `(ContractId, VersionNo)`, một review mỗi OrderItem và một Cart mỗi User. Category cần unique theo parent theo GBR-09; I.3.2 lại ghi unique toàn hệ thống, phải thống nhất.

### 4.8. Quan hệ optional với tài chính cần quy tắc liên bảng

PaymentTransaction có nhiều đích optional: Order, B2BSubscription, ContractMilestone và B2BContract. Bản số riêng lẻ cho phép giao dịch không có đích hoặc trỏ nhiều nghĩa vụ không liên quan. Cần chọn một nghĩa vụ thanh toán chính; nếu thanh toán cọc trực tiếp contract thì không đồng thời tính như cọc milestone. Nếu lưu thêm ContractId để tra cứu, phải là dữ liệu nhất quán với milestone chứ không là một nghĩa vụ thứ hai.

PayoutItem phải có nguồn quyết toán rõ ràng, không quyết toán trùng một khoản qua cả OrderItem và ContractMilestone. Invoice liên kết SellerOrder/Milestone và InvoiceLine liên kết OrderItem/ShipmentItem phải có quy tắc nhất quán cùng hợp đồng, người bán và lượng đã nghiệm thu. Tổng hoàn tiền thành công không vượt khoản đã thu có thể hoàn; tính cả hoàn từng phần và xử lý lặp.

### 4.9. Dữ liệu tổng hợp và ownership chưa có invariant

Ví dụ cần ghi rõ: batch.product = allocation.orderItem.product; sellerOrder.farmer = orderItem.product.farmer; batch và crop season cùng Farmer; shipment và milestone thuộc cùng hợp đồng với seller order khi có liên kết contract; tổng phân bổ không vượt nhu cầu/tồn; tổng giao/nhận/nghiệm thu tuân theo quy tắc đối soát.

HarvestBatch.AvailableQuantity/ReservedQuantity, campaign.CommittedYield, Voucher.UsedCount, các tổng tiền và lượng milestone có thể suy ra từ bản ghi chi tiết. Nếu lưu để tăng tốc, phải xác định cách cập nhật atomic và phục hồi khi lỗi. Với concept view, đánh dấu là derived hoặc lược bỏ chi tiết này.

### 4.10. Ký phiên bản và trạng thái cấp đơn

ContractSignature gắn ContractVersion là hướng hợp lý. Cần bảo đảm nội dung đã ký bất biến, hai bên ký cùng một phiên bản và bản đang có hiệu lực được xác định rõ. ContractLine nằm ở contract trong khi snapshot ở ContractVersion chỉ an toàn khi snapshot đóng băng đủ nội dung được ký; không được sửa dòng hiện hành rồi diễn giải thành nội dung phiên bản đã ký.

OrderStatusHistory chỉ gắn SellerOrder nhưng Order cũng có OrderStatus. Phải quy định cách tổng hợp trạng thái Order khi một Farmer hủy còn Farmer khác giao; mốc Delivered/Received dùng để khiếu nại phải thuộc đúng đối tượng. Nếu cần timeline Order tổng, phải biểu diễn hoặc quy định cách tạo ra timeline đó.

## 5. Mâu thuẫn/yêu cầu còn thiếu trong Report 3 phải chốt trước

| Điểm | Bằng chứng | Ảnh hưởng |
|---|---|---|
| 24 giờ và 48 giờ | FAR-06 postconditions nói 24 giờ; RC-06, CON-04 và GBR-24 cho khiếu nại 48 giờ; I.3.3 cho payout sau 24 giờ không tranh chấp | Cần thống nhất cửa sổ khiếu nại, mốc bắt đầu và chính sách payout. Có thể trả sau khi hết cửa sổ hoặc có cơ chế thu hồi/giữ lại; không tự chọn thay nghiệp vụ. |
| Một order hay nhiều seller orders | Cart nhóm theo Farmer, FAR-06 xử lý theo Farmer; I.3.2 mô tả payout 1:1 order | Phải xác định order tài chính gắn một hay nhiều Farmer và cách chia tiền, phí, hoàn tiền, voucher. |
| Một contract–một order hay nhiều lần thực hiện | I.3.2 ghi B2BContract 1:1 eligible B2B Order; permission matrix và tracking có delivery lots/milestones | Thẻ 8 hỗ trợ nhiều SellerOrder thực hiện một contract; có thể hợp lý nhưng phải sửa SRS cho khớp. |
| Một ngưỡng sỉ hay nhiều tier | FAR-03 và I.3.3 dùng WholesaleMinQuantity/WholesalePrice; WB-03 nói price tiers/matrix | Hai thuộc tính hiện tại đủ cho một ngưỡng. Nếu nhiều bậc thực sự thì cần mô hình tier; chưa được thêm bảng chỉ vì cách diễn đạt không nhất quán. |
| RetailCustomer KYC | VO-01 và JOB-08 nhắc RetailCustomer; hồ sơ và luồng đăng ký chủ yếu mô tả Farmer/Buyer | Chốt phạm vi trước khi bổ sung hồ sơ KYC người mua lẻ. |
| Voucher Farmer và stacking | UC quản lý voucher do Promotion Staff; Thẻ 8 thêm issuer Farmer, nhiều redemption trên order/seller order | Đây là lựa chọn mở rộng cần có quy tắc eligibility, quota, stacking và phân bổ discount. |
| FIFO | Thẻ 8 ghi allocation theo FIFO; UC đã đọc không quy định FIFO | Ghi thành design decision hoặc bổ sung yêu cầu; không viện Gomaa để suy ra FIFO. |
| RegionalPriceBenchmark | Đã có entity và quy tắc bất biến lịch sử, nhưng chưa rõ UC/API/job cung cấp giá | Giữ có điều kiện theo phạm vi đã nêu, bổ sung nguồn và quy trình dữ liệu. |

## 6. Cách chốt phiên bản tiếp theo

1. Chốt các quyết định nghiệp vụ trong mục 5; không đổi SRS âm thầm để hợp thức hóa ERD.
2. Sửa campaign–batch, cart–batch và QR theo quyết định đã chốt; bổ sung dữ liệu Consumer/điểm thưởng, KYC và deadline bằng chứng còn thiếu.
3. Tạo ma trận `UC/BR → dữ liệu cần lưu → entity/thuộc tính → quan hệ/ràng buộc`, ghi rõ phần nào là yêu cầu và phần nào là quyết định thiết kế.
4. Hoàn thiện PK/FK/unique/nullability, các invariant tài chính/tồn kho, snapshot và trạng thái.
5. Chạy walkthrough: checkout nhiều Farmer có một Farmer hủy; giữ tồn rồi payment hết hạn; webhook gửi lặp; pre-order Planned chuyển harvested; hai bên ký khác phiên bản; hoàn tiền từng phần; khiếu nại gần cuối cửa sổ và payout; giao từng phần hợp đồng và đối soát lượng nhận.
6. Xuất conceptual view cho SRS và logical view cho thiết kế. Chỉ gộp bảng khi xác nhận vòng đời và bản số cho phép; không lấy số lượng bảng làm tiêu chí duy nhất.

Kết quả đánh giá: **cần chỉnh sửa trước khi phê duyệt**, không có căn cứ yêu cầu cắt 46 entity xuống một số lượng định sẵn. Nhiều entity bổ sung là hợp lý; sai lệch nghiệp vụ và thiếu ràng buộc hiện đáng ưu tiên hơn việc giảm số bảng.
