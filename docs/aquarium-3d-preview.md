# Preview đặt đồ 3D và hiệu ứng nước

Scene: `Assets/_Project/Scenes/Aquarium3DPreview.unity`.

## Đã áp dụng asset vào hồ chính

Bộ đã duyệt được lưu thành asset dùng chung `Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab` và áp vào `Assets/_Project/Scenes/MainAquarium.unity`. Asset chứa cây, đá, lũa, cát, sỏi, nước, bọt, nền ngày/đêm và HUD tương tác. Scene chính giữ các đối tượng cá, rig, feeding manager, camera và EventSystem hiện có; camera và tỷ lệ trình bày lấy theo bản đã duyệt. Geometry/HUD cũ được giữ trong scene và ẩn đi.

Công cụ `Scripts/Editor/AquariumAssetInstaller.cs`, menu **Lumaquarium → Assets → Apply Approved Aquarium to Main**, sao lưu scene đã lưu và trạng thái đang làm việc trước khi áp dụng. Khi cập nhật lại, công cụ nhận diện instance bằng đường dẫn prefab để tránh tạo môi trường thứ hai. Các tham chiếu cá, camera và đèn được gắn tại scene, không lưu tham chiếu chéo từ scene preview vào prefab. Khóa bố cục game là `Lumaquarium.Decoration.Main.v1`, tách khỏi `Lumaquarium.Decoration.Preview.v1`.

MainAquarium hiện là scene đang mở và scene Play của Editor. Build Settings đang có MainAquarium làm scene game; cấu hình build không thay đổi trong lượt cập nhật này. Chưa tạo bản APK/iOS hoặc kiểm tra trên thiết bị thật.

Kiểm tra tích hợp: **11 kiểm tra Edit Mode**, **27 kiểm tra đặt đồ/shader**, **12 kiểm tra ánh sáng** và **6 kiểm tra callback HUD/cho ăn** đều đạt (**56 tổng cộng**). Đã mở lại MainAquarium từ ổ đĩa, xác nhận frame counter tiến, thử áp lại không tạo môi trường trùng, và kiểm tra trực tiếp trong Device Simulator. Có một lỗi TMP_SubMeshUI OnValidate trong quá trình thao tác asset ở Edit Mode; không lặp lại trong phiên game sau khi nạp lại. Một số ảnh từ encoder xuất tức thời thiếu đá trong khi Game View vẫn hiện đủ; ảnh xác nhận cuối dùng ScreenCapture cuối khung hình. Ảnh hồ chính: `docs/previews/Aquarium3D/MainAquariumUpdated.png`. Khi chụp ScreenCapture, Metal báo hai cảnh báo memoryless depth load/store; ảnh cuối và Game View vẫn hiện đủ các vật thể.

Kiểm tra đã lưu ở `docs/tests/approved-assets-editmode.cs` (ngoài Play), `main-aquarium-playmode.cs`, `decoration-playmode.cs` và `lighting-playmode.cs` (khi MainAquarium đang Play). Ví dụ:

```sh
unity command eval_file --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --format json -- --file /Users/nha-tran/Data/projects/lumaquarium/mobile/docs/tests/approved-assets-editmode.cs
```

## Đá, cát, sỏi, lũa và ánh sáng v3

- Đá dùng mesh mòn mượt và texture vân khoáng; lũa dùng 5 nhánh cong có UV chạy theo chiều thân, bề mặt thớ gỗ và hai đầu khép kín. Giữ chân đế đá 0,19 m, lũa 0,24 m và vị trí/góc xoay/tỷ lệ các món đã đặt.
- Cát dùng texture hạt mịn; 190 viên sỏi nhỏ gộp thành một mesh nền, không thêm collider hoặc chặn thao tác đặt đồ. Sỏi là lớp nền trang trí, chưa có mục kho đồ riêng.
- Nút **Ban ngày / Ban đêm** ở đầu HUD chuyển ánh sáng trong 1,5 giây. Đèn, ambient, nền xa và texture phản chiếu nước/bọt đổi cùng nhau. Ban đêm có ảnh môi trường riêng để loại các tia nắng vàng đã được vẽ vào ảnh ngày. Đây là chuyển chế độ thủ công, chưa chạy theo giờ thực.
- `AquariumLighting.cs` điều khiển chuyển cảnh và khôi phục trạng thái ánh sáng/global shader khi bị tắt hoặc thoát Play. `Editor/AquariumHardscapeBuilder.cs` tạo và áp dụng asset, `Shaders/AquariumHardscape.shader` xử lý texture triplanar, thớ gỗ theo UV, bóng đổ và vệt sáng dưới nước. Các shader lá/thân/nước/bọt đã nhận trạng thái ánh sáng chung. Builder preview đầy đủ tự áp dụng v3 khi asset có sẵn.
- Asset mới trong `Art/Aquarium3D/`: prefab đá/lũa có hậu tố Natural, mesh/material `v3`, ba texture `Textures/RiverStone-v3.png`, `RiverSand-v3.png`, `Driftwood-v3.png`. Nền đêm: `UI/Backgrounds/AquariumEnvironmentNight-v3.png`. Bốn ảnh được tạo bằng built-in ImageGen; prompt đầy đủ và ảnh gốc lưu trong `Art/Aquarium3D/Textures/Hardscape-v3.generation.json`.

Áp dụng lại: thoát Play rồi chọn **Lumaquarium → Preview → Upgrade Hardscape and Day Night**. Công cụ tạo bản sao lưu scene trước khi sửa. Sau khi chạy riêng công cụ nâng cây v2, có thể chạy lại công cụ này để bảo đảm toàn bộ v3 được áp dụng.

Đã biên dịch không lỗi/cảnh báo; **27 kiểm tra đặt đồ/shader + 12 kiểm tra ánh sáng = 39 đạt**. Đã chạm nút ngày/đêm thật trong Device Simulator và xác nhận trạng thái đổi. Console không có lỗi runtime. Đã ghi **90 khung hình thật**, frame counter 2960 → 3282 trong 10,95 giây; chuyển ngày → đêm → ngày. Ảnh `Day.png`, `Night.png`, `Hardscape.png`, ảnh động `day-night-preview.gif` và `lighting-capture.json` nằm trong `docs/previews/Aquarium3D/`. Trang preview cho chọn ảnh ngày/đêm, so sánh bằng thanh kéo và xem chuyển cảnh. Chưa build hoặc đo hiệu năng trên điện thoại thật; nền xa vẫn là ảnh.

Chạy kiểm tra ánh sáng khi preview đang Play:

```sh
unity command eval_file --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --format json -- --file /Users/nha-tran/Data/projects/lumaquarium/mobile/docs/tests/lighting-playmode.cs
```

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
unity command eval_file --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --format json -- --file /Users/nha-tran/Data/projects/lumaquarium/mobile/docs/tests/decoration-playmode.cs
```

Kiểm tra thủ công thêm trên Android/iPhone: thao tác một/hai ngón, đổi hướng ứng dụng, thoát/mở lại, ảnh chụp và hiệu năng sau đặt nhiều món. Chưa build hoặc đo FPS trên thiết bị thật.
