# Tích hợp tính phí GHTK

Khi khách chọn phương thức vận chuyển (với địa chỉ nhận hợp lệ), checkout gọi API tính phí GHTK cho từng nông trại trong giỏ hàng và hiển thị tổng cước. Phí giao hàng là tổng cước của các kiện gửi riêng từ từng nông trại; từng khoản phí được lưu vào đơn của đúng nông trại. Khi khách xác nhận checkout, hệ thống dùng snapshot báo giá đã lưu, không gọi GHTK thêm lần nữa. Địa chỉ người nhận và khối lượng được gửi theo API chính thức; lựa chọn giao tiêu chuẩn dùng vận chuyển đường bộ (`road`), giao nhanh nông sản dùng đường bay (`fly`). API trả phí và trạng thái vùng giao hàng. Ngày giao dự kiến là cấu hình ước tính riêng vì API tính phí không trả ngày giao; hai giá trị này phải được khai báo trước khi bật tích hợp.

## Cấu hình

Không lưu token trong JSON hoặc mã nguồn. Cấu hình token và mã đối tác bằng biến môi trường hoặc secret store:

```text
Ghtk__Enabled=true
Ghtk__Token=<token do GHTK cấp>
Ghtk__ClientSource=<mã đối tác do GHTK cấp>
```

Mỗi nông trại bắt buộc có điểm lấy hàng GHTK riêng theo mã `FarmerProfileId`. Không dùng điểm lấy hàng mặc định chung để tránh báo cước theo sai tuyến. Cấu hình từng nông trại:

```json
"PickupLocations": {
  "123": {
    "PickAddressId": "<mã địa chỉ lấy hàng GHTK>"
  },
  "456": {
    "Address": "Số nhà, đường",
    "Ward": "Tên phường/xã",
    "District": "Tên quận/huyện",
    "Province": "Tên tỉnh/thành phố"
  }
}
```

Nếu dùng `PickAddressId`, GHTK tra cứu địa chỉ trong tài khoản shop. Nếu dùng địa chỉ tường minh, `Address`, `Ward` và `Province` là bắt buộc. Cấu hình `EstimatedStandardDeliveryDays` và `EstimatedFreshExpressDeliveryDays` là số ngày hiển thị dự kiến, không phải cam kết từ API báo giá.

API cần khối lượng theo gram. Hệ thống hiện quy đổi đơn vị `kg` và `g`; các sản phẩm dùng đơn vị khác sẽ không được báo giá để tránh tính cước sai. GHTK cũng cần tài khoản API hoạt động và quyền mạng từ máy chủ.

Nguồn: [Tài liệu API tính phí GHTK](https://api.ghtk.vn/docs/submit-order/calculate-shipping-fee/).
