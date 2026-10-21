# Cá và tiệm cá

## Cá trong game

Mọi con cá đều đi qua quy trình Blender trong `../betta-splendens/pipeline/` (xem README ở đó): rig mẫu M3 được khớp vào model mới, rồi Unity tô màu lên bản trắng bằng variant mask riêng của từng con.

| Cá | Thư mục | Mã shop | Giá |
|---|---|---|---|
| Koi Nemo Betta | `Assets/_Project/Art/Fish/KoiNemoRig/` | `koi-nemo` | miễn phí |
| Plakat Koi | `Assets/_Project/Art/Fish/Plakat/` | `plakat` | miễn phí |

Người chơi mới nhận **Plakat Koi**: đó là variant gán trên `BettaAppearance` của `BettaRoot` trong MainAquarium. Nếu save có cá không còn bán, hồ cũng quay về cá này.

Thêm cá mới: chép `<Name>.fbx`, `<Name>_VariantMask.png`, `<Name>_BaseColor.png` vào `Art/Fish/<Name>/`, tạo `<Name>.asset` (Create › Lumaquarium › Pipeline Fish), rồi chạy **Lumaquarium › Fish › Import Pipeline Fish and Update Shop** khi MainAquarium đang mở và không ở Play Mode. Shop liệt kê mọi Pipeline Fish có `inShop`.

## Cách cá được dựng

- `BettaRoot` trong scene chỉ giữ AI (`BettaFishAI`), thức ăn và `BettaAppearance`. Không còn thân Betta dùng chung.
- `BettaAppearance` tạo prefab của variant làm con của `BettaRoot`, rồi tô màu các renderer có `_VariantMask` bằng MaterialPropertyBlock (shader `Lumaquarium/Betta Lit`, `Shaders/BettaVariant.hlsl`).
- Prefab mỗi cá có `BettaSwimMotion`, `BettaFinPhysics`, `BettaReactions` và `BettaRiggedModel`. `BettaRiggedModel` nối Animator của cá vào AI hồ.
- `Art/Betta/Betta.fbx` là rig mẫu M3, chỉ dùng làm nguồn clip cho `Art/Betta/Betta.controller` (Idle/Swim/Nibble/Rest/Flare). Mỗi cá chạy controller này qua một override controller với clip riêng. Dựng lại controller: **Lumaquarium › Fish › Rebuild Betta Animator Controller**.
- Pattern màu nằm trong `Data/Betta/Patterns/` (Solid, Bicolor, Butterfly, Marble, Koi, Dragon, Galaxy).

## Tiệm cá

Mở từ nút Cửa hàng trên dock hoặc chip ví. Chọn thẻ cá để xem ảnh lớn, mô tả và giá. Chỉ khi nhấn **Xác nhận** shop mới trừ xu, ghi quyền sở hữu và đổi cá trong hồ. Cá giá 0 hiện "Miễn phí" và chỉ nhận được một lần. Cá đã có thì đổi qua lại miễn phí; cá đang dùng hiện **Đang trong hồ**.

Ví khởi đầu 1.250 xu. Ví, cá đã có và cá đang dùng lưu chung tại PlayerPrefs `Lumaquarium.FishShop.v1`, dùng chung với [cửa hàng trang trí](decoration-shop.md). Đây là dữ liệu cục bộ, chưa có máy chủ.

## File chính

- `Scripts/BettaAppearance.cs`, `Scripts/BettaVariant.cs`: hiện và tô màu cá.
- `Scripts/Editor/PipelineFish.cs`, `PipelineFishImporter.cs`: spec cá và bước nhập vào Unity.
- `Scripts/Editor/BettaControllerBuilder.cs`: controller animation dùng chung.
- `Scripts/UI/FishShopScreen.cs`, `FishShopInventory.cs`: shop, ví, cá đang dùng.
- `Scripts/Editor/FishShopScreenBuilder.cs`: dựng `UI/Prefabs/FishShop.prefab`, render ảnh vào `UI/Portraits/Shop/` và nối vào hồ (**Lumaquarium › UI › Build Fish Shop Screen**).

## Kiểm tra

`docs/tests/fish-shop-inventory.cs` (Edit Mode) kiểm tra ví và quyền sở hữu bằng khóa PlayerPrefs thử riêng. `docs/tests/fish-shop-scene.cs` kiểm tra mọi offer trong scene hợp lệ.

Ngày 21/10/2026, sau khi bỏ cá cũ: biên dịch không lỗi. Play với save trống thì nhận Plakat Koi, HUD đúng tên và ảnh. Plakat chuyển Idle/Swim, đi ăn và flare khi chạm. Nhận Koi Nemo qua shop: đổi model, Animator chuyển sang Koi Nemo, Rest/Swim chạy. Console không có lỗi. Chưa kiểm tra trên thiết bị thật.
