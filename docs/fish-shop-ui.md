# Tiệm cá nhỏ

Trong `MainAquarium`, nhấn **Mua cá** dưới hai nút Cài đặt / Ẩn UI ở góc phải. Shop là một màn hình Canvas phủ toàn màn hình, dùng lại hồ và cá hiện tại; không chuyển scene và không tạo thêm cá.

- Royal Blue Halfmoon: **350 xu**.
- Mustard Gas Veiltail: **450 xu**.
- Koi Galaxy Plakat: **650 xu**.
- Black Orchid Crowntail: **900 xu**.
- Aqua Pearl Rosetail: **1.200 xu** (mép vây gợn sóng, xem `docs/betta-variants.md`).

Chọn thẻ cá để xem ảnh lớn, mô tả và giá. Chỉ khi nhấn **Xác nhận mua**, shop mới trừ xu, ghi quyền sở hữu và áp dụng biến thể lên cá trong hồ. **Chọn lại** hoặc quay về không trừ xu. Cá đã mua có nút **Dùng cá này** để đổi miễn phí; cá đang dùng hiển thị **Đang trong hồ**. Không đủ xu thì nút mua bị khóa. Phím Escape đóng hộp xác nhận trước, rồi mới đóng shop.

Ví khởi đầu có **1.250 xu trong game**. Ví, danh sách cá đã mua và cá đang dùng lưu chung tại PlayerPrefs `Lumaquarium.FishShop.v1`, khôi phục khi mở hồ lại. Hiện chưa có kiếm xu, nạp tiền thật, máy chủ hay đồng bộ tài khoản. Đây là dữ liệu cục bộ trên thiết bị. Mỗi biến thể chỉ mua một lần; mỗi hồ vẫn có một cá đang được chọn.

Khi mở shop, HUD hồ bị ẩn, camera bị khóa và thao tác trang trí đang làm được hủy. Khi quay lại, HUD và camera được khôi phục. Việc xem cá trong tiệm không thay đổi cá trong hồ trước khi mua hoặc chọn dùng.

## File chính

- `Assets/_Project/UI/Prefabs/FishShop.prefab`: giao diện và năm offer, có thể chỉnh giá / mô tả / variant trong Inspector.
- `Assets/_Project/Scripts/UI/FishShopScreen.cs`: chọn cá, xác nhận mua, áp dụng biến thể và cập nhật HUD.
- `Assets/_Project/Scripts/UI/FishShopInventory.cs`: ví, quyền sở hữu, cá đang dùng và lưu dữ liệu.
- `Assets/_Project/Scripts/Editor/FishShopScreenBuilder.cs`: dựng prefab, render chân dung và nối vào hồ. Menu **Lumaquarium > UI > Build Fish Shop Screen**.
- `Assets/_Project/Scripts/Editor/MainAquariumScreenBuilder.cs`: nối lại shop hiện có nếu tạo lại HUD hồ.
- `Assets/_Project/UI/Portraits/Shop/`: ảnh render từ model và variant thật, không dùng hình cá minh họa của bản HTML.
- `Tools/UI/utility_coin.svg`: nguồn vector của biểu tượng xu mới.
- `docs/previews/FishShop/`: ảnh Game View thực tế.

Danh sách cá nằm trong `LoadOffers` của builder. Thẻ cá xếp 2 cột; quá hai hàng thì danh sách cuộn dọc trong cùng khung. Khi danh sách id trong prefab khác `LoadOffers` (ví dụ vừa thêm Aqua Pearl Rosetail), chạy lại **Lumaquarium > UI > Build Fish Shop Screen**: builder render chân dung còn thiếu vào `UI/Portraits/Shop/`, dựng lại prefab (giữ GUID) rồi nối lại vào hồ. Lần dựng lại này thay các chỉnh sửa tay trong prefab.

Builder giữ bản scene trên đĩa và bản đang chỉnh trong `Assets/_Project/Scenes/Backups/` trước khi tích hợp. Nếu shop đã tồn tại, builder chỉ nối lại tham chiếu và nút mở, giữ các chỉnh sửa giao diện. Prefab được nối tới cá và HUD bằng scene overrides. Không chạy lại các builder hồ starter để thêm shop.

## Kiểm tra

Các script dưới đây dùng khóa PlayerPrefs thử riêng, không ghi đè ví hay bố cục của người chơi.

Trong Edit Mode:

```sh
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/fish-shop-inventory.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/fish-shop-scene.cs
```

Play `MainAquarium`, rồi chạy:

```sh
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/fish-shop-playmode.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/decoration-playmode.cs
```

Dừng Play Mode sau kiểm thử để khôi phục hình dáng ban đầu của cá trong scene. Kiểm thử shop bao phủ hủy mua, chống trừ xu lặp, thiếu xu, mua vừa đủ số dư, đổi cá miễn phí, tải lại dữ liệu, khôi phục cá khi khởi động, cập nhật HUD, giữ một cá và đổi mesh vây cho cả năm biến thể, và chỉ Rosetail có mép vây gợn sóng. Kiểm thử trang trí hiện có cũng được chạy lại.

Đã kiểm tra Game View dọc 450 × 974 và 320 × 568, bao gồm hộp xác nhận. Chưa build Android/iOS hoặc kiểm tra cảm ứng / hiệu năng trên thiết bị thật.
