# Danh mục địa chỉ checkout

Checkout dùng địa chỉ 3 cấp (tỉnh/thành → quận/huyện → phường/xã) lấy trực tiếp từ API
địa giới của Goship, để mã gửi sang API báo giá (`address_to.city`, `address_to.district`)
luôn khớp với danh mục của hãng. Bộ JSON 2 cấp đóng gói trước đây đã được gỡ bỏ.

## API

Các endpoint giữ policy JWT `RetailCustomer` của checkout:

| Endpoint | Gọi Goship | Response |
| --- | --- | --- |
| `GET /api/checkout/provinces` | `GET /cities` | Array `{ code, name }` |
| `GET /api/checkout/provinces/{code}/districts` | `GET /cities/{code}/districts` | Array `{ code, provinceCode, name }` |
| `GET /api/checkout/districts/{code}/wards` | `GET /districts/{code}/wards` | Array `{ code, districtCode, name }` |
| `GET /api/checkout/areas` | – | Cấu hình `Checkout:Areas` (không dùng làm danh mục) |

Kết quả được cache trong bộ nhớ theo `Goship:CatalogCacheHours` (mặc định 24 giờ).
Phản hồi lỗi hoặc rỗng không được cache; endpoint trả `[]` khi Goship không phản hồi.
Mã phường của Goship là số, được chuyển thành chuỗi.

## Chuẩn hóa

Khi quote, revalidate lúc đặt đơn hoặc lưu địa chỉ hồ sơ, backend kiểm tra
`provinceCode` → `districtCode` → `wardCode` thuộc đúng cấp cha. Sai mã hoặc sai quan hệ
trả `ADDRESS_INVALID`. Tên tỉnh/quận/phường lấy từ Goship, không tin tên do client gửi.
`catalogVersion` cố định là `goship-v2` để địa chỉ trong snapshot quote vẫn bằng địa chỉ
chuẩn hóa lại lúc đặt đơn.

Đơn hàng lưu bản sao mã, tên tỉnh/quận/phường, chi tiết và phiên bản địa chỉ
(`DistrictCodeSnapshot`, `DistrictNameSnapshot` được thêm bởi
`database/20261025_goship_three_level_address.sql`). `DeliveryAddress.DistrictCode` lưu mã
quận của địa chỉ mặc định.

Địa chỉ đã lưu theo danh mục cũ (không có mã quận Goship) cần được người dùng chọn lại
trên checkout hoặc trang hồ sơ.
