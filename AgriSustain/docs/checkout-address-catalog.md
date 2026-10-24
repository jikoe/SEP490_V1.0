# Danh mục địa chỉ checkout

Checkout sử dụng JSON đóng gói từ `adminvsrm/vndata_hanhchinh`, gồm 34 tỉnh/thành và
3.321 phường/xã. Nguồn, giấy phép và checksum nằm tại
`AgriSustain/Infrastructure/Data/Geography/README.md`. Không gọi GitHub trong request.

## API

Các endpoint giữ policy JWT `RetailCustomer` của checkout:

| Endpoint | Response |
| --- | --- |
| `GET /api/checkout/provinces` | Array `{ code, name }` của toàn bộ tỉnh/thành |
| `GET /api/checkout/provinces/{code}/wards` | Array `{ code, provinceCode, name }` thuộc tỉnh đã chọn; mã tỉnh không có trả `[]` |
| `GET /api/checkout/areas` | Cấu hình vùng phục vụ và phí giao hàng hiện có, không dùng làm danh mục hành chính |

Mã địa giới là string của bộ dữ liệu; không suy ra mã hãng vận chuyển. Frontend chọn
tỉnh/thành rồi phường/xã, nhập địa chỉ chi tiết; không yêu cầu quận/huyện.
`CheckoutAddress.District` giữ lại để tương thích payload cũ, nhưng không bắt buộc và
được chuẩn hóa thành chuỗi rỗng khi tính quote.

Khi quote hoặc revalidate lúc đặt đơn, backend kiểm tra cặp `provinceCode`/`wardCode`
trong JSON. Mã sai hoặc phường không thuộc tỉnh trả `ADDRESS_INVALID`. Backend lấy
tên tỉnh/phường từ catalog, tính `catalogVersion` bằng SHA-256 của hai file JSON nối
theo thứ tự province rồi ward; không tin tên hoặc phiên bản do client gửi.
Đơn hàng lưu bản sao mã, tên, chi tiết và phiên bản địa chỉ trong SQL hiện có.

## Vùng phục vụ

Danh mục địa giới độc lập với `Checkout:Areas`. Chọn được một địa chỉ không có nghĩa
địa chỉ đó được phục vụ. Quote vẫn yêu cầu vùng cấu hình khớp `provinceCode` và
`wardCode`, dùng phí/số ngày từ cấu hình; không còn đối chiếu quận/huyện cũ.
Mỗi cặp mã chỉ được cấu hình một lần.

`Areas: []` vẫn cho phép dropdown hiển thị toàn bộ danh mục nhưng quote trả `MSG117`
vì chưa có chính sách giao hàng. Không tự thêm phí hay mặc định miễn phí/toàn quốc.

Ví dụ cấu trúc một vùng (phí/số ngày phải được dự án xác định trước khi sử dụng):

```text
ProvinceCode: "11"
Province: "Thành phố Hà Nội"
District: ""
WardCode: "267"
Ward: "Xã Minh Châu"
StandardFee / ExpressFee: phí mỗi farm theo chính sách dự án
StandardDays / ExpressDays: số ngày giao theo chính sách dự án
```

JSON được copy vào output build và publish. Sau khi cập nhật bộ dữ liệu cần triển khai
lại và khởi động lại backend; catalog được nạp một lần trong singleton.
Địa chỉ đã lưu có mã không còn hợp lệ cần được người dùng chọn lại trên checkout.

Lần tích hợp này không chạy test theo yêu cầu người dùng.
