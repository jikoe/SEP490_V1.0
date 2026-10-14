# Use Case Diagram Design for AgriSustain

## Notation

| Notation | Meaning |
|---|---|
| Solid association | An actor directly initiates or participates in a use case. |
| `<<include>>` | The source use case always invokes the target use case as a required sub-function. |
| `<<extend>>` | The source use case is optional/conditional behavior added to the target use case. |

## 4.3.1 Common Authentication and Profile Management

| Actor | Associated use cases |
|---|---|
| Guest | Register Consumer or Farmer Account; Register Buyer B2B Account; Reset Password |
| Registered User (Consumer, Farmer, Buyer B2B, Administrator) | Log In; Log Out; Manage Profile; Change Password |

Không vẽ quan hệ `include` hoặc `extend` tại đây vì đăng ký, hoàn tất hồ sơ và đặt lại mật khẩu có thể diễn ra ở các thời điểm khác nhau và tạo ra các kết quả độc lập.

## 4.3.2 UCs for Guest

| Actor | Associated use cases |
|---|---|
| Guest | Browse Marketplace; Search and Filter Products; View Product Details; View Public Farm/Seller Profile |

| Source UC | Relationship | Target UC | Lý do |
|---|---|---|---|
| Search and Filter Products | `<<extend>>` | Browse Marketplace | Tìm kiếm/lọc là hành vi tùy chọn khi Guest đang duyệt marketplace. |
| View Product Details | `<<extend>>` | Browse Marketplace | Guest có thể mở một sản phẩm đã chọn trong khi duyệt marketplace. |
| View Public Farm/Seller Profile | `<<extend>>` | View Product Details | Guest có thể mở hồ sơ công khai của nông trại/người bán từ trang chi tiết sản phẩm. |

## 4.3.3 UCs for Consumer

| Actor | Associated use cases |
|---|---|
| Consumer | Browse Marketplace; View Product Detail; View Product Traceability; Manage Shopping Cart; Place Retail Order; Pay Retail Order; Track Retail Order; Submit Product Review; Submit Order Complaint |
| VietQR/PayOS Payment Gateway | Pay Retail Order |

| Source UC | Relationship | Target UC | Lý do |
|---|---|---|---|
| Place Retail Order | `<<include>>` | Pay Retail Order | Luồng mua lẻ yêu cầu hệ thống tạo yêu cầu thanh toán QR sau khi checkout; cổng thanh toán xác nhận kết quả sau đó qua webhook. |
| View Product Detail | `<<extend>>` | Browse Marketplace | Consumer có thể mở một sản phẩm đã chọn trong khi duyệt marketplace. |
| View Product Traceability | `<<extend>>` | View Product Detail | Truy xuất nguồn gốc là thông tin tùy chọn mà Consumer có thể mở từ trang chi tiết sản phẩm. |

`Submit Product Review` và `Submit Complaint` đều có tiền điều kiện là đơn hàng đã hoàn tất. Chúng không được mô hình hóa là `extend` của theo dõi đơn hàng vì mỗi chức năng là một mục tiêu độc lập của người dùng.

## 4.3.4 UCs for Buyer B2B

| Actor | Associated use cases |
|---|---|
| Buyer B2B | Search Suppliers; View Supplier Capacity Profile; Create Wholesale Order; Create Pre-Order Commitment; Create Offtake Contract Proposal; Sign Offtake Contract; Track B2B Fulfillment; Manage Buyer Subscription |
| Farmer | Sign Offtake Contract |

| Source UC | Relationship | Target UC | Lý do |
|---|---|---|---|
| View Supplier Capacity Profile | `<<extend>>` | Search Suppliers | Xem hồ sơ năng lực của một nhà cung cấp được chọn là bước tùy chọn sau khi có kết quả tìm kiếm. |

`Sign Offtake Contract` có tiền điều kiện là đề xuất hợp đồng đã tồn tại và được hai bên đồng ý; đây không phải `<<extend>>`, vì thao tác ký không bổ sung hành vi vào lúc tạo đề xuất. Farmer/Cooperative cũng được liên kết trực tiếp với UC ký để thể hiện tính hai bên của hợp đồng. Đơn mua sỉ và đặt trước cần có nhà cung cấp được chọn làm tiền điều kiện; chúng không phải quan hệ `include` với tìm kiếm nhà cung cấp vì Buyer có thể sử dụng lại một quan hệ nhà cung cấp đã có.

## 4.3.5 UCs for Farmer / Cooperative

| Actor | Associated use cases |
|---|---|
| Farmer / Cooperative | Manage Crop Seasons; Record E-Farming Logs; Manage Product Listing; Manage Traceability Batches; Manage Pre-Order Campaign; Submit Certificate Dossier; Process Order; Manage Offtake Contract; View Revenue Dashboard |

Không vẽ quan hệ `include` hoặc `extend` giữa các UC của Farmer. `Manage Pre-Order Campaign` cần tham chiếu một mùa vụ đã có, nhưng không phải lúc nào cũng thực hiện `Manage Crop Seasons` trong cùng lượt tương tác; đó là tiền điều kiện/dữ liệu tham chiếu, không phải quan hệ UML `<<include>>`.

Quản lý lô hàng, nhật ký canh tác và chứng nhận có liên kết dữ liệu với nhau, nhưng vẫn là các use case riêng vì mỗi chức năng đáp ứng một mục tiêu khác nhau của Farmer.

## 4.3.6 UCs for Administrator

| Actor | Associated use cases |
|---|---|
| Administrator | Monitor Platform Operations; Review KYC Application; Manage User Account Status; Moderate Product Listing; Manage Product Categories; Resolve Complaint; Manage Promotions; Moderate Product Review; Manage Subscription Tiers |

Không vẽ quan hệ `include` hoặc `extend` giữa các UC của Admin. Đây là các mục tiêu quản trị độc lập, mỗi UC có quyền hạn, quyết định và kết quả ghi nhận riêng. Dashboard chỉ là điểm vào để theo dõi/điều hướng, không phải chức năng con bắt buộc của duyệt, kiểm duyệt hay cấu hình.
