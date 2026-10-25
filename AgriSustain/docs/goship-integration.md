# Tích hợp tính phí Goship

Khi khách chọn phương thức vận chuyển (với địa chỉ nhận hợp lệ), checkout gọi API báo giá Goship `POST /rates` cho từng nông trại trong giỏ hàng và hiển thị tổng cước. Phí giao hàng là tổng cước của các kiện gửi riêng từ từng nông trại; từng khoản phí được lưu vào đơn của đúng nông trại. Khi khách xác nhận checkout, hệ thống dùng snapshot báo giá đã lưu, không gọi Goship thêm lần nữa.

Nguồn: [Goship API – Lấy biểu phí](https://doc.goship.io/api/shipment/rate), [Authentication](https://doc.goship.io/getting-started/authentication), [Thiết lập cơ bản](https://doc.goship.io/getting-started/installation).

## Lời gọi API

```http
POST {BaseUrl}rates
Accept: application/json
Content-Type: application/json
Authorization: Bearer <AccessToken>

{
  "shipment": {
    "address_from": { "city": "<mã tỉnh lấy hàng>", "district": "<mã quận lấy hàng>" },
    "address_to":   { "city": "<ProvinceCode khách>", "district": "<DistrictCode khách>" },
    "parcel": { "cod": 0, "amount": <giá trị hàng>, "width": 20, "height": 20, "length": 30, "weight": <gram> }
  }
}
```

- `address_to` lấy từ địa chỉ checkout 3 cấp, vốn là mã của chính Goship (xem `checkout-address-catalog.md`).
- `weight` là tổng khối lượng theo gram của các dòng thuộc nông trại. Hệ thống chỉ quy đổi đơn vị `kg` và `g`; sản phẩm dùng đơn vị khác sẽ không được báo giá.
- `amount` là tạm tính tiền hàng của nông trại (dùng để Goship tính phí bảo hiểm).
- `cod = 0` vì checkout chỉ thanh toán trước (PayOS/VietQR).
- Kích thước kiện dùng giá trị cấu hình `ParcelLength/Width/Height` (cm).

## Chọn báo giá

Goship trả danh sách báo giá của nhiều hãng/dịch vụ; hệ thống dùng `total_fee`:

| Phương thức | Quy tắc |
| --- | --- |
| `Standard` (Giao tiêu chuẩn) | Báo giá có `total_fee` thấp nhất |
| `FreshExpress` (Giao nhanh nông sản) | Báo giá rẻ nhất có `service` chứa "Nhanh" hoặc "Hỏa tốc" |

- Không có báo giá phù hợp → `MSG117` (Goship chưa có dịch vụ phù hợp cho địa chỉ).
- Lỗi kết nối, HTTP lỗi (kể cả 422 thiếu trường), thiếu token hoặc thiếu điểm lấy hàng → `SHIPPING_RATE_UNAVAILABLE`.

Ngày giao dự kiến là cấu hình ước tính riêng (`EstimatedStandardDeliveryDays`, `EstimatedFreshExpressDeliveryDays`), không phải cam kết từ API.

## Cấu hình

Không lưu token trong JSON hoặc mã nguồn. Lấy token một lần qua `POST /login` (token có hạn khoảng 10 năm):

```bash
curl -X POST https://sandbox.goship.io/api/v2/login \
  -H "Content-Type: application/json" \
  -d '{"username":"<tài khoản goship>","password":"<mật khẩu>","client_id":"<Client ID>","client_secret":"<Client Secret>"}'
```

Trên máy dev, chép `AgriSustain/AgriSustain/.env.example` thành `.env` cùng thư mục và điền token. File `.env` đã nằm trong `.gitignore`. Khi khởi động, backend nạp `.env` từ thư mục làm việc hiện tại (thư mục project khi chạy `dotnet run`). Nếu một biến đã được đặt trong môi trường thật thì giá trị đó được ưu tiên hơn giá trị trong file.

Trên server, cấu hình bằng biến môi trường hoặc secret store:

```text
Goship__Enabled=true
Goship__BaseUrl=https://sandbox.goship.io/api/v2/     # production: https://api.goship.io/api/v2/
Goship__AccessToken=<access_token>
```

Mỗi nông trại bắt buộc có điểm lấy hàng riêng theo `FarmerProfileId`, khai báo bằng mã tỉnh/quận Goship (tra bằng `GET /cities` và `GET /cities/{code}/districts`):

```json
"Goship": {
  "PickupLocations": {
    "123": { "City": "100000", "District": "100300" }
  }
}
```

**Lưu ý sandbox:** sandbox Goship chỉ trả báo giá cho hàng gửi đi từ Hà Nội (`100000`) hoặc TP.HCM (`700000`). Điểm lấy hàng ở tỉnh khác (ví dụ Lâm Đồng `670000/670100`) nhận danh sách rỗng, và checkout báo `MSG117`. Khi test trên sandbox, hãy đặt điểm lấy hàng ở Hà Nội (ví dụ `100000/100100`). Chỉ dùng mã thật của từng nông trại khi chuyển sang production. Một số tuyến liên tỉnh chỉ có dịch vụ "Tiêu chuẩn"/"Tiết kiệm", nên chọn "Giao nhanh" sẽ báo `MSG117`.

Khi `Enabled = true`, ứng dụng kiểm tra lúc khởi động: có token, số ngày giao ≥ 1, kích thước kiện > 0, ít nhất một điểm lấy hàng và mỗi điểm đủ `City`/`District`.

Danh mục địa chỉ checkout luôn gọi Goship (kể cả khi `Enabled = false`), nên cần `AccessToken` hợp lệ để khách chọn được địa chỉ.
