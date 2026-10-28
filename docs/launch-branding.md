# Splash, StartGame và LoadingGame

Tên hiển thị: **Betta Fish Aquarium 3D**.

Hai màn dùng chung `Assets/_Project/UI/Backgrounds/AquariumLaunch.png`: tank thủy sinh, colorful betta, tên game hai dòng và tagline. Tên và tagline nằm trong artwork; nút và ghi chú lưu là TextMeshPro/UI thật. Không dùng ảnh mockup hai màn làm giao diện.

Splash Unity bật, tắt logo Unity, animation, lớp phủ và blur. Background portrait/landscape cùng trỏ vào artwork. iOS dùng launch image riêng cho iPhone portrait, landscape và iPad; Android dùng launch image với ScaleToFill. Android phiên bản có splash hệ thống có thể hiện icon app trước splash Unity.

Khi xuất iOS, `LaunchBrandingBuilder` chỉnh storyboard iPhone/iPad để ảnh dùng `scaleAspectFill` và bám cả bốn cạnh root view, phủ cả vùng tai thỏ và home indicator. Template mặc định của Unity dùng `scaleAspectFit`, gây hai dải đen trên/dưới trên iPhone màn dài. Ảnh giữ đúng tỉ lệ và được cắt ở cạnh khi cần, giống background StartGame. Cần xuất lại Xcode project để thay đổi có trong bản cài mới.

StartGame giữ luồng guest hiện có: Chơi ngay hoặc Tiếp tục hồ của bạn → LoadingGame. Các nút hiện dần trong 0,45 giây bằng thời gian thực; khóa nhận chạm trong lúc fade. Nút và ghi chú nằm trong SafeArea. Cảnh hồ và tên giữ nguyên khi chuyển từ splash sang StartGame. Không còn card đăng nhập trên màn mở đầu; màn LoginGame vẫn được giữ trong project.

Artwork đã được chỉnh bằng ImageGen để cá giảm chói, giảm bão hòa và nhận sắc xanh/ánh sáng dưới nước, giữ bố cục và chữ. Ảnh gốc được giữ tại `../backups/launch-blend-20261027/AquariumLaunch-original.png`. Lớp phủ `ControlsScrim` của Start/Loading phủ toàn Canvas; chỉ nút và chữ theo SafeArea, tránh vệt ngang tại vùng home indicator. Preview `StartGame-safe-area.png` và `LoadingGame-safe-area.png` mô phỏng inset trên/dưới.

## Tạo lại và kiểm tra

- `Lumaquarium > UI > Apply Splash and Start Branding`: áp dụng cấu hình cả hai nền tảng, tạo lại riêng StartGame và render preview. Dừng Play Mode và lưu thay đổi StartGame trước khi chạy.
- `Build Front End Screens` tạo StartGame và LoadingGame với cùng artwork `AquariumLaunch.png`; Loading giữ thanh tiến trình và nút thử lại/quay lại, không vẽ thêm tiêu đề lên tên game trong ảnh. Màn Login giữ layout riêng.
- `Verify Splash and Start Branding`: kiểm tra đường dẫn ảnh, cấu hình native, scene đầu và tham chiếu UI. Bước này cũng chạy trước build iOS/Android.
- `Test Branded Start in Play Mode`: kiểm tra frame chạy, fade hoàn tất, EventSystem, raycast nút, label tương ứng hồ hiện có và LoadingGame trong build. Không tạo profile hoặc tải remote content.
- `Capture Splash and Start Previews`: render ảnh trong `docs/previews/LaunchBranding/` cho màn dài, 9:16, iPad, nút Tiếp tục và Loading (gồm trạng thái lỗi).

Đã kiểm tra trong Unity 6000.3.26f1. Chưa build/cài phiên bản mới lên thiết bị iOS hoặc Android; cold launch và splash hệ thống cần được xác nhận trên thiết bị thật.
