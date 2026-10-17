# Orders

Feature điều phối giỏ hàng, checkout, SellerOrder, phân bổ lô và giao nhận. Bộ khung hiện chưa có implementation nghiệp vụ.

Khi triển khai, đặt IOrderService.cs và OrderService.cs ở thư mục này. Request, response và validator nằm tại Requests/, Responses/ và Validators/ của Orders.

- Controller nhận HTTP và gọi IOrderService; Application không trả IActionResult hay HTTP status trực tiếp.
- Application điều phối transaction qua Abstractions/Persistence. EF Core DbContext và repository implementation thuộc Infrastructure.
- Order, SellerOrder, OrderItem và OrderItemBatchAllocation thuộc Domain/Entities/Orders.
- Checkout nhiều nông trại cần tách SellerOrder. Một OrderItem có thể phân bổ từ nhiều HarvestBatch.
- Pre-order giữ năng lực dự kiến, chỉ phân bổ tồn kho thực sau thu hoạch; phối hợp Features/PreOrders khi cần.
- Job hết hạn đơn và luồng HTTP dùng chung quy tắc giải phóng reservation, không tạo tồn kho hoặc giao dịch lặp lại.

Với feature khác, áp dụng cùng cách tổ chức và chỉ thêm thư mục con khi có file cần đặt vào đó.
