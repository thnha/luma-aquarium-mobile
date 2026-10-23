# Dữ liệu shop: giá, gói quà và vật phẩm mới

Tên, mô tả và giá của mọi thứ trong shop (cá, đồ trang trí, thành hồ, cát nền, hồ) cùng các gói quà đều được sửa trong **một Google Sheet**. Unity kéo sheet về và ghi vào các catalog. Các catalog đi kèm [remote content](remote-content.md), nên đổi giá hay thêm gói quà không cần ra bản app mới.

## Bảng giá

Workbook `Art/Aquarium3D/DecorShop/decor_catalog.xlsx` có các tab sau:

| Tab | Nội dung | Lưu vào |
|---|---|---|
| Gói quà | các gói quà, xem [bên dưới](#gói-quà) | `AquariumDecorCatalog.asset` (`giftPacks`) |
| Cá | cá có `inShop` | `Art/Fish/<Tên>/<Tên>.json` (khối `shop`), asset cá, prefab shop, scene |
| Lũa · Đá · San hô · Cây · Rêu · Bèo | đồ trang trí | `AquariumDecorCatalog.asset`, `decor_catalog.csv` |
| Thành hồ · Cát nền · Hồ | kiểu vách, kiểu nền, các cỡ hồ | `Art/Aquarium3D/Surfaces/AquariumSurfaceCatalog.asset` |

Pull tìm dòng theo **id**, nên một món nằm ở tab nào cũng được. Các cột được nhận theo tên ở hàng đầu, vì vậy có thể đổi thứ tự cột. Cột xám (ảnh, kích thước, điểm, sức chứa) chỉ để xem, Pull bỏ qua. Ô để trống thì giữ giá trị cũ. Giá có thể ghi kèm dấu phân cách hàng nghìn (`1.200`). Cá, vách, nền và hồ được để giá 0 (tặng miễn phí); đồ trang trí phải có giá lớn hơn 0.

Các menu nằm ở **Lumaquarium › Decor Catalog**:

- **Google Sheet Settings…**: dán link sheet (Share › Anyone with the link, quyền Viewer), rồi bấm Pull.
- **Pull from Google Sheet**: tải cả workbook và ghi vào các catalog. Dòng nào sai thì giữ giá trị cũ và được liệt kê trong hộp thoại kết quả.
- **Export Excel (tab theo nhóm)**: ghi `decor_catalog.xlsx` từ các catalog. Import file này vào Google Sheets bằng File › Import › Upload › **Replace spreadsheet**. Thao tác Replace ghi đè cả sheet, nên menu sẽ hỏi có Pull trước không; luôn chọn **Pull rồi Export**.

### Đổi giá, tên, mô tả

1. Sửa trên Google Sheet.
2. Chạy **Pull from Google Sheet**.
3. Chạy test `ShopEconomyTests` (Window › General › Test Runner › EditMode).
4. Chạy `Tools/publish_content.sh <ios|android|all>` (Unity Editor phải đóng).

## Gói quà

Mỗi dòng của tab **Gói quà** là một món trong một gói:

| gói | id | số lượng | dùng ngay |
|---|---|---|---|
| starter_v1 | xu | 1250 | |
| starter_v1 | plakat-galaxy | 1 | x |
| starter_v1 | tank_square | 1 | x |
| starter_v1 | wall_river | 1 | x |
| starter_v1 | sand_river | 1 | x |
| starter_v1 | starter-anubias | 2 | |
| tet_2027 | xu | 500 | |

- **id**: id của một món ở bất kỳ tab nào, hoặc `xu` để tặng xu.
- **Mỗi người chơi nhận mỗi gói đúng một lần**, theo thứ tự trong tab, lúc MainAquarium mở (`AquariumDecoration.Awake`). Gói mới thêm sau này thì cả người chơi cũ cũng nhận được.
- **`starter_v1` là quà của người chơi mới.** Tab bắt buộc phải có gói này. Người chơi chuyển từ save v1 đã được tính là đã nhận gói này.
- **dùng ngay** (ghi chữ bất kỳ) chỉ áp dụng cho cá, thành hồ, cát nền và hồ, và mỗi gói chỉ được một món dùng ngay cho mỗi loại. Món đó chỉ được đưa vào dùng khi người chơi **chưa dùng món nào cùng loại**, nên quà sự kiện không thay thế lựa chọn của người chơi.
- Khi Pull, tab được kiểm tra: id phải có thật, số lượng phải ≥ 0, không có id lặp trong cùng gói, phải có `starter_v1`. Có lỗi thì giữ nguyên **toàn bộ** các gói cũ. Nếu sheet không có tab Gói quà (sheet tạo trước khi có tab này) thì các gói cũng được giữ nguyên.
- Gói sự kiện được phát ngay khi có trong catalog đã publish. Hiện chưa có ngày bắt đầu và kết thúc.

Đổi quà của người chơi mới: sửa các dòng `starter_v1`. Những thay đổi này chỉ ảnh hưởng tới người chơi chưa nhận gói, vì người đã nhận thì không nhận lại. Muốn tặng thêm cho mọi người thì tạo gói mới với tên chưa từng dùng.

## Thêm vật phẩm mới

| Loại | Khai báo | Chạy | Ra bản app mới? |
|---|---|---|---|
| Đồ trang trí | thêm món hoặc loài vào `AquariumDecorCatalogBuilder` (hoặc `*SpeciesBuilder`, `StoneCoralBuilder`), **ở cuối danh sách** | **Lumaquarium › UI › Build Decoration Shop**, rồi **Lumaquarium › UI › Build Main Aquarium Screen** | Không |
| Cá | `Art/Fish/<Tên>/` với `shop.inShop = true` và một `shopId` mới, xem [fish-config.md](fish-config.md) | **Lumaquarium › Fish › Import Pipeline Fish and Update Shop** | Không |
| Thành hồ, cát nền | thêm `Wall(...)` / `Floor(...)` vào `AquariumSurfaceBuilder` | **Lumaquarium › Build Surface Catalog** | Không |
| Hồ | thêm `Spec` vào `AquariumTank.Specs` (code runtime) | Export Excel tự thêm dòng vào tab Hồ | **Có** |

Chạy các builder ở Edit Mode, khi MainAquarium đang mở. Mọi builder và importer ở bảng trên đều lưu lại scene MainAquarium, nên chạy `git status` trước và sau để chắc không mất chỉnh sửa tay trong scene.

Sau khi tạo asset:

1. Ảnh mới: `Tools/upload_sheet_images_r2.sh` (để cột ảnh trong sheet hiện hình).
2. **Export Excel** (chọn Pull rồi Export), rồi Import vào Google Sheet với Replace spreadsheet. Món mới xuất hiện ở tab của nhóm nó.
3. Trên sheet: đặt tên, mô tả, giá. Nếu muốn tặng thì thêm dòng vào **Gói quà**.
4. **Pull from Google Sheet**, chạy `ShopEconomyTests`, rồi `Tools/publish_content.sh`. Nếu bước nào có sửa code runtime (ví dụ thêm hồ) thì ra bản app mới (`Tools/build_testflight.sh`, `Tools/build_android.sh`) và tăng Player Settings › Version.

## Quy tắc không được phá

- **Id là vĩnh viễn.** Save của người chơi và các gói quà tham chiếu tới id. Muốn đổi tên hiển thị thì sửa cột `tên`, không đổi id.
- **Id không được trùng trên toàn workbook**, kể cả với `xu`.
- **Đồ trang trí mới luôn thêm vào cuối.** Mỗi món có một số `kind` theo thứ tự, và layout hồ đã lưu dùng số này. Chèn vào giữa sẽ làm đồ người chơi đã đặt biến thành món khác. Bảng kind hiện tại nằm ở [decoration-shop.md](decoration-shop.md).
- **Không xoá món đã phát hành.** Với cá, đặt `inShop = false`. Đồ trang trí hiện chưa có cờ để ẩn khỏi shop.
- Thứ tự trong danh sách vách, nền và hồ không còn quyết định đồ mặc định nữa (đồ mặc định do gói quà quyết định). Món đầu tiên chỉ còn dùng làm phương án dự phòng khi save trỏ tới một id không còn tồn tại.

## Save của người chơi

`FishShopInventory` lưu trong PlayerPrefs `Lumaquarium.FishShop.v1` (tên key giữ nguyên dù nội dung đã là version 2):

```json
{ "version": 2,
  "wallet": [{ "id": "xu", "count": 1250 }],
  "items": [{ "id": "plakat-galaxy", "count": 1 }, { "id": "starter-stone", "count": 2 }],
  "equipped": [{ "slot": "fish", "id": "plakat-galaxy" }, { "slot": "wall", "id": "wall_river" }],
  "grantsApplied": ["starter_v1"] }
```

- **Sở hữu nghĩa là có số lượng lớn hơn 0.** Giá 0 chỉ có nghĩa là nhận miễn phí: shop hiện nút "Nhận" và món chỉ thuộc về người chơi sau khi nhận. Đồ trang trí có thể mua nhiều lần; cá, vách, nền và hồ chỉ mua một lần rồi đưa vào dùng.
- **Save v1** được chuyển sang v2 một lần lúc đọc, và bản gốc được giữ ở `Lumaquarium.FishShop.v1.v1.bak`. Mọi thứ đã sở hữu hoặc đang dùng (kể cả món miễn phí) đều thành món sở hữu. Những loại để trống trong v1 (khi đó game tự dùng món đầu tiên) được điền bằng món dùng ngay của `starter_v1`.
- **Save không đọc được** (JSON hỏng, version lạ, dữ liệu sai) được giữ nguyên: inventory rỗng, `LoadFailed` bật, mọi thao tác mua, chọn và nhận quà đều bị từ chối, và có log lỗi. Hiện chưa có thông báo hiện lên cho người chơi.
- Bản app cũ (save v1) gặp save v2 sẽ reset về mặc định. Tester đã cài bản có save v2 thì không nên cài lại bản cũ.

## Code

- `Scripts/UI/FishShopInventory.cs`: save v2, migration, mua bán, gói quà.
- `Scripts/AquariumDecorCatalog.cs`: `AquariumGiftPack`, `AquariumGift`.
- `Scripts/Editor/DecorCatalogSheet.cs`: đọc và ghi workbook, Pull, Export, CSV.
- `Scripts/Editor/GiftSheet.cs`, `FishCatalogSheet.cs`, `SurfaceCatalogSheet.cs`: tab Gói quà, Cá, Thành hồ / Cát nền / Hồ.
- Test: `Editor/Tests/ShopEconomyTests.cs` kiểm tra dữ liệu thật (id trùng, món thiếu ảnh, gói quà); `FishShopInventoryTests.cs` kiểm tra save và migration; `DecorCatalogSheetTests.cs` kiểm tra workbook.
