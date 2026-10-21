# Tiệm cá nhỏ

Trong `MainAquarium`, nhấn **Mua cá** dưới hai nút Cài đặt / Ẩn UI ở góc phải. Shop là một màn hình Canvas phủ toàn màn hình, dùng lại hồ và cá hiện tại; không chuyển scene và không tạo thêm cá.

- Royal Blue Halfmoon: **350 xu**.
- Mustard Gas Veiltail: **450 xu**.
- Koi Galaxy Plakat: **650 xu**.
- Black Orchid Crowntail: **900 xu**.
- Aqua Pearl Rosetail: **miễn phí** (mép vây gợn sóng, xem `docs/betta-variants.md`). Giá 0 hiện là "Miễn phí", nút là **Nhận cá**, vẫn qua hộp xác nhận và chỉ nhận được một lần.
- Aqua Fire Betta: **miễn phí**, dùng đúng bề mặt model và bốn texture từ ZIP Meshy được cung cấp. FBX và PNG nguồn được giữ nguyên trong `Assets/_Project/Art/AquaFireBetta/Source/`. Skin dẫn xuất giữ nguyên mọi đỉnh, tam giác và UV; rig Betta có sẵn được sao chép rồi căn theo thân/vây Aqua Fire. Cá dùng lại `BettaSwimMotion`, `BettaFinPhysics` và các clip Idle/Swim/Nibble/Rest/Flare để bơi, uốn thân và vẫy vây. Clip dẫn xuất chỉ giữ kênh xoay để không kéo xương về tỉ lệ của model cũ.

Chọn thẻ cá để xem ảnh lớn, mô tả và giá. Chỉ khi nhấn **Xác nhận mua**, shop mới trừ xu, ghi quyền sở hữu và áp dụng biến thể lên cá trong hồ. **Chọn lại** hoặc quay về không trừ xu. Cá đã mua có nút **Dùng cá này** để đổi miễn phí; cá đang dùng hiển thị **Đang trong hồ**. Không đủ xu thì nút mua bị khóa. Phím Escape đóng hộp xác nhận trước, rồi mới đóng shop.

Ví khởi đầu có **1.250 xu trong game**. Ví, danh sách cá đã mua và cá đang dùng lưu chung tại PlayerPrefs `Lumaquarium.FishShop.v1`, khôi phục khi mở hồ lại. Ví này cũng dùng chung với [cửa hàng cây, lũa và đá](decoration-shop.md). Đồ trang trí mua theo từng món và được mua nhiều lần; quyền sở hữu cá vẫn mua một lần. Hiện chưa có kiếm xu, nạp tiền thật, máy chủ hay đồng bộ tài khoản. Đây là dữ liệu cục bộ trên thiết bị. Mỗi biến thể chỉ mua một lần; mỗi hồ vẫn có một cá đang được chọn.

Khi mở shop, HUD hồ bị ẩn, camera bị khóa và thao tác trang trí đang làm được hủy. Khi quay lại, HUD và camera được khôi phục. Việc xem cá trong tiệm không thay đổi cá trong hồ trước khi mua hoặc chọn dùng.

## File chính

- `Assets/_Project/UI/Prefabs/FishShop.prefab`: giao diện và sáu offer, có thể chỉnh giá / mô tả / variant trong Inspector.
- `Assets/_Project/Scripts/Editor/AquaFireBettaBuilder.cs`: dựng material URP từ texture gốc, prefab và variant Aqua Fire. Menu **Lumaquarium > Betta > Import Aqua Fire Betta and Update Shop**. Roughness được đổi thành smoothness trong alpha của map metallic dẫn xuất; PNG gốc không thay đổi.
- `Assets/_Project/Scripts/Editor/AquaFireBettaSkinning.cs`: căn bản sao rig Betta, tạo skin weights và bindposes trên bề mặt gốc, dựng controller override và các clip chỉ xoay. Đầu ra ở `Assets/_Project/Art/AquaFireBetta/`.
- `Assets/_Project/Scripts/BettaRiggedModel.cs`: nối Animator của model nhập vào AI hồ, khôi phục Animator cũ khi gỡ model và nối lại khi bật model.
- `Assets/_Project/Scripts/BettaAppearance.cs`: dùng prefab riêng khi variant có `modelPrefab`; ẩn rig cũ khi dùng Aqua Fire và khôi phục khi đổi về các cá có rig.
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
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/aqua-fire-rig.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/aqua-fire-face.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/aqua-fire-views.cs
```

Play `MainAquarium`, rồi chạy:

```sh
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/fish-shop-playmode.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/aqua-fire-shop.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/aqua-fire-motion.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/aqua-fire-turns.cs
unity command eval_file --caller plugin --skill unity-cli --file docs/tests/decoration-playmode.cs
```

Dừng Play Mode sau kiểm thử để khôi phục hình dáng ban đầu của cá trong scene. Kiểm thử shop bao phủ hủy mua, chống trừ xu lặp, thiếu xu, mua vừa đủ số dư, đổi cá miễn phí, tải lại dữ liệu, khôi phục cá khi khởi động, cập nhật HUD, giữ một cá và đổi mesh vây cho năm biến thể dùng model chung, và chỉ Rosetail có mép vây gợn sóng. Kiểm thử Aqua Fire kiểm tra nhận miễn phí cả khi ví có đúng 0 xu, dùng mesh/texture nguồn, đổi về rig cũ, tắt/bật model và khôi phục khi tải lại.

`aqua-fire-motion.cs` theo dõi 40 frame thực sau khi animation ổn định, rồi ghi kết quả vào `Temp/aqua-fire-motion-check.json`; đợi kết quả này trước khi chạy script đổi cá khác. Kiểm tra bao gồm mesh thực sự biến dạng, xương đuôi chuyển động, vị trí rig đã căn được giữ nguyên và Animator nhận lệnh từ AI hồ. Bài kiểm tra khôi phục variant cũ khi kết thúc, không ghi vào ví hoặc lựa chọn đã lưu.

`aqua-fire-face.cs` kiểm tra 29.152 đỉnh phía trước, bao gồm miệng và hai vùng mắt, ở 20 tư thế của năm clip và khi xoay mạnh các vây gần đầu. Mắt, môi và má chỉ nhận trọng số từ Head; vùng cổ chuyển tiếp mềm sang cột sống. Việc này ngăn vây ngực/vây bụng kéo giãn mặt. `aqua-fire-views.cs` xuất ảnh chính diện và góc nghiêng từ bề mặt đã bake, cùng ảnh bề mặt gốc để đối chiếu, vào `docs/previews/AquaFireFix/`.

`aqua-fire-turns.cs` tạo cá kiểm thử riêng, chạy tám lượt rẽ trái/phải 90° và 180° ở tốc độ chậm và tốc độ tối đa của AI hồ (100°/s, gia tốc 220°/s²), rồi ghi `Temp/aqua-fire-turns-check.json`. Đợi file kết quả trước khi chạy kiểm thử Play Mode tiếp theo. Bài kiểm tra đo độ uốn thân thực, độ ổn định mặt, tính liên tục của chuyển động và tỉ lệ chiều dài 1.656 cạnh được lấy mẫu trong vùng thân giữa; ảnh chụp giữa lượt rẽ được lưu cùng thư mục preview. Tham số chuyển động chỉ được chỉnh trên prefab Aqua Fire: thân đi theo đầu chậm hơn, góc mỗi khớp được giới hạn và nhịp quẫy cuống đuôi được giảm để tránh cộng dồn gây nén thân. Các cá dùng rig chung giữ thông số hiện có.

Sau sửa mặt và chuyển hướng: face **5**, rig **9**, rẽ **40**, Aqua Fire shop **17** và shop Play Mode **31** đều đạt (102 kiểm tra). Mặt giữ hình ở 20 tư thế; sai lệch hình dạng mặt khi rẽ dưới 0,0001 mm. Thân sau lệch khoảng 10,5–11,4 cm so với tư thế quay cứng theo đầu; các cạnh thân được lấy mẫu có tỉ lệ chiều dài 0,761–1,257, không vượt ngưỡng kiểm tra 0,75–1,30. FBX và bốn PNG tiếp tục khớp SHA-256 của ZIP gốc. Console cuối vòng không có lỗi/cảnh báo. Chi tiết ở [verification.json](previews/AquaFireFix/verification.json); ảnh [chính diện](previews/AquaFireFix/front-inspection.png), [rẽ chậm](previews/AquaFireFix/turn-inspection.png) và [rẽ theo tốc độ game](previews/AquaFireFix/turn-gameplay-inspection.png). Chưa kiểm tra trên thiết bị Android/iOS thật.

Nguồn FBX và bốn PNG đã được đối chiếu SHA-256 với ZIP gốc, tất cả khớp. Kiểm tra bind pose so sánh trực tiếp bề mặt gốc và skin dẫn xuất, sai lệch dưới 0,01 mm. Ảnh shop ở `docs/previews/FishShop/AquaFire-shop-320x568.png`. Chưa build hoặc đo hiệu năng Android/iOS cho model mới.

Sau khi gắn rig: kiểm tra rig **9**, chuyển động qua 40 frame **7**, Aqua Fire shop **17**, shop Play Mode **31**, inventory **15**, scene **10**, trang trí Play Mode **27** đều đạt. Console cuối vòng kiểm tra không có lỗi hoặc cảnh báo. Ảnh cá đã gắn rig trong hồ: `docs/previews/FishShop/AquaFire-rigged.png`.

Đã kiểm tra Game View dọc 450 × 974 và 320 × 568, bao gồm hộp xác nhận. Chưa build Android/iOS hoặc kiểm tra cảm ứng / hiệu năng trên thiết bị thật.
