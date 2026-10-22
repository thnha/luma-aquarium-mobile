# Cấu hình cá (một file JSON cho mỗi con)

Mọi thông số của một con cá nằm trong **một file**:
`mobile/Assets/_Project/Art/Fish/<Tên>/<Tên>.json`.
File mẫu: [`fish-config-template.json`](fish-config-template.json). Ví dụ thật: `Art/Fish/PlakatGalaxy/PlakatGalaxy.json`.

| Phần | Nội dung | Ai đọc | Khi sửa thì chạy lại |
|---|---|---|---|
| `build` | mesh gốc, hình dáng, độ dày vây | Blender (`build_fish.py --config`) | build Blender **rồi** import Unity |
| `shop` | tên, mô tả, giá, có bán không | Unity (import) | chỉ import Unity |
| `model` | kích cỡ, biên độ bơi | Unity (import) | chỉ import Unity |
| `look` | hoa văn, màu, độ bóng, mép vây | Unity (import) | chỉ import Unity |

JSON là **nguồn chính**: mỗi lần import, Unity ghi đè `<Tên>.asset` và `Data/Betta/Variants/<Tên>.asset` từ JSON.
Sửa trong Inspector sẽ bị mất ở lần import sau, nên luôn sửa trong JSON.

---

## Thêm một con cá mới

Ví dụ tên cá là `RedDragon` (tên = tên thư mục = tên file, không dấu, không khoảng trắng).

1. **Mesh gốc.** Xuất từ Meshy dạng FBX, cá **trắng trơn** (màu do Unity tô, chỉ cần chi tiết vảy và tia vây).
   Đặt vào `betta-splendens/input/RedDragon/`.
2. **Tạo thư mục và JSON.** Tạo `mobile/Assets/_Project/Art/Fish/RedDragon/`, copy `fish-config-template.json` vào
   đó và đổi tên thành `RedDragon.json`. Sửa ít nhất:
   - `build.input`: `"input/RedDragon/<tên file>.fbx"` (tính từ thư mục `betta-splendens/`)
   - `shop.displayName`, `shop.shopId` (duy nhất, ví dụ `"red-dragon"`), `shop.description`
   - `look.pattern` và các màu
3. **Build trong Blender** (chạy từ thư mục `betta-splendens/`, khoảng 2–3 phút vì có bake):
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender -b source/Betta_Splendens_M3.blend --python pipeline/build_fish.py -- --config ../mobile/Assets/_Project/Art/Fish/RedDragon/RedDragon.json
   ```
   Kết quả nằm ở `betta-splendens/work/RedDragon/`, và 6 file Unity cần
   (`.fbx`, `_VariantMask`, `_BaseColor`, `_PatternMask`, `_Normal`, `_Detail`) được copy tự động vào thư mục cá.
4. **Kiểm tra rig** (nên làm với cá mới):
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender -b work/RedDragon/RedDragon.blend --python pipeline/check_fish.py -- work/RedDragon
   ```
   Xem `work/RedDragon/RedDragon_weights.png` và `RedDragon_poses.png`: thân phải màu xanh dương, vây không bị kéo rách.
   Nếu đầu cá nằm ngược đuôi, đặt `"flip": true` rồi build lại.
5. **Import vào Unity.** Mở scene `MainAquarium`, **thoát Play Mode**, chạy menu
   **Lumaquarium > Fish > Import Pipeline Fish and Update Shop**.
   Thư mục có JSON mà chưa có `RedDragon.asset` sẽ được tạo `.asset` tự động. Sau đó importer tạo material, prefab,
   variant và cập nhật cửa hàng.
6. **Thử rồi mới bán.** Template để `"inShop": false`. Chỉnh `size` và màu cho đến khi ưng ý, rồi đổi `"inShop": true`
   và import lại.

## Sửa một con cá đã có
- Đổi màu, kích cỡ, tên, giá: sửa JSON, rồi chạy menu import (bước 5). Không cần Blender.
- Đổi hình dáng (độ dày vây, độ rộng thân…): sửa phần `build`, rồi chạy lại bước 3 và bước 5.
- Muốn xem nhanh hình dáng: thêm `--no-bake` vào cuối lệnh build (chạy khoảng 20 giây). **Đừng import bản no-bake**,
  vì normal map và detail map của nó không còn khớp. Khi đã ưng ý thì build lại đầy đủ.

---

## Ý nghĩa từng thông số

### `build`: hình dáng mesh (Blender)
| Khoá | Mặc định | Ý nghĩa |
|---|---|---|
| `input` | – | File Meshy gốc (FBX/GLB/OBJ), tính từ `betta-splendens/`. |
| `flip` | `false` | `true` nếu pipeline nhận nhầm đầu thành đuôi. |
| `faces` | `30000` | Số mặt sau khi giảm lưới. Cao hơn thì mịn hơn nhưng nặng hơn trên điện thoại. |
| `depth` | `1` | Độ cao thân (lưng–bụng). `1.2` = thân cao hơn 20%. |
| `width` | `1` | Độ dày thân (hai bên hông). Dùng khi nhìn thẳng mặt thấy cá bị dẹt. PlakatGalaxy dùng `1.5`. |
| `fins` | `{}` | Co/giãn chiều dài vây theo nhóm, ví dụ `{"caudal": 0.8, "dorsal": 1.25, "ventral": 0.55}`. `caudal` = đuôi, `dorsal` = lưng, `ventral` = vây hậu môn + vây bụng. Nhỏ hơn 1 là ngắn lại. |
| `finThickness` | `1` (template: `0.4`) | Độ dày vây so với mesh gốc. `1` = giữ nguyên. `0.4` thực tế giảm còn khoảng một nửa, vì những chỗ vây bị ép tới mức hai mặt chạm nhau thì pipeline tự giảm lực ép. 3 cá hiện tại dùng `0.2`. Vây trông dày chủ yếu do đục và rãnh sâu: chỉnh `finRelief`, `finTranslucency`, `finRimAlpha` trước. |
| `landmarks` | `""` | File JSON chỉnh lại vị trí mắt, mang, hàm, gốc vây (xem phần "Landmarks" trong `betta-splendens/pipeline/README.md`). |
| `bake` | `true` | Bake normal map và AO từ mesh gốc. Chỉ tắt khi thử nhanh. |

### `shop`: cửa hàng
| Khoá | Ý nghĩa |
|---|---|
| `displayName` | Tên hiện trong game. |
| `inShop` | `true` = có bán trong cửa hàng. |
| `shopId` | Mã duy nhất (lưu trong dữ liệu người chơi). **Không đổi sau khi đã phát hành.** |
| `description` | Dòng mô tả trên thẻ cửa hàng (viết tiếng Việt có dấu được). |
| `price` | Giá. `0` = miễn phí. |

### `model`: kích cỡ và chuyển động
| Khoá | Mặc định | Ý nghĩa |
|---|---|---|
| `size` | `2` | Kích cỡ so với rig mẫu. Plakat và PlakatGalaxy dùng `2`, Koi Nemo dùng `2.4`. |
| `bodySwimAmplitude` | `3` | Độ uốn thân khi bơi. Cá thân ngắn, chắc (Plakat) nên để thấp. |
| `strokeSwimAmplitude` | `8` | Độ quẫy của cuống đuôi mỗi nhịp bơi. |

### `look`: màu sắc và bề mặt
Màu ghi dạng `[r, g, b]`, mỗi số từ 0 đến 1 (giống thanh màu trong Inspector). Lưu ý shader tính màu tuyến tính, nên
một màu tối pha 40% màu sáng vẫn trông khá xám.

| Khoá | Mặc định | Ý nghĩa |
|---|---|---|
| `pattern` | `"Solid"` | Tên hoa văn (file trong `Data/Betta/Patterns`, xem bảng dưới). |
| `primary` | xanh dương | Màu chính. |
| `secondary` | trắng | Màu phụ (mép vây, đốm, dải). |
| `accent` | đen | Màu nhấn (đốm Koi, vảy Dragon, mảng đỏ Koi Galaxy…). |
| `finEdge` | vàng | Galaxy: mảng vàng và viền vây dưới. Koi Galaxy: màu nền vây. |
| `iridescence` | `0.4` | Ánh kim trên thân. Cao = bạc, crôm; thấp = satin, ngọc trai. |
| `textureDetail` | `0.6` | Mức vảy và tia vây làm tối màu. Thấp = màu mịn, sữa. |
| `silverSheen` | `1` | Ánh bạc của bản đồ kim loại. `0` = satin, không kim loại (trắng sữa). |
| `gloss` | `1` | Nhân độ bóng (0–1.5). Dưới 1 làm vệt sáng bớt kiểu sứ. |
| `blackGloss` | `0` | Chỉ cho `KoiGalaxy`: vảy đen và viền vây đen thành **đen tuyền** (từ 0.25 trở lên: đen hơn, bỏ ánh bạc, bỏ ánh ngũ sắc), số càng cao càng bóng: `0.25` satin, `0.5` bóng nhẹ, `1` sơn mài. Không bị ảnh hưởng bởi `gloss`. PlakatGalaxy dùng `0.15` (dưới 0.25 bóng giảm thêm nhưng mảng đen cũng ngả xám than, có ánh bạc mờ), kèm `secondary` đen than `[0.22, 0.225, 0.24]`. |
| `whiteScales` | `0` | Chỉ cho `KoiGalaxy`: tỉ lệ vảy đen hoá thành **vảy trắng** ngọc trai, rải lẻ tẻ từng vảy (0–0.5). `0.04` = rất thưa, `0.08` thấy rõ, `0.15` khá dày. PlakatGalaxy dùng `0.04`. |
| `pigmentDepth` | `0` | Sắc tố đậm ở đầu, nhạt dần về đuôi (0–1). `0` = đều màu. |
| `finEdgeClear` | `0` | Mép vây trong và sáng hơn (0–2). |
| `finRelief` | `1` | Độ sâu rãnh giữa các tia vây (0–1). `1` = như bản bake, thấp hơn = vây phẳng, trông **mỏng** hơn. 3 cá hiện tại dùng `0.5`. |
| `finTranslucency` | `0.8` | Ánh sáng xuyên qua vây từ phía sau (0–2), sáng hơn về phía mép. 3 cá hiện tại dùng `1.2`. |
| `scaleRelief` | `1` | Độ nổi của vảy trên thân (0–1). `1` = như bản gốc (vảy cộm), thấp hơn = vảy **mỏng**, sát thân, rãnh giữa vảy nông hơn; vảy vẫn còn thấy ở mức thấp nhất. 3 cá hiện tại dùng `0.55`. |
| `finBlack` | `0` | Phần đen trên vây (viền vây, gốc vây tối) đen sâu thêm, tách riêng khỏi màu đen của thân (0–1). `0` = như thân. PlakatGalaxy dùng `1`. |
| `finBrightness` | `1` | Độ sáng các màu còn lại trên vây (0.5–2). `1` = như vẽ, `1.25` ≈ sáng thêm một tông, `1.8` = rất tươi (vàng ngả vàng chanh). PlakatGalaxy dùng `1.8`. |
| `vibrance` | `1` | Độ tươi (bão hoà) của cả con cá sau khi tính ánh sáng (0.5–2). Ánh sáng xanh dưới nước làm màu nhạt đi; `1.3` = tươi rõ, `1.6` bắt đầu cháy màu đỏ. PlakatGalaxy dùng `1.3`. |
| `brightness` | `1` | Độ sáng của cả con cá sau khi tính ánh sáng (0.5–2). PlakatGalaxy dùng `1.12`. |
| `finRimAlpha` | `1` | Độ đục ở chỗ mép vây trong nhất. `1` = tắt (vây đục hoàn toàn). 3 cá hiện tại dùng `0.6`, kèm `finRim` `[0.55, 1.0]` và `caudalRim` `[0.3, 0.8]` cho nửa ngoài vây trong như màng. |
| `finRim` | `[0.9, 1.0]` | Vùng mép trong của vây lưng, hậu môn, ngực, bụng: [bắt đầu, trong nhất], tính từ gốc vây 0 đến mép vây 1. |
| `caudalRim` | `[0.62, 0.8]` | Như trên nhưng cho vây đuôi. |
| `caudalStart` | `0.55` | Vây đuôi bắt đầu ở đâu dọc thân cá (mõm 0, chóp đuôi 1). |
| `seed` | `0` | Đổi vị trí đốm (Marble/Koi…). Cùng seed thì cùng đốm. |

### Hoa văn (`pattern`)
| Tên | Màu dùng thế nào |
|---|---|
| `Solid` | Thân `primary`, vây chuyển dần sang `secondary` ở mép. |
| `Bicolor` | Thân `primary`, vây phần lớn là `secondary`. |
| `Butterfly` | Vây có dải `secondary` rõ nét từ khoảng giữa vây ra mép. |
| `Pearl` | Như Butterfly nhưng dải chuyển mềm. |
| `Marble` | Đốm loang: nền `secondary`, đốm `primary`, chấm `accent`. |
| `Koi` | Như Marble, mép vây ngả về `secondary`. |
| `Dragon` | Thân `primary`, vảy sáng ánh `accent`, vây `primary` ra `secondary`. |
| `Galaxy` | Thân xen mảng `primary` và `finEdge`, lưng tối `secondary`, đầu `accent`, có chấm sao. |
| `KoiGalaxy` | Thân ngọc trai `primary`, mảng đỏ `accent`, vảy tối `secondary`, vây nền `finEdge`. |

Ngưỡng đốm, độ rộng dải, mật độ chấm… là thông số của chính hoa văn (file `.asset` trong `Data/Betta/Patterns`),
dùng chung cho mọi con cá có hoa văn đó, nên không nằm trong JSON của từng con.

---

## Lỗi hay gặp
- **`no pattern …`**: sai tên `pattern` (phân biệt hoa thường, ví dụ `KoiGalaxy` chứ không phải `koigalaxy`).
- **`… needs [r, g, b]`** hoặc **`… needs [start, end]`**: màu phải có đúng 3 số, vùng phải có đúng 2 số.
- **`Copy <Tên>.fbx … into …`**: chưa build Blender (bước 3).
- **Import không thấy cá mới**: tên file JSON phải trùng tên thư mục (`RedDragon/RedDragon.json`); khác tên thì file bị bỏ qua.
- **`unknown build keys`**: gõ sai tên khoá trong `build` (ví dụ `fin_thickness` thay vì `finThickness`).
- **Thiếu khoá**: được, khoá thiếu lấy giá trị cột "Mặc định" ở bảng trên (không phải giá trị trong template).
- JSON không cho viết chú thích (`//`). Muốn ghi chú thì ghi vào file này hoặc vào `description`.
