# 4.4 Permission Matrix

**Full** = được thực hiện không hạn chế trong phạm vi vai trò và dữ liệu được cấp quyền.  
**Restricted** = chỉ được thực hiện khi thỏa điều kiện ở chú thích.  
**No** = không được phép.

| Entity | Action | Admin | Farmer / Cooperative | Buyer B2B | Consumer | Guest |
|---|---|---|---|---|---|---|
| Account | Register account | No | Full | Full | Full | Full |
| Account | Log in / log out / reset password | Full | Full | Full | Full | Restricted ¹ |
| Account | View/update own profile | Full | Full | Full | Full | No |
| Account | Submit KYC dossier | No | Full | Full | No | No |
| Account | Approve/reject KYC dossier | Full | No | No | No | No |
| Account | Search/view user directory | Full | No | No | No | No |
| Account | Suspend/unlock/activate account | Full | No | No | No | No |
| Marketplace | Browse/search public products | Full | Full | Full | Full | Full |
| Marketplace | View product details | Full | Full | Full | Full | Full |
| Farm / supplier profile | View public profile | Full | Full | Full | Full | Full |
| Traceability | View QR traceability data | Full | Full ² | Full | Full | Restricted ³ |
| Farm and crop season | Create/update own farm and crop seasons | No | Full | No | No | No |
| Farming log | Create/update own e-farming logs | No | Full | No | No | No |
| Certificate dossier | Submit/view own certificate dossier | Restricted ⁴ | Full | No | No | No |
| Product listing | Create/update/archive own listing | No | Full | No | No | No |
| Product listing | Approve/reject/moderate listing | Full | No | No | No | No |
| Product batch | Create/update own batch, stock and QR traceability | No | Full | No | No | No |
| Pre-order campaign | Create/update/close own campaign | No | Full | No | No | No |
| Shopping cart | Add/update/remove cart items | No | No | No | Full | No |
| Retail order | Checkout and place retail order | No | No | No | Full | No |
| Payment | Initiate QR payment for own retail order | No | No | Restricted ⁵ | Full | No |
| Order | View own purchase/order history | Restricted ⁶ | Full ⁷ | Full | Full | No |
| Order | Confirm/reject/pack/hand over assigned order | No | Full | No | No | No |
| B2B procurement | Search suppliers and production regions | No | Restricted ⁸ | Full | No | No |
| B2B order | Create wholesale order | No | No | Full | No | No |
| Pre-order commitment | Create commitment/reservation | No | No | Full | No | No |
| Offtake contract | Create contract proposal | No | No | Full | No | No |
| Offtake contract | Review/sign/manage own contract | Restricted ⁹ | Full | Full | No | No |
| B2B subscription | View/manage own subscription | No | No | Full | No | No |
| Subscription tier | Create/update/activate tier | Full | No | No | No | No |
| Product review | Submit review | No | No | No | Full ¹⁰ | No |
| Product review | Moderate/remove violating review | Full | No | No | No | No |
| Complaint | Submit complaint | No | No | No | Full ¹¹ | No |
| Complaint | View/resolve complaint | Full | No | No | No | No |
| Voucher / promotion | Create/update/activate voucher | Full | No | No | No | No |
| Voucher / reward points | Apply eligible voucher/reward points | No | No | No | Full ¹² | No |
| Product category | Create/update/activate category hierarchy | Full | No | No | No | No |
| Dashboard | View platform KPI and pending work | Full | No | No | No | No |
| Dashboard | View own revenue/cash-flow dashboard | No | Full | No | No | No |
| Notification | View own notifications | Full | Full | Full | Full | No |

## Notes

1. Guest chỉ có thể khởi tạo đăng ký hoặc đặt lại mật khẩu; các thao tác đăng nhập/đăng xuất yêu cầu đã có tài khoản.
2. Farmer xem được dữ liệu truy xuất của lô hàng thuộc sở hữu của mình, bao gồm dữ liệu nội bộ cần thiết để quản lý.
3. Guest chỉ xem được QR/lô hàng đã công khai, còn hiệu lực và không chứa thông tin riêng tư.
4. Admin chỉ được xem hồ sơ chứng nhận để kiểm tra hoặc phục vụ kiểm duyệt, không được tự tạo hoặc sửa hồ sơ của Farmer.
5. Buyer B2B chỉ được thanh toán khi đơn mua sỉ hoặc khoản đặt cọc Pre-order đã hỗ trợ thanh toán trực tuyến; chức năng này cần được chốt trong SRS trước khi phát triển.
6. Admin được xem toàn bộ đơn hàng để giám sát và xử lý vấn đề, nhưng không thay mặt Buyer/Consumer tạo đơn hoặc thanh toán.
7. Farmer chỉ xem các đơn có sản phẩm/lô hàng của mình và được phép xử lý.
8. Farmer chỉ xem thông tin công khai của nhà cung cấp khác; không dùng chức năng tìm nguồn cung B2B như Buyer.
9. Admin chỉ xem hợp đồng khi cần giải quyết tranh chấp hoặc kiểm tra tuân thủ; không ký thay các bên.
10. Consumer chỉ đánh giá sản phẩm thuộc đơn hàng của mình sau khi đơn hoàn tất; tối đa một đánh giá cho mỗi sản phẩm/đơn hàng.
11. Consumer chỉ khiếu nại đơn hàng của mình trong thời hạn và trạng thái được chính sách cho phép.
12. Consumer chỉ áp dụng voucher hoặc điểm thưởng khi đáp ứng điều kiện giá trị đơn hàng, thời hạn và giới hạn sử dụng.
