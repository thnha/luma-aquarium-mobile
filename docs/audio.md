# Âm thanh bể cá: hướng dẫn thay file

Âm thanh hiện tại là **bản tạm tạo bằng code**. Tài liệu này hướng dẫn thay bằng file thật (tự thu, hoặc tải
từ thư viện CC0) mà không phải sửa code.

## Thay file trong 4 bước

1. Chuẩn bị file mới theo bảng ở mục "Danh sách file" (đúng **tên**, đúng **kiểu**).
2. Thoát Play trong Unity. Chép file vào `Assets/_Project/Audio/Placeholder/`, ghi đè file cũ.
   - Dùng được `.wav`, `.ogg`, `.mp3`. Nếu file mới khác đuôi, ví dụ `water_loop.ogg` thay cho
     `water_loop.wav`, phải **xoá file cũ** đi, kể cả file `.meta` đi kèm. Nếu không, Unity có hai file
     trùng tên và builder có thể lấy nhầm file cũ.
3. Chạy menu **Lumaquarium > Audio > Build Aquarium Audio**. Builder sẽ:
   - đặt import setting cho mobile (mono; file lặp nén Vorbis, file ngắn dùng ADPCM),
   - gắn lại các clip vào `AquariumAudio` trên prefab `AquariumEnvironment`.
4. Vào Play (từ LoadingGame), nghe thử, rồi chỉnh âm lượng theo mục "Chỉnh âm lượng".

## Danh sách file

| Tên file | Là tiếng gì | Khi nào phát | Yêu cầu |
|---|---|---|---|
| `water_loop` | Tiếng nước nền rất nhẹ (tiếng "hù hù") | Lặp liên tục | **Lặp liền mạch**, 20–60 giây |
| `bubble_loop` | Tiếng máy sủi oxi chạy đều | Lặp liên tục, nhỏ dần khi máy sủi tắt | **Lặp liền mạch**, 10–30 giây, không quá dày |
| `bloop_01`, `bloop_02`, … | Một bọt vỡ trên mặt nước ("bủm") | Khi bọt máy sủi vỡ (20% số bọt), và khi bọt cá thổi vỡ | Mỗi file 0.1–0.4 giây, một bọt duy nhất |
| `ripple_01`, `ripple_02`, … | Tiếng nước "lách tách" khi mặt nước gợn | Khi chạm vào mặt nước, hoặc khi di chuyển đồ trang trí | 0.3–0.6 giây, có tiếng giọt nhỏ, **tránh tiếng "xì"** |
| `gulp_01`, `gulp_02`, … | Cá betta đớp khí ("chụt") | Mỗi lần cá ngoi lên đớp khí | 0.1–0.3 giây |

Ghi chú:
- **Số lượng file bloop/ripple/gulp không cố định.** Có thể thêm `bloop_07`, `bloop_08`… hoặc bớt đi, builder
  lấy tất cả file có tên bắt đầu bằng tiền tố đó. Nên có 3–6 file mỗi loại để nghe không bị lặp. Game đã tự
  đổi cao độ và âm lượng ngẫu nhiên mỗi lần phát.
- **Lặp liền mạch:** điểm cuối file phải nối khít với điểm đầu, không có tiếng "cụp" hay khoảng lặng. Hầu hết
  thư viện ghi rõ "seamless loop". Nếu tự cắt, có thể dùng Audacity: cắt tại điểm biên độ bằng 0
  (zero crossing), hoặc crossfade đuôi vào đầu.
- **Tiếng sạch:** không có tiếng người, tiếng xe, tiếng máy lạnh phía sau. Cắt sát đầu file ngắn (không để
  khoảng lặng ở đầu), nếu không tiếng sẽ phát trễ so với hình.
- **Định dạng:** 44.1 kHz hoặc 48 kHz. Stereo cũng được, builder tự chuyển sang mono.

## Chỉnh âm lượng

Chọn object `AquariumDecor` (trong prefab `Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab`), mở
component **AquariumAudio**:

| Thông số | Hiện tại | Tác dụng |
|---|---|---|
| Water Volume | 0.1 | Độ to tiếng nước nền. Đặt 0 để bỏ hẳn |
| Bubble Volume | 0.22 | Độ to tiếng máy sủi |
| Bloop Volume | 0 (đang tắt) | Độ to tiếng bọt vỡ ("bủm"). Bọt cá thổi phát bằng mức này × 0.6 (`Pop Volume` trên BettaMouth). Muốn bật lại thì thử khoảng 0.2–0.3 |
| Pop Chance | 0.2 | Tỷ lệ bọt máy sủi vỡ có tiếng. Tăng thì "lỏn tỏn" nhiều hơn |
| Ripple Volume | 0.5 | Độ to tiếng lách tách khi chạm mặt nước |
| Gulp Volume | 0.55 | Độ to tiếng cá đớp khí |
| Ripple Gap | 0.4 | Khoảng cách tối thiểu (giây) giữa hai tiếng lách tách |

Lưu ý khi chỉnh:
- **Chỉnh lúc đang Play sẽ mất khi thoát Play.** Ghi lại số đã chọn, thoát Play, rồi nhập vào prefab.
- Chạy lại builder **không** đổi các số này. Builder chỉ gắn lại file âm thanh và import setting.

## Mixer

`Assets/_Project/Audio/AquariumMixer.mixer`:

- **Nhóm:** Master → Ambience (Water, Bubbles), SFX, UI.
- **Snapshot Default:** bình thường.
- **Snapshot Muffled:** dùng khi mở shop. Âm thanh bị nghẹt như nghe từ ngoài kính (low-pass 900 Hz trên
  Master), và Ambience nhỏ đi 6 dB. Có thể chỉnh trực tiếp trong cửa sổ **Window > Audio > Audio Mixer**.

## Lấy âm thanh ở đâu

- **Freesound.org:** lọc giấy phép **Creative Commons 0** (không cần ghi nguồn). Từ khoá gợi ý:
  - `aquarium bubbles`, `air pump aquarium`, `air stone`
  - `bubble pop`, `underwater bubble`
  - `water droplet`, `water drip small`
  - `fish gulp`, `mouth pop`
  - Tránh file CC-BY-NC (không dùng thương mại được). File CC-BY thì phải ghi nguồn trong app.
- **Sonniss GDC Game Audio Bundle:** miễn phí, dùng thương mại được, không cần ghi nguồn.
- **Tự thu bằng điện thoại:**
  - Máy sủi oxi thật đặt cạnh bể.
  - Thổi ống hút vào cốc nước (cho tiếng bọt vỡ).
  - Nhỏ giọt nước vào chậu (cho tiếng lách tách).
  - Thu ở phòng yên tĩnh, đặt máy gần nguồn âm.

Khi thay xong, nên ghi lại nguồn và giấy phép của từng file, ví dụ vào cuối tài liệu này.

## Phát hành

`AquariumAudio` là code mới, nên âm thanh phải đi cùng **bản build app tiếp theo**. Không upload riêng content
lên R2 (xem `remote-content.md`). Sau khi bản build đó phát hành, việc thay file âm thanh (cùng tên) chỉ là thay
content, có thể cập nhật bằng `Tools/publish_content.sh` mà không cần build lại app.

## Code liên quan

- `Scripts/AquariumAudio.cs`: phát các lớp âm thanh, theo dõi bọt vỡ, xử lý mặt nước gợn, muffle.
- `Scripts/Editor/AquariumAudioBuilder.cs`: tạo mixer, import setting, gắn clip vào prefab.
- `Scripts/BettaMouth.cs`: tiếng cá đớp khí và tiếng bọt cá thổi vỡ.
- `Scripts/UI/ShopScreen.cs`, `FishShopScreen.cs`, `DecorShopScreen.cs`: gọi `AquariumAudio.Muffle` khi mở và
  đóng shop.
