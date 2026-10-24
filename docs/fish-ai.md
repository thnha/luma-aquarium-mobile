# AI cá betta: hành vi, tâm trạng và việc còn lại

Tài liệu này mô tả AI của cá trong bể (cập nhật 2026-10-24), các file liên quan, cách chỉnh và
danh sách việc chưa làm. Mọi thông số đều chỉnh được trong Inspector, không phải sửa code.

## Các file

| File | Vai trò |
|---|---|
| `Scripts/BettaFishAI.cs` | Bộ máy trạng thái (FSM): chọn việc cá làm, cách bơi, tránh đá và kính |
| `Scripts/BettaMood.cs` | Tâm trạng và độ thân thiết, lưu giữa các lần mở app. **Không có trong scene**: `BettaFishAI` tự gắn vào cá lúc chạy |
| `Scripts/BettaMouth.cs` | Ngáp, bọt ở miệng khi đớp khí hoặc thổi bọt |
| `Scripts/BubbleNest.cs` | Tổ bọt trên mặt nước. Tạo lúc chạy, không có trong scene |
| `Scripts/BettaAppearance.cs` | Màu cá. Nhạt khi ngủ, sợ hoặc trốn; rực hơn khi vui (`Paleness`, `Vividness`) |
| `Scripts/BettaFinPhysics.cs` | Vây: dựng lên khi hưng phấn, cụp khi nghỉ, xòe khi Display hoặc duỗi vây (`Arousal`, `FlareAmount`) |
| `Scripts/BettaReactions.cs` | Clip Flare khi chạm trúng cá, cờ Resting cho Animator |
| `Scripts/FeedingSystem.cs` | Chạm vào bể: thả mồi, cá xòe, cá giật mình, cá bơi tới chỗ chạm |
| `Scripts/FoodPellet.cs` | Viên mồi: nổi, chìm, nằm trên cát; `Spit()` khi cá nhả ra |
| `Scripts/AquariumLighting.cs` | Ngày/đêm theo giờ máy (21h–6h), nút Ngày/Đêm ghi đè tạm |
| `Scripts/AquariumDecoration.cs` | Sự kiện `Placed` khi bấm Đặt một món đồ, để cá tới xem |
| `Scripts/UI/MainAquariumScreen.cs` | Dòng trạng thái trong bảng chăm sóc (hành vi, tâm trạng, thân thiết) |
| `Shaders/AquariumBubble.shader` | `_Foam` làm bọt tổ đục. Chỉ tổ dùng, qua một bản sao vật liệu |

## Các trạng thái (`BettaFishAI.State`)

| Trạng thái | Khi nào | Cá làm gì |
|---|---|---|
| Wander | Mặc định | Bơi theo nhịp quạt đuôi rồi lướt. Hay ra phía kính trước; khi chán càng hay ra kính |
| Idle | Sau nhiều việc khác | Đứng yên, trôi nhẹ. Thỉnh thoảng ngáp hoặc duỗi vây |
| Rest | Ngẫu nhiên ban ngày; cả đêm khi ngủ | Nằm dưới đáy (hơi nghiêng), trên đỉnh đá hoặc gỗ, hoặc sát mặt nước |
| Breathe | 1–3 phút một lần (đói thì thường hơn, ngủ thì ít hơn) | Ngoi lên đớp khí, đôi khi thổi vài bọt |
| SeekFood / Eat | Có mồi trong bể | Lao tới đớp. 25% số lần nhả ra rồi đớp lại |
| SwimToTap | Chạm vào kính | Bơi tới chỗ chạm. Cá thân (từ 50%) có thể nhảy múa ở đó |
| Flee | Chạm trúng cá, hoặc đổi đèn | Quay ngoắt rồi vọt đi. Cá thân chạy ngắn hơn |
| Display | Chạm khi cá no và đang nhìn ra; hoặc thấy bóng mình trên kính trước | Bung mang, xòe vây, đi ngang khoe sườn |
| Forage | Đói | Mổ cát, kính và đồ trang trí tìm mồi sót |
| Patrol | No, ban ngày, ngẫu nhiên (cách nhau ít nhất 45 giây) | Đi 4 góc bể, dừng ở mỗi góc nhìn ra |
| Greet | Mở app, hoặc quay lại app sau hơn 30 giây | Ra kính trước, ngước đầu, lắc người và nhấp lên xuống. Cá còn lạ thì chỉ khoảng một nửa số lần |
| Inspect | Sau khi bấm Đặt đồ, lúc màn hình chỉnh bể đã đóng | Bơi tới, nhìn từ 2–3 phía, mổ thử |
| Nest | No, ban ngày (cách nhau ít nhất 60 giây, tổ càng thưa càng hay làm) | Đớp khí rồi thổi bọt vào tổ, dưới cây nổi hoặc dọc vách bên |
| Hide | Bị chạm trúng 3 lần trong 20 giây (2–4 lần tùy độ sợ và thân thiết) | Trốn sau cây hoặc đá 12–20 giây, nhạt màu, cụp vây. Thả mồi thì cá ra ăn |

Những hành vi chồng lên trạng thái: duỗi vây và rùng mình (khi Idle hoặc lúc thức dậy), ngáp
(`BettaMouth`), cụp vây khi đói, nhạt màu khi ngủ, sợ hoặc trốn.

### Ngày và đêm
- Đêm là từ 21h đến 6h theo giờ máy (`AquariumLighting.nightFromHour` / `nightToHour`; tắt `followClock`
  thì chỉ đổi bằng nút).
- Nút Ngày/Đêm ghi đè tới mốc giờ kế tiếp. Lựa chọn này chưa lưu khi tắt hẳn app.
- Tắt đèn: cá giật mình, 2–5 giây sau đi ngủ, nhạt màu trong khoảng 4 giây, ít lên thở hơn.
  Bị đánh thức thì 6–12 giây sau ngủ lại.
- Bật đèn: cá giật nhẹ, 1–3 giây sau dậy, ngáp, duỗi vây.

## Tâm trạng và độ thân thiết (`BettaMood`)

| Chỉ số | Tăng | Giảm | Lưu? |
|---|---|---|---|
| Độ vui (0–1) | Được ăn +0.06, chơi +0.02, chào +0.05, có đồ mới +0.05 | Bị dọa −0.1, đổi đèn −0.03. Không có gì xảy ra thì trôi về mức nền = 0.5 + 0.2·thân − 0.4·đói − 0.3·chán − 0.3·sợ | Có |
| Độ sợ (0–1) | Bị dọa +0.35, đổi đèn +0.15 | Giảm 0.03 mỗi giây | Không |
| Độ chán (0–1) | Đầy sau 10 phút không ai chơi (không tăng lúc ngủ) | Mọi lần được chú ý | Không |
| Thân thiết (0–1) | Ăn +0.012, chơi +0.004, chào +0.006, nhân với (1 − thân hiện tại); tối đa 0.1 mỗi ngày | Bị dọa −0.02. Vắng quá 2 ngày thì −0.01 mỗi ngày (tối đa −0.2) | Có |

- **Lưu:** PlayerPrefs `Lumaquarium.FishMood.v1.<tên BettaVariant>`, riêng cho từng con. Lưu mỗi 30 giây,
  khi app vào nền và khi thoát. Muốn reset thì xoá key này.
- **Tâm trạng hiện ra:** Buồn ngủ, Sợ (sợ > 0.45), Đói, Chán (chán > 0.6), Vui (vui > 0.62),
  Bình thường. Thứ tự này cũng là thứ tự ưu tiên.
- **Ảnh hưởng:**
  - Vui: bơi nhanh hơn, hay tuần tra, làm tổ và duỗi vây hơn, màu rực hơn tới 15%.
  - Chán: lười hơn, hay ra kính trước, hay xòe với bóng hơn.
  - Sợ: nhạt màu.
  - Thân: ít giật mình, hăng bơi tới chỗ chạm, chào thường hơn và lâu hơn, nhảy múa ở chỗ chạm.
- **Mức thân thiết:** Còn lạ (< 20%), Quen dần (< 45%), Thân (< 70%), Rất thân.

## Thử nhanh trong Editor

- Vào Play từ LoadingGame (hoặc StartGame → Begin), vì MainAquarium được tải qua Addressables.
- **Không sửa .cs và không recompile khi đang Play.** Domain reload giữa Play làm các biến tạo trong Awake
  về null, gây hàng nghìn lỗi NullReference. Hãy dừng Play, sửa code, rồi vào lại.
- Ép cá vào một trạng thái để thử: gọi các hàm `Enter…` (private) qua reflection bằng `unity cmd eval`,
  ví dụ `EnterPatrol`, `EnterNest`, `EnterSleep`. Muốn thấy hành vi ngắn (nhả mồi) thì đặt
  `Time.timeScale = 0.2`.
- Camera mặc định nằm dưới mặt nước: phần mặt nước sát kính trước bị HUD che, nên tổ bọt nhìn rõ hơn khi
  xoay camera.

## Việc còn lại

### Hành vi chưa làm
- [ ] **A5 Nằm võng lá:** nghỉ trên lá to (ráy, lá cây cao). Cần biết vị trí lá: trong `AquariumDecorItem`
      hiện chỉ có `foliage`, chưa có điểm đặt.
- [ ] **A8 Quạt đuôi khi Display:** cần clip animation mới. Animation M2 chưa làm.
- [ ] **B2 Bơi theo ngón tay:** cá thân bơi theo khi kéo ngón tay. Đang vướng: thao tác kéo dùng để xoay
      camera (`OrbitTouchCamera`). Cần quy ước khác, ví dụ nhấn giữ rồi kéo.
- [ ] **B4 Nhảy đớp mồi:** giữ mồi trên mặt nước, cá rình rồi nhảy lên đớp. Cần clip và cho cá được ra
      khỏi `swimBounds` một chút.
- [ ] **C2 Tính cách riêng từng con:** dạn/nhát, siêng/lười, tham ăn, có thể lưu trong `BettaVariant` hoặc
      sinh từ `seed`. Nhân vào các thông số sẵn có (chance, speed, hideAfterStartles).
- [ ] **C4 Nhịp sinh học trong ngày:** sáng hoạt bát, trưa lười. Đêm đã có. Có thể nhân `Happy`/`zest`
      theo giờ máy.
- (Đã bỏ: bơi dọc kính liên tục, vì trông như lỗi game.)

### Việc dang dở, cần kiểm tra
- [ ] **Thử trên điện thoại thật** (màn hình dọc): tổ bọt có thấy rõ không; vẫy chào khi quay lại app
      (`OnApplicationPause`); ngày/đêm theo giờ máy; dòng trạng thái có tràn chữ không (đang bật autosize 24–36).
- [ ] **Thử trên thiết bị:** phần giảm thân thiết khi lâu không mở app (`lastSeenTicks`) chưa thử qua nhiều
      ngày thật.
- [ ] **Lưu tổ bọt** giữa các lần mở app. Hiện mở lại app là tổ mất.
- [ ] **Lưu lựa chọn Ngày/Đêm bấm tay** giữa các lần mở app. Hiện mở lại thì bể theo giờ máy.
- [ ] **Bình minh/hoàng hôn sáng dần** (đã bàn, chưa làm). Hiện đúng 21:00 và 6:00 bể đổi ngày/đêm
      trong 1,5 giây (`AquariumLighting.Update`, `step = dt / 1.5`), trông như tắt công tắc, và cá giật mình.
      Ý định:
      - Khi theo giờ máy, tính `NightBlend` theo phút: hoàng hôn khoảng 20:00–21:00, bình minh khoảng 5:30–6:30.
      - Hoàng hôn ngả cam ấm rồi tối dần; cá chậm lại, ít tuần tra, rồi tìm chỗ ngủ.
      - Đèn tối dần nên không gọi `ReactToLights`/`Spooked`, cá không giật mình.
      - Bình minh sáng dần; cá dậy từ từ, ngáp, duỗi vây.
      - Nút Ngày/Đêm vẫn đổi nhanh như hiện tại.
      - Nên làm cùng C4 (nhịp trong ngày), vì cả hai dựa trên giờ máy.
- [ ] **Animation M2** (Swim/Turn/Nibble/Flare/Rest trên rig `Betta_Splendens_M2.blend`) chưa làm. A8 và B4
      cần có animation trước.
- [ ] Hiển thị tâm trạng ngoài HUD chính (chip tên cá), ví dụ một icon nhỏ. Cần sửa scene, mà scene là
      nội dung tải từ xa, nên phải upload lại content.
- [ ] Các thay đổi code đi theo bản build app kế tiếp; shader `AquariumBubble` có thêm `_Foam`. Kiểm tra
      lại nếu bundle remote content được build trước khi có thay đổi này.
