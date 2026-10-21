# MainAquarium

Màn `Assets/_Project/Scenes/MainAquarium.unity` dùng HUD theo mẫu `../medias/concepts/lumaquarium-hud-approved-v1.png`, sprite và font Nunito đã có. Cá là prefab Betta 3D hiện tại, giữ rig, animation, chuyển động, phản ứng chạm và hệ thức ăn.

## Phần đã dựng

- Bảng tên và chân dung được render từ chính model Betta; nút Cài đặt, Ẩn UI và bốn hành động tiếng Việt.
- Canvas tham chiếu 1080 × 1920, co theo chiều rộng, toolbar dùng HorizontalLayoutGroup và SafeArea hiện có.
- Cho ăn gọi FeedingSystem, giữ cooldown và giới hạn viên thức ăn. Panel Chăm sóc đọc độ no và trạng thái thật từ BettaFishAI.
- Ẩn UI giữ nút hiện lại. Cài đặt cho bật/tắt âm thanh và đưa camera về góc ban đầu. Camera bị khóa khi mở panel.
- Ảnh được chụp sau khi ẩn HUD, lưu PNG vào `Application.persistentDataPath/Photos`, rồi hiện lại HUD. Thông báo thành công chỉ xuất hiện sau khi ghi file; chưa xuất vào thư viện ảnh điện thoại hoặc chia sẻ.

## Phạm vi hình ảnh

Nền `Assets/_Project/UI/Backgrounds/AquariumEnvironment.png` là ảnh hồ sạch được tạo bằng built-in imagegen từ ảnh mở đầu, bỏ cá vẽ sẵn. Nền nằm trên Canvas camera ở sau cá 3D. Đây là cách dựng **2.5D**: cây, đá, lũa và ánh sáng trong nền chưa phải vật thể 3D có va chạm. Camera giữ khoảng xoay nhỏ phù hợp với nền ảnh.

Geometry blockout cũ được giữ trong scene và tắt Renderer, còn SwimBounds và FoodSpawnPoint tiếp tục dùng cho gameplay. Vùng bơi nông hơn và cá lớn hơn để thấy rõ trên màn dọc. Không thay prefab hoặc script AI của cá.

Panel Trang trí giới thiệu ba nhóm Cây, Đá, Lũa và ghi rõ chưa hỗ trợ đặt đồ 3D. Panel Chăm sóc ghi rõ chưa có dữ liệu nước và thiết bị; không hiển thị thông số minh họa như dữ liệu thật.

## File chính và tạo lại

- `Assets/_Project/Scripts/UI/MainAquariumScreen.cs`: điều khiển HUD và chụp ảnh.
- `Assets/_Project/Scripts/Editor/MainAquariumScreenBuilder.cs`: dựng HUD lên scene hiện tại, tạo chân dung từ model và cấu hình trình bày.
- `Assets/_Project/Scripts/SafeArea.cs`: xử lý cả trường hợp Device Simulator trả safe area cũ nằm ngoài viewport sau khi đổi độ phân giải.
- `Assets/_Project/UI/Portraits/BettaPortrait.png`: chân dung model.
- `docs/previews/MainAquarium/`: ảnh HUD và panel kiểm tra trong Editor.

Mở MainAquarium, thoát Play Mode rồi dùng `Lumaquarium > UI > Build Main Aquarium Screen`. Builder lưu riêng bản scene trên đĩa và bản đang chỉnh trong `Assets/_Project/Scenes/Backups/` trước khi tạo lại HUD; không thay Build Settings hoặc dựng lại cá. Các tùy chỉnh riêng trong HUD do builder tạo sẽ bị thay thế khi chạy lại. HUD starter được giữ dưới `LegacyAquariumHUD` và tắt.

Builder hồ cá starter tạo lại toàn bộ MainAquarium nên lệnh `Build Main Aquarium Scene` đã được gỡ khỏi menu. Cập nhật cá bằng `Lumaquarium > Build Betta Prefab` sau khi thoát Play Mode.

## Kiểm tra đã thực hiện

- Unity 6000.3.26f1 biên dịch hai script mới; builder chạy lại vẫn chỉ có một cá và một MainAquariumScreen.
- Play Mode: frame counter tiến, cá bơi; nút Cho ăn tạo viên thức ăn, cá chuyển SeekFood rồi độ đói giảm sau khi ăn. Gọi liên tiếp 21 lần vẫn chỉ tạo một viên trong cooldown.
- Panel Chăm sóc hiển thị độ no/trạng thái và khóa camera; Ẩn/Hiện UI hoạt động; bật/tắt âm thanh khôi phục giá trị ban đầu trong phép kiểm tra.
- Chụp được PNG không có HUD và xác nhận HUD/camera được khôi phục.
- Đã xem ảnh HUD và panel ở Game View 1170 × 2532 và 320 × 568 (9:16). SafeArea nằm trong 0–1, raycast chạm trúng nút Chăm sóc sau khi đổi kích thước. Chưa kiểm tra thao tác cảm ứng và hiệu năng trên thiết bị thật.

Kiểm tra thủ công: Play từ luồng StartGame → LoginGame → LoadingGame → MainAquarium, mở từng nút, cho cá ăn, chụp ảnh, thử ẩn/hiện, xoay/zoom và đổi tỷ lệ Game View sang 9:16 rồi màn hình dài. Kiểm tra tiếp trên Android/iPhone trước khi nghiệm thu mobile.
