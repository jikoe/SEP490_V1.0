# Địa giới Việt Nam dùng trong checkout

Nguồn người dùng chọn: https://github.com/adminvsrm/vndata_hanhchinh, nhánh main,
đóng gói ngày 18/10/2026 cùng LICENSE. Không gọi GitHub khi app xử lý checkout.

- province.json: 34 phần tử dạng object, khóa theo mã tỉnh/thành.
- ward.json: 3.321 phần tử dạng object, parent_code liên kết tỉnh/thành.
- Giữ mã string của bộ dữ liệu này; không tự đồng nhất với mã của carrier.

SHA-256:

```text
province.json F828968031D495989AC86F257152DACE7FDB360F4925FC81080479172CD4D836
ward.json     27E3A6C5F78464886D0C06D0E8B3819A1E7F76293A12CDFEB5224D75AA1D406E
```

VietnamAddressCatalog tạo CatalogVersion từ SHA-256 của hai nội dung nối theo thứ tự
province rồi ward. Tên địa giới/code/version được chụp trên quote và order address.
Nguồn có địa chỉ hợp lệ không đồng nghĩa phương thức giao hàng phục vụ địa chỉ đó.
