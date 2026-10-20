# Preview đặt đồ 3D và hiệu ứng nước

Scene: `Assets/_Project/Scenes/Aquarium3DPreview.unity`.

Mở Unity rồi chọn **Lumaquarium → Preview → Play 3D Preview**. Công cụ mở scene preview riêng, cho phép thử trước khi tích hợp vào luồng chơi chính. Khi thoát Play Mode, công cụ khôi phục scene khởi đầu trước đó. Build Settings và MainAquarium được giữ nguyên.

## Cách thử

1. Chọn **Trang trí**, sau đó chọn **Cây**, **Đá** hoặc **Lũa**.
2. Camera chuyển góc nhìn xuống nền cát. Kéo trên cát để di chuyển món đang chọn. Vòng xanh cho phép đặt; vòng đỏ báo ra ngoài hồ hoặc chồng chân đế của món khác.
3. Chọn **Xoay 30°**, rồi **Đặt** để xác nhận. **Hủy** bỏ món mới hoặc khôi phục vị trí/góc xoay của món đang sửa.
4. Chọn **Sửa đồ trong hồ**, chạm món hiện có, rồi kéo, xoay hoặc **Xóa**.
5. Bố cục chỉ được ghi sau xác nhận/xóa, lưu cục bộ bằng khóa PlayerPrefs `Lumaquarium.Decoration.Preview.v1`. Chạy lại preview để kiểm tra nạp bố cục. Giới hạn 24 món.
6. Chạm vùng nước để tạo gợn trên mặt nước. Khi cá tới gần mặt nước cũng phát gợn; bọt khí nổi lên và cây đung đưa liên tục. Cho ăn, chăm sóc và chụp ảnh dùng HUD hiện có.

## Phạm vi hình ảnh

Đây là bản tương tác với model dựng bằng mesh cơ bản: cây có thân/lá cong, đá có bề mặt biến dạng và lũa phân nhánh. Các món mới và nền cát là geometry 3D; nền ảnh xa vẫn giữ mẫu đã duyệt. Chưa phải toàn bộ môi trường 3D hoàn chỉnh hoặc bộ asset mỹ thuật cuối cùng.

Nước dùng shader gợn động với tối đa bốn xung tương tác, vệt sáng chuyển động trên bề mặt và một hệ hạt bọt khí giới hạn 70 hạt. Đây là mô phỏng thị giác nhẹ; chưa gồm mô phỏng chất lỏng vật lý, khúc xạ theo độ sâu, hóa học nước hoặc điều hướng cá tránh đồ trang trí. Kiểm tra đặt đồ dựa trên chân đế tròn, nên một số lá có thể giao nhau ở phía trên.

## Mã chính

- `AquariumDecoration.cs`: chọn/đặt/xoay/xóa, xác nhận/hủy, kiểm tra biên và chồng đồ, lưu/nạp bố cục.
- `AquariumDecorItem.cs`: loại món, kích thước chân đế và chuyển động cây.
- `AquariumWaterEffects.cs`: nhận tương tác và cấp xung gợn cho shader, theo dõi cá gần mặt nước.
- `Editor/Aquarium3DPreviewBuilder.cs`: tạo scene preview từ MainAquarium, mesh/prefab/material và bổ sung nút vào HUD hiện có.
- `UI/MainAquariumScreen.cs`: hủy thao tác đặt đang mở khi đổi panel, cho ăn, ẩn HUD hoặc chụp ảnh.
- `Shaders/AquariumWater.shader`, `AquariumNature.shader`, `AquariumBubble.shader`: mặt nước, vật liệu đồ/nền và bọt khí.
- `Art/Aquarium3D/`: mesh, ba prefab và vật liệu đã lưu để mở lại dự án.

Tạo lại preview: mở MainAquarium ngoài Play Mode rồi chọn **Lumaquarium → Preview → Build 3D Decoration and Water**. Preview cũ được sao lưu trong Scenes/Backups. Builder không sửa scene MainAquarium hoặc Build Settings. Các tùy chỉnh thủ công trong scene preview sẽ được thay bằng bản mới.

## Kiểm tra

Đã chạy 25 kiểm tra trực tiếp trong Unity Play Mode: loại đồ không hợp lệ, vị trí ngoài biên, chồng đồ, tọa độ NaN, đặt ở nền trống, xoay, xác nhận, hủy, xóa, lưu, nạp lại, dữ liệu lỗi, giới hạn số lượng, gợn nước, hạt bọt khí và biên dịch ba shader. Đã xác nhận frame counter tiến.

Chạy lại khi preview đang Play:

```sh
unity command eval_file --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --file /Users/nha-tran/Data/projects/lumaquarium/mobile/docs/tests/decoration-playmode.cs --format json
```

Kiểm tra thủ công thêm trên Android/iPhone: thao tác một/hai ngón, đổi hướng ứng dụng, thoát/mở lại, ảnh chụp và hiệu năng sau đặt nhiều món. Chưa build hoặc đo FPS trên thiết bị thật.
