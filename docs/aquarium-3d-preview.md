# Preview đặt đồ 3D và hiệu ứng nước

Scene: `Assets/_Project/Scenes/Aquarium3DPreview.unity`.

## Nâng hình ảnh v2

- Cây mới là cụm 19 lá Anubias với cuống uốn riêng, lá cong có UV và texture gân lá, ánh sáng hai mặt và bóng đổ cutout. Shader làm phần ngọn lá lay nhẹ; các cụm cây lệch pha theo vị trí. Chân đế đặt đồ vẫn là 0,14 m như bản trước.
- Nước chuyển sang bốn hướng sóng có chu kỳ khác nhau, tính pháp tuyến và Fresnel để trộn phản chiếu nhẹ từ ảnh môi trường. Gợn do chạm/cá vẫn dùng tối đa bốn xung. Đây là phản chiếu giả lập từ texture, chưa phải phản chiếu hoặc khúc xạ toàn bộ scene theo độ sâu.
- Bọt dùng bề mặt cầu tính từ billboard: tâm trong, viền tối/sáng và chấm phản chiếu nhỏ. Kích thước, tuổi thọ và tốc độ nổi khác nhau; thêm dao động nhẹ và tăng kích thước khi nổi. Giới hạn 55 hạt.
- Giữ ảnh trước ở `BeforeNatural.png` và chuyển động cũ ở `water-preview-v1.gif`. Trang preview có thanh so sánh trước/sau; `AfterNatural.png` và `water-preview.gif` ghi lại bản mới từ Unity.

Mã và asset bổ sung: `Editor/AquariumNaturalVisualsBuilder.cs`, `Shaders/AquariumLeaf.shader`, `Art/Aquarium3D/AquaticPlantNatural.prefab`, các mesh/material có hậu tố `v2`, và `Art/Aquarium3D/Textures/AnubiasLeaf-v2.png`. Đã chỉnh `AquariumWater.shader`, `AquariumBubble.shader`, chuyển động trong `AquariumDecorItem.cs` và cho builder preview áp dụng hình ảnh mới khi tạo lại scene. Các thao tác đặt/xóa/lưu bố cục giữ nguyên.

Texture lá được tạo bằng built-in ImageGen, alpha trong suốt; ảnh gốc được giữ và bản sử dụng đã chép vào dự án. Prompt đầy đủ và đường dẫn gốc nằm trong `Art/Aquarium3D/Textures/AnubiasLeaf-v2.generation.json`.

Đã biên dịch không lỗi/cảnh báo, render trong Play Mode và chạy lại **26 kiểm tra đạt** (thêm shader lá). Đã xác nhận bọt hoạt động với 25 hạt ở thời điểm chụp, và ghi 60 khung hình chuyển động cho bản mới. Chưa đo hiệu năng trên Android/iPhone.

Muốn áp dụng lại trên preview hiện có: thoát Play rồi chọn **Lumaquarium → Preview → Upgrade Natural Plants Water and Bubbles**. Công cụ sao lưu scene, thay model cây nhưng giữ vị trí/góc xoay/tỷ lệ, rồi cập nhật vật liệu và hệ hạt. MainAquarium và Build Settings không bị chỉnh bởi công cụ này.

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

Nước dùng shader gợn động với tối đa bốn xung tương tác, vệt sáng chuyển động trên bề mặt và một hệ hạt bọt khí giới hạn 55 hạt. Đây là mô phỏng thị giác nhẹ; chưa gồm mô phỏng chất lỏng vật lý, khúc xạ theo độ sâu, hóa học nước hoặc điều hướng cá tránh đồ trang trí. Kiểm tra đặt đồ dựa trên chân đế tròn, nên một số lá có thể giao nhau ở phía trên.

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

Kiểm tra tiếp bằng thao tác thật trong Device Simulator: mở Trang trí, chọn Cây, chạm giữ trên nền cát để đưa cây tới vòng xanh, xoay 30° rồi xác nhận. Số món tăng từ 5 lên 6; sau thoát và mở lại Play, cây vẫn ở đúng vị trí và góc xoay. Đã xóa đúng món thử để trả bố cục về 5 món và chạy lại đủ 25 kiểm tra, đều đạt. Một phiên chạy liên tục hơn 117 giây giữ đúng scene preview. Menu Play Preview khôi phục StartGame khi thoát Play.

Gián đoạn lúc ghi preview trước đó có dấu vết điều khiển Editor đồng thời: `Library/Pipeline/eval-usage.jsonl` ghi các lời gọi `EditorSceneManager.LoadSceneInPlayMode` ngoài các lệnh của lượt triển khai này; một lời gọi xuất hiện khoảng 5 giây sau khi bắt đầu ghi hình. Các script cá cũng được cập nhật đồng thời. Nguồn phiên điều khiển còn lại chưa được định danh. Khi kiểm tra lại trong khoảng Editor ổn định, thao tác đặt/lưu/nạp và phiên chạy dài ở trên đều hoàn tất. Không sửa các script cá đang được công việc khác cập nhật.

Ảnh thao tác thật: `docs/previews/Aquarium3D/Placement.png`. Preview động và thông tin số khung hình: cùng thư mục, file `water-preview.gif` và `capture.json`.

Chạy lại khi preview đang Play:

```sh
unity command eval_file --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --file /Users/nha-tran/Data/projects/lumaquarium/mobile/docs/tests/decoration-playmode.cs --format json
```

Kiểm tra thủ công thêm trên Android/iPhone: thao tác một/hai ngón, đổi hướng ứng dụng, thoát/mở lại, ảnh chụp và hiệu năng sau đặt nhiều món. Chưa build hoặc đo FPS trên thiết bị thật.
