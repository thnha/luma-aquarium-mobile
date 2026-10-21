# Cửa hàng cây, lũa và đá

Trong **MainAquarium**, nhấn **Cây · Lũa · Đá** dưới nút Mua cá, hoặc mở **Trang trí → Cửa hàng & kho đồ**. Ba ô cây/đá/lũa trong bảng Trang trí cũng mở đúng nhóm hàng. Tiệm cá có nút sang shop trang trí và shop trang trí có nút về tiệm cá.

**Mỗi lần xác nhận mua nhận đúng 1 món. Có thể mua cùng loại nhiều lần.** Shop dùng chung ví xu với tiệm cá. Cá vẫn mua quyền sở hữu một lần; đồ trang trí quản lý theo số lượng. Không có mua bằng tiền thật hay ví mới.

Chọn thẻ để xem ảnh 3D, mô tả, giá, số trong kho và số đang đặt. Nhấn **Mua 1 món → Xác nhận mua** để cộng kho. **Chọn lại** không trừ xu. Chọn **Đặt vào hồ**, kéo tới vị trí, xoay rồi **Đặt**. Chỉ xác nhận đặt mới sử dụng một món trong kho. Hủy đặt giữ nguyên số lượng. **Cất** trả món về kho, không hoàn xu. Hết xu vẫn có thể đặt các món đã mua còn trong kho.

Bộ mới có 24 món, ngoài 3 mẫu ban đầu cũng đã được đưa vào shop, tổng cộng **27 lựa chọn**:

| Nhóm | Món | Xu/món |
|---|---|---:|
| Cây | Rong đuôi chó | 60 |
| Cây | Rong la hán | 75 |
| Cây | Cỏ Vallisneria | 80 |
| Cây | Hồng liễu Rotala | 100 |
| Cây | Ráy nana petite | 120 |
| Cây | Dương xỉ Java | 130 |
| Rêu | Rêu Java | 80 |
| Rêu | Rêu Christmas | 100 |
| Rêu | Rêu mini fiss | 110 |
| Bèo | Bèo tấm | 40 |
| Bèo | Bèo ong | 55 |
| Bèo | Bèo Nhật | 70 |
| Lũa | Khúc ngắn | 120 |
| Lũa | Thân dài | 180 |
| Lũa | Cong | 200 |
| Lũa | Phân nhánh | 230 |
| Lũa | Chùm rễ | 250 |
| Lũa | Gốc cây | 220 |
| Đá | Đá cuội suối | 70 |
| Đá | Đá tai mèo | 140 |
| Đá | Đá nham thạch | 100 |
| Đá | Đá vân Seiryu | 160 |
| Đá | Cụm đá tự nhiên | 200 |
| Tiểu cảnh | Hòn non bộ | 320 |
| Mẫu ban đầu | Ráy Anubias | 140 |
| Mẫu ban đầu | Đá suối tròn | 80 |
| Mẫu ban đầu | Lũa nhánh cổ thụ | 200 |

## Gắn cây/rêu và bèo nổi

Ráy, dương xỉ và ba loại rêu có thể trồng nền hoặc gắn trên lũa. Khi đang cầm cây/rêu, kéo lên thân lũa để bắt vào một trong ba điểm gắn. Điểm đã có cây sẽ báo vòng đỏ. Nhấn **Đặt** để xác nhận. Di chuyển/xoay lũa mang theo cây đã gắn; hủy sửa khôi phục cả cụm. Cất lũa trả cả lũa lẫn cây/rêu gắn trên đó về kho. Có thể chọn cây gắn để chuyển sang chỗ khác.

Bèo luôn đặt ở độ cao mặt nước, có rễ rủ xuống. Camera chuyển lên cao khi đặt/sửa bèo. Bèo và đồ dưới nền có thể nằm cùng tọa độ ngang. Giới hạn hồ vẫn là 24 món, tính cả cây/rêu gắn lũa; kho có thể giữ số món đã mua vượt giới hạn đang đặt.

## Dữ liệu và asset

- `Scripts/UI/FishShopInventory.cs`: giữ ví, cá và tổng số sở hữu của từng loại trang trí chung trong PlayerPrefs `Lumaquarium.FishShop.v1`. Mỗi giao dịch đọc lại dữ liệu mới nhất để hai shop không ghi đè số dư/quyền sở hữu của nhau.
- `Scripts/AquariumDecoration.cs`: số khả dụng = tổng sở hữu − số đã đặt; bố cục chính giữ khóa `Lumaquarium.Decoration.Main.v1`. Bố cục có ID từng món, ID lũa cha và vị trí gắn cục bộ, đọc được bố cục cũ chưa có ID.
- Khi mở hồ lần đầu sau nâng cấp, các món ban đầu đang có trong bố cục được ghi nhận sở hữu miễn phí đúng số lượng hiện có. Cờ chuyển đổi trong ví ngăn cộng lại khi mở hồ lần sau.
- `Scripts/AquariumDecorItem.cs`: thông tin bèo nổi, cây có thể gắn, điểm gắn lũa và đung đưa lá.
- `Scripts/AquariumDecorCatalog.cs` và `Art/Aquarium3D/DecorShop/AquariumDecorCatalog.asset`: ID, tên, nhóm, giá, prefab và ảnh từng món.
- `Scripts/UI/DecorShopScreen.cs`, `UI/Prefabs/DecorShop.prefab`: shop, xác nhận mua, kho và chuyển sang đặt đồ.
- `Scripts/Editor/AquariumDecorCatalogBuilder.cs`: dựng mesh riêng cho cây/rêu/bèo, các kiểu lũa và đá, dùng lại shader/texture sẵn có; render ảnh từ prefab thật. Không thêm dependency.
- `Scripts/Editor/DecorShopScreenBuilder.cs`: menu **Lumaquarium → UI → Build Decoration Shop**. Chạy ở Edit Mode trong MainAquarium. Lưu bản sao scene trên đĩa và bản đang chỉnh trong `Scenes/Backups/` trước khi tích hợp. Cập nhật environment prefab dùng chung và scene; không dựng lại hồ starter.
- `Scripts/Editor/FishShopScreenBuilder.cs`: nối lại cửa hàng trang trí khi nối HUD/shop cá.

Builder giữ nguyên ba kind cũ 0/1/2; 24 món mới dùng kind 3–26. Trong catalog, 24 món mới đứng trước, ba mẫu ban đầu đứng cuối. Không đổi kind hoặc ID của món đã phát hành.

## Kiểm thử và ảnh

Chạy trong thư mục `mobile` với Unity Editor/Pipeline đang mở:

```sh
# Edit Mode
unity command eval_file --caller plugin --skill unity-cli -- --file docs/tests/decor-portraits.cs --timeout 120000
unity command eval_file --caller plugin --skill unity-cli -- --file docs/tests/decor-shop-inventory.cs --timeout 120000
unity command eval_file --caller plugin --skill unity-cli -- --file docs/tests/decor-catalog-scene.cs --timeout 120000
# Play MainAquarium
unity command eval_file --caller plugin --skill unity-cli -- --file docs/tests/decor-shop-playmode.cs --timeout 120000
unity command eval_file --caller plugin --skill unity-cli -- --file docs/tests/decor-placement-input.cs --timeout 120000
unity command eval_file --caller plugin --skill unity-cli -- --file docs/tests/decor-shop-teardown.cs --timeout 120000
```

Các script dùng khóa ví/bố cục thử riêng, không mua bằng xu của người chơi. Kiểm thử đặt đồ cũ đã cập nhật fixture kho có số lượng; kiểm thử callback HUD xác minh các ô trang trí mở nhóm hàng tương ứng. Dừng Play Mode sau kiểm thử shop cá để khôi phục hình dáng cá ban đầu.

Ảnh Game View nằm trong `docs/previews/DecorShop/`. Đã đạt 301 kiểm tra trong 14 script Edit/Play Mode, biên dịch không lỗi và xác nhận game tiến khung hình. Đã kiểm tra trong Unity Editor; chưa build Android/iOS hoặc kiểm tra cảm ứng/hiệu năng trên thiết bị thật.
