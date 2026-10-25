# Ảnh nông sản từ Pixabay

Ngày tải: 25/10/2026. Đối chiếu với các bản ghi `ProductImage` trong `../../../database/Create_DB_First_Time.sql`.

| SeedId sản phẩm | Tên sản phẩm trong SQL | Tên file | Kích thước | Tác giả / nguồn Pixabay |
| --- | --- | --- | --- | --- |
| 1 | Xà Lách Thủy Tinh Đà Lạt VietGAP | `xalach-1.jpg` | 1280 × 854 | [Engin_Akyurt](https://pixabay.com/photos/lettuce-green-food-vegetable-fruit-6757475/) |
| 2 | Xoài Cát Hòa Lộc Cái Bè Loại 1 | `xoai-1.jpg` | 1280 × 832 | [candoyi](https://pixabay.com/photos/mango-fruit-still-life-1534061/) |
| 3 | Gạo ST25 Lúa Tôm Sinh Thái | `gaost25-1.jpg` | 1280 × 974 | [congerdesign](https://pixabay.com/photos/rice-rice-grains-food-meal-3506194/) |
| 4 | Cam Vinh Hưng Yên Canh Tác Hữu Cơ | `cam-1.jpg` | 1280 × 720 | [NoName_13](https://pixabay.com/photos/oranges-fruits-citrus-citrus-fruits-1995079/) |
| 5 | Cà Phê Robusta Honey Đắk Lắk | `caphe-1.jpg` | 1280 × 805 | [gosiak1980](https://pixabay.com/photos/coffee-beans-coffee-grains-seeds-8286087/) |

Ảnh minh họa đúng loại nông sản. Nguồn ảnh không xác nhận giống, địa phương, chứng nhận VietGAP/hữu cơ hay phương pháp sơ chế ghi trong tên sản phẩm. Ví dụ ảnh xoài không được xác nhận là xoài Cát Hòa Lộc; ảnh gạo không được xác nhận là ST25.

Các trang nguồn ghi giấy phép [Pixabay Content License](https://pixabay.com/service/license-summary/). Ảnh được tải dưới dạng JPEG, không chỉnh sửa nội dung.

Tên file khớp phần tên cuối của URL `/media/img/...` trong SQL. Bản dùng để phục vụ ứng dụng được lưu tại `../media/img/` (tức `wwwroot/media/img/`). Backend phục vụ thư mục này qua `/media/img/`; frontend Vite đã có proxy `/media` đến backend. Các ảnh trong thư mục `wwwroot/img/` này được giữ lại làm bản gốc cùng thông tin nguồn.
