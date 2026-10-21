# Lumaquarium — redesign giao diện

Bản xem trước tương tác (sơ đồ luồng + 12 màn): https://claude.ai/artifact/13fS4ioUHtvvvDrN3bYrkj

Bản này dùng lại artwork, icon, font Nunito và bảng màu đã duyệt trong `Assets/_Project/UI/sprite-manifest.json` (ink `#343C2D`, cream `#F1E6C8`, sage `#CFDDBB`, forest `#455D3F`). Thiết kế đã được duyệt và đưa vào code Unity, xem phần **Triển khai** bên dưới.

## Vấn đề ở bản hiện tại

- Hai cửa hàng khác nhau hoàn toàn: Tiệm cá có nền hồ, nút ← tròn và chip xu; Vườn thủy sinh có nền xanh phẳng, nút chữ "‹ Về hồ" và số xu dạng chữ.
- HUD hồ không hiện ví xu. Lối vào cửa hàng là hai nút chữ "Mua cá" và "Cây · Lũa · Đá" xếp dưới Cài đặt / Ẩn UI. Nút "Ban ngày" nằm giữa đỉnh màn.
- Có 3 đường vào shop trang trí (nút góc phải, panel Trang trí, ô Cây/Đá/Lũa). Ô trong panel Trang trí mở shop thay vì mở kho.
- Nhóm hàng không thống nhất: panel Trang trí dùng Cây / Đá / Lũa, shop dùng Cây thủy sinh / Lũa / Đá · Non bộ.
- Start chỉ có một nút "Bắt đầu" rồi sang Login để chọn chơi khách, nên người chơi phải bấm hai lần.
- Thanh đặt đồ dùng 4 nút chữ cùng kiểu, nút xác nhận "Đặt" không nổi bật.

## Quy tắc chung mới

| Thành phần | Quy tắc |
|---|---|
| Thanh trên | Trái: chip cá (chân dung, tên, thanh độ no) hoặc nút ← tròn 44 px. Phải: chip xu và nút Cài đặt. |
| Ví xu | Luôn ở góc phải trên, trong hồ và trong cửa hàng, cùng một kiểu chip. |
| Dock | 5 nút chỉ có icon, không chữ: Cho ăn · Chăm sóc · Trang trí · Cửa hàng · Ảnh. Mỗi icon đứng trên một tảng đá rêu (`ui3d_rock`), không có khay. Nút đang mở được nhấc lên trên quầng sáng sage (`ui3d_glow`). |
| Sheet | Nền cream, bo 26 px, nổi trên dock, tiêu đề 22 px với nút ✕ ở bên phải. Chạm ✕ hoặc chạm lại nút dock thì đóng. |
| Nút | Chính: forest, chữ cream, cao 52–54 px. Phụ: cream đậm có viền. Nút khóa: xám be. Mỗi vùng chỉ có một nút chính. |
| Khối 2.5D | Mọi chip, nút, khay dock, sheet và hộp thoại: viền xanh rêu `#4A7A3A` dày 2–3 px, mép dưới đặc 2–3.5 px cùng màu, vệt sáng trắng 2 px ở mép trên và nền chuyển màu nhẹ. Nút chính chuyển `#5E8F4A` → `#45703A`; nút phụ nền kem, viền và mép dưới `#BFA978`. Khi nhấn, nút lún 2 px. |
| Nhóm hàng | Cửa hàng: Cá · Cây · Lũa · Đá. Trong kho Trang trí, nhóm Cây tách ra thành Cây / Rêu / Bèo. |

## Luồng mới

1. **Khởi động** (gộp Start và Login): *Chơi ngay* → Đang tải. *Đăng nhập tài khoản* mở sheet có nhãn Sắp có. Người chơi quay lại thấy nút *Tiếp tục hồ của bạn*.
2. **Đang tải** → Hồ cá. Khi lỗi: *Thử lại* / *Quay lại*, nằm trong cùng một thẻ.
3. **Hồ cá** là màn trung tâm. Từ dock mở Chăm sóc, Trang trí (kho), Cửa hàng; nút Ảnh chụp rồi hiện thông báo. Chip cá mở Chăm sóc, chip xu mở Cửa hàng.
4. **Trang trí** chỉ quản lý kho. Chọn món còn trong kho → Đặt đồ. Món đã hết → Cửa hàng. Có thêm *Sắp xếp lại* và *Mua thêm*.
5. **Cửa hàng chung** có tab Cá / Cây / Lũa / Đá, khung trên xem trước món đang chọn, thanh dưới cố định hiện giá và nút chính. Mua → hộp Xác nhận → thông báo thành công (*Về hồ ngắm cá* / *Xem tiếp*).
6. **Đặt đồ**: chip món đang đặt và số còn trong kho, gợi ý chuyển đỏ khi vị trí không hợp lệ, công cụ Xoay / Cất / Hủy và nút chính *Đặt* (bị khóa khi vị trí không hợp lệ).
7. **Cài đặt**: Âm thanh, Ánh sáng ngày/đêm (chuyển từ nút giữa màn vào đây), Góc nhìn, Ẩn giao diện, Hồ sơ khách.

## Triển khai

Code đã sửa xong. Scene và prefab là do builder sinh ra, nên cần mở Unity 6000.3.26f1 và chạy builder theo đúng thứ tự dưới đây. Mỗi builder đều lưu bản sao scene vào `Assets/_Project/Scenes/Backups/` trước khi sửa.

1. `Lumaquarium > UI > Prepare Sprite Assets`: import 24 surface `ui3d_*` mới với viền 9-slice lấy từ manifest.
2. `Lumaquarium > UI > Build Front End Screens`: dựng lại StartGame, LoginGame và LoadingGame.
3. Mở `MainAquarium` rồi chạy `Lumaquarium > UI > Build Main Aquarium Screen`. Builder chỉ thay `MainAquariumHUD`, dựng lại dock đặt đồ, rồi nối lại `AquariumDecoration`, nút ngày/đêm của `AquariumLighting` và hai shop. Hồ 3D, backdrop đêm và camera được giữ nguyên.
4. `Lumaquarium > UI > Build Fish Shop Screen`: prefab có `layoutVersion < 2` sẽ được dựng lại theo khung cửa hàng chung.
5. `Lumaquarium > UI > Build Decoration Shop`: tương tự cho shop trang trí, rồi nối tab Cá ↔ Cây/Lũa/Đá và bảng Trang trí.

Sprite nguồn được sinh lại bằng `python3 Tools/UI/make_ui3d_sprites.py`. Nếu đổi kích thước hoặc viền, cập nhật `borderLTRB` trong manifest theo số liệu script in ra.

### Thay đổi chính trong code

| Khu vực | File |
|---|---|
| Widget 2.5D và khung cửa hàng dùng chung | `Scripts/Editor/Ui3D.cs`, `Scripts/UI/Ui3DButtonLabel.cs`, `Tools/UI/make_ui3d_sprites.py` |
| Khởi động gộp với chơi khách; Login chỉ còn form Sắp có; Back ở Loading về Start | `FrontEndScreenBuilder.cs`, `StartGameScreen.cs`, `LoginGameScreen.cs`, `GameLoadingScreen.cs`, `FrontEndScenes.cs` |
| HUD: chip cá + độ no, ví xu, dock 5 nút, sheet Chăm sóc / Cài đặt / Trang trí, dock đặt đồ | `MainAquariumScreenBuilder.cs`, `MainAquariumScreen.cs`, `DecorInventoryPanel.cs` |
| Cửa hàng chung có tab | `FishShopScreenBuilder.cs`, `DecorShopScreenBuilder.cs`, `FishShopScreen.cs`, `DecorShopScreen.cs` |
| Không đụng HUD mới khi chạy lại builder cũ | `AquariumHardscapeBuilder.cs` (tìm `DayNightButton` trong Cài đặt), `Aquarium3DPreviewBuilder.cs` (bỏ qua khi đã có `DecorInventoryPanel`) |
| Test | `docs/tests/main-aquarium-playmode.cs`, `fish-shop-scene.cs`, `fish-shop-playmode.cs` |

Logic ví, kho và đặt đồ (`FishShopInventory`, `AquariumDecoration`) giữ nguyên. Các field mới đều tùy chọn, nên scene dựng trước redesign vẫn chạy được.

### Khác với preview

- Kho Trang trí chia 3 tab Cây · Lũa · Đá, khớp với `AquariumDecorCategory` và tab cửa hàng. Preview tách riêng Rêu và Bèo, nhưng dữ liệu catalog hiện chưa có nhóm đó.
- Ánh sáng ngày/đêm trong Cài đặt là một nút chuyển, để `AquariumLighting.label` và test cũ vẫn dùng được.
- Icon dock Cửa hàng `action_shop.png` là căn nhà shop mái xanh, mái hiên sọc và biển xu, vẽ bằng `Tools/UI/make_shop_icon.py` (dùng ảnh render dương xỉ Java trong game). Tảng đá và quầng sáng dưới icon dock vẽ bằng `Tools/UI/make_dock_rock.py`.

### Chỉnh HUD và cửa hàng (layout 4)

- Thanh độ no (chip cá và panel Chăm sóc) dùng `ui3d_fill` dạng 9-slice và đổi chiều rộng theo %, thay cho Image Filled. Filled kéo giãn cả sprite bo tròn nên hai đầu thanh bị bóp nhọn.
- Nút Cài đặt và Ẩn UI lớn hơn (122 → 150 px, icon khoảng 100 px) và có viền tối quanh icon.
- Thẻ cửa hàng cao hơn (340 → 420 px), ảnh chiếm nửa trên, tên 46 px. Giá nằm trong viên gọn ở góc phải dưới: viên vàng `ui3d_price` chỉ có icon xu và số, không ghi chữ "xu". Viên xanh `ui3d_price_free` dùng cho Miễn phí, Đã có và Trong hồ. Thanh dưới hiện icon xu và số 72 px. Khung chọn được nâng lên để thấy một hàng thẻ và một phần hàng sau.
- Nút *Đăng nhập tài khoản* ở màn Khởi động dùng nền khóa `ui3d_button_disabled`, không bấm được cho tới khi có tài khoản.
- `FishShopScreenBuilder.LayoutVersion` = 4, nên Build Fish Shop Screen và Build Decoration Shop dựng lại prefab cũ. Cảnh cũ vẫn chạy: field mới đều tùy chọn và thanh no Filled vẫn được hỗ trợ.

Thứ tự dựng lại: chạy `python3 Tools/UI/make_ui3d_sprites.py`, `python3 Tools/UI/make_dock_rock.py` và `python3 Tools/UI/make_shop_icon.py` (ảnh đã có sẵn trong repo, chỉ cần chạy khi sửa script), rồi Prepare Sprite Assets → Build Front End Screens → Build Main Aquarium Screen → Build Fish Shop Screen → Build Decoration Shop.

### Đã dựng và kiểm tra (21/10/2026)

Đã chạy cả 5 bước trên trong Unity 6000.3.26f1, biên dịch không lỗi. Build Settings: StartGame → LoginGame → LoadingGame → MainAquarium. `FrontEndScreenBuilder` đặt StartGame làm cảnh bắt đầu khi bấm Play trong Editor, mỗi lần nạp lại domain.

Play Mode ở Game View 1080 × 1920: Khởi động hiện *Tiếp tục hồ của bạn* (đã có hồ sơ khách) → Đang tải → Hồ cá. Đã mở Chăm sóc, Trang trí, Cài đặt và Cửa hàng (tab Cá, 8 cá), console không có lỗi. Chưa thử mua cá, đặt đồ, màn lỗi của Loading, màn dài và thiết bị thật.

Các builder vẫn ghi bản sao scene vào `Assets/_Project/Scenes/Backups/`. Thư mục này đã được dọn và đưa vào `.gitignore`; có thể xóa bất cứ lúc nào.
