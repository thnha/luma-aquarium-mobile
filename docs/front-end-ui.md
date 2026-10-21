# Lumaquarium — giao diện mở đầu

Luồng: **StartGame → LoadingGame → MainAquarium**. StartGame gộp màn bắt đầu với chơi khách; nút *Đăng nhập tài khoản* mở LoginGame, nơi chỉ còn form Sắp có và nút *Chơi với tư cách khách*. Xem [redesign](ui-redesign.md).

Mở `Assets/_Project/Scenes/StartGame.unity` rồi nhấn Play. StartGame cũng được đặt làm cảnh bắt đầu khi Play trong Editor, để vẫn giữ cảnh hồ cá đang mở. Build Settings cần bật cả bốn cảnh theo thứ tự trên.

## Asset và màn hình

- `Assets/_Project/UI/Icons`: 13 icon PNG nền trong suốt.
- `Assets/_Project/UI/Surfaces`: 20 bề mặt nút, panel, slot và thanh tiến trình.
- `Assets/_Project/UI/Backgrounds`: nền hồ sạch và lớp chuyển tối dưới chân màn.
- `Assets/_Project/UI/Fonts`: Nunito, hai font TMP hỗ trợ tiếng Việt và giấy phép OFL.
- `Assets/_Project/UI/Prefabs`: ba Canvas prefab có thể sửa trực tiếp.
- `Assets/_Project/UI/LumaquariumUI.spriteatlas`: atlas của icon và surface; nền lớn để riêng.

PNG được import thành Sprite Single, 100 PPU, Bilinear, Clamp, không mipmap. Surface có border nine slice theo `sprite-manifest.json`. Chữ là TextMeshPro riêng, không nằm trong ảnh. Canvas dùng chiều rộng tham chiếu 1080 và SafeArea cho điện thoại có tai thỏ.

Nút ngang dùng các biến thể `button_*_wide` (512×128, border 48), thay vì ảnh vuông của action tile. Cách này giữ góc tròn và gradient khi nút chỉ cao 114–130. Builder cũng tính `pixelsPerUnitMultiplier` đồng đều cho hai chiều nếu tổng viền vượt kích thước nút. Đã đo cả 7 nút trong ba prefab: không còn cặp viền vượt kích thước; preview Login và trạng thái lỗi Loading đã được chụp lại trong Simulator.

## Hành vi

Chơi khách tạo và giữ một mã hồ sơ cục bộ tại PlayerPrefs `Lumaquarium.Profile.GuestId`. Đây chưa phải tài khoản máy chủ. Chưa có đồng bộ hoặc khôi phục tiến trình qua tài khoản.

Form tài khoản được chuẩn bị sẵn, có nhãn **Sắp có**; các ô và nút gửi đang khóa. Không gửi thông tin đăng nhập và không lưu mật khẩu. Quay về chơi khách sẽ xóa nội dung mật khẩu trong bộ nhớ.

Loading hiển thị tiến trình `LoadSceneAsync` của MainAquarium; giữ màn tối thiểu 1,2 giây để dễ đọc. Có thông báo lỗi, Thử lại và Quay lại nếu cảnh đích chưa sẵn sàng. Nút chuyển cảnh có chặn nhấn liên tiếp.

## Tạo lại màn hình

Menu Unity:

1. `Lumaquarium > UI > Prepare Sprite Assets`.
2. `Lumaquarium > UI > Build Front End Screens`.

Bước 2 tạo lại ba scene và prefab mở đầu, vì vậy cần lưu hoặc giữ bản sao các chỉnh sửa tùy biến ở những màn này trước khi dùng. Builder tạo scene theo kiểu additive và giữ nguyên cảnh MainAquarium đang mở, kể cả khi cảnh đó có thay đổi chưa lưu.

Nếu chạy lại builder hồ cá cũ, kiểm tra Build Settings: builder đó có thể đặt danh sách về chỉ còn MainAquarium. Chạy lại builder UI để thêm ba cảnh mở đầu.

Nguồn artwork đã duyệt: `../medias/ui/v1` và `../medias/concepts/lumaquarium-hud-approved-v1.png` tính từ thư mục mobile. Nền sạch được tạo từ bản đã duyệt, giữ góc nhìn dưới nước và bỏ HUD.
