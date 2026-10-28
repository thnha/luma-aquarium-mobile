# Cấu hình cá (một file JSON cho mỗi con)

Mọi thông số của một con cá nằm trong **một file**:
`mobile/Assets/_Project/Art/Fish/<Tên>/<Tên>.json`.
File mẫu: [`fish-config-template.json`](fish-config-template.json) (cá trắng, Unity tô màu). Ví dụ thật: `Art/Fish/PlakatGalaxy/PlakatGalaxy.json`.
Cá **giữ màu gốc của file Meshy** (build M3, dáng của file): dùng [`fish-config-template-meshy.json`](fish-config-template-meshy.json).
File này chỉ có `build` và `shop`; `model` và `look` lấy từ mẫu chung `"base": "MeshyColour"` (xem **Mẫu chung** bên dưới),
đã chỉnh sẵn khử ám xanh (`iridescence: 0.1`). Ví dụ thật: `Art/Fish/KoiBetta/KoiBetta.json`.

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

   Cá giữ màu Meshy: copy `fish-config-template-meshy.json` thay vì template trên, chỉ sửa `build.input` và
   `shop`. Giải nén zip Meshy vào `betta-splendens/input/<Tên>/meshy_zip/`. Màu, độ bóng, kích cỡ lấy từ mẫu
   `MeshyColour`; con nào cần khác thì thêm đúng khoá đó vào `look`/`model` của nó.
3. **Build trong Blender** (chạy từ thư mục `betta-splendens/`, khoảng 2–3 phút vì có bake):
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender -b source/Betta_Splendens_M3.blend --python pipeline/build_fish.py -- --config ../mobile/Assets/_Project/Art/Fish/RedDragon/RedDragon.json
   ```
   Kết quả nằm ở `betta-splendens/work/RedDragon/`, và 7 file Unity cần
   (`.fbx`, `_VariantMask`, `_BaseColor`, `_PatternMask`, `_Normal`, `_Detail`, `_ScaleCells`) được copy tự động vào thư mục cá.
   Với `baseTexture: true`, build dừng báo lỗi nếu không bake được màu (mesh đầu vào nhẹ hơn `faces`), thay vì ra cá trắng.
4. **Kiểm tra rig** (nên làm với cá mới):
   ```bash
   /Applications/Blender.app/Contents/MacOS/Blender -b work/RedDragon/RedDragon.blend --python pipeline/check_fish.py -- work/RedDragon
   ```
   Xem `work/RedDragon/RedDragon_weights.png` và `RedDragon_poses.png`: thân phải màu xanh dương, vây không bị kéo rách.
   Nếu đầu cá nằm ngược đuôi, đặt `"flip": true` rồi build lại.
5. **Import vào Unity.** Mở scene `MainAquarium`, **thoát Play Mode**, chạy menu
   **Lumaquarium > Fish > Import Pipeline Fish and Update Shop**.
   Thư mục có JSON mà chưa có `RedDragon.asset` sẽ được tạo `.asset` tự động. Sau đó importer tạo material, prefab,
   variant và cập nhật cửa hàng. Importer tự Refresh nên không cần bấm Assets > Refresh sau khi build Blender. Một cá
   lỗi chỉ bị bỏ qua (Console ghi `Not built: …`); các cá khác và cửa hàng vẫn được dựng, cá lỗi chưa từng import
   thì không lên cửa hàng. Bản sao scene trước mỗi lần dựng nằm ở `mobile/Backups/Scenes/` (ngoài `Assets`, giữ 20 bản mới nhất).
6. **Thử rồi mới bán.** Template để `"inShop": false`. Chỉnh `size` và màu cho đến khi ưng ý, rồi đổi `"inShop": true`
   và import lại.

## Sửa một con cá đã có
- Đổi màu, kích cỡ, tên, giá: sửa JSON, rồi chạy menu import (bước 5). Không cần Blender.
- Đổi hình dáng (độ dày vây, độ rộng thân…): sửa phần `build`, rồi chạy lại bước 3 và bước 5.
- Muốn xem nhanh hình dáng: thêm `--no-bake` vào cuối lệnh build (chạy khoảng 20 giây). Bản no-bake chỉ nằm trong
  `betta-splendens/work/<Tên>/`, **không** được copy vào Unity (normal/detail map của nó không khớp, cá màu Meshy sẽ ra
  trắng). Khi đã ưng ý thì build lại đầy đủ.

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
| `baseTexture` | `false` | `true` = giữ **màu gốc của file Meshy**: bake texture màu của mesh gốc vào `_BaseColor.png` thay cho bản trắng. Dùng kèm `look.baseTexture: 1`. Ví dụ: GalaxyBetta. Với `body: "master"` (template M4): màu thân Meshy được chiếu sang thân mẫu (dáng M4, màu theo file), vây giữ màu gốc, mắt của thân mẫu; chỗ hai thân lệch nhau (mang, miệng) màu có thể hơi lệch. Ví dụ: SkyKoiBettaM4. |
| `whitenPaleBlue` | `0` | Chỉ khi `baseTexture: true`. Màu trắng của Meshy thường ngả xanh, vây nhạt thường là xanh lơ, nên trong hồ trông như bị ám xanh. Số này là độ bão hoà tối đa của màu xanh nhạt sẽ được đổi thành **trắng trung tính** (giữ độ sáng). `0` = tắt, `0.44` = SkyKoiBetta (vây xanh nhạt thành trắng bạc, giữ mảng xanh đậm). Cao hơn thì làm trắng cả màu xanh đậm hơn. Đỏ, cam, vàng không bị đổi. Cần build lại. |
| `pelvicSplay` | `12` | Góc (độ) mỗi vây bụng xoè ra khỏi mặt phẳng giữa thân. Thấp hơn thì hai vây khép lại; số âm thì chụm vào nhau. Dùng khi nhìn từ phía trước thấy hai vây bụng tách chữ V, hở bụng. RoyalKoiBetta dùng `-6`. Cần build lại. |
| `colourReference` | `""` | Chỉ khi `baseTexture: true`. Thư mục ảnh chụp cá thật (ảnh đã tách nền hoặc nền trắng, ví dụ ảnh đưa vào Meshy), tính từ `betta-splendens/`. Meshy thường tô texture tối hơn ảnh khoảng 1/3. Bước này chỉnh độ sáng và độ bão hoà của **từng nhóm màu** (xanh, đỏ, vàng, trắng/đen…) trên texture cho khớp với ảnh, giữ nguyên sắc màu và hoa văn. Chạy trước `whitenPaleBlue`. RoyalKoiBetta dùng `input/Betta1024e/reference`. Cần build lại. |
| `hueShift` | không có | Chỉ khi `baseTexture: true`. `[gốc, đích, mức, rộng, sáng thêm]`: đổi **sắc màu** mà `colourReference` giữ nguyên. Các màu cách sắc `gốc` (độ, 0–360) trong khoảng `rộng` độ được đẩy `mức` (0–1) phần đường về sắc `đích` và sáng thêm tới `sáng thêm` (0.3 = +30%), nhạt dần ra mép khoảng; màu xám/trắng/đen giữ nguyên. Chạy sau `colourReference`. EmeraldRedBetta dùng `[182, 135, 0.65, 40, 0.3]` (thân xanh ngọc → xanh lục bảo). Cần build lại. |

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
| `iridescence` | `0.4` | Ánh kim trên thân. Cao = bạc, crôm; thấp = satin, ngọc trai. Lớp ánh này ngả **xanh tím** ở góc nghiêng (lực = 2 × số này), nên với cá giữ màu Meshy (`baseTexture: 1`) nó phủ một màn xanh lên thân: dùng `0.1` (KoiBetta). |
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
| `envReflection` | `1` | Phản chiếu cảnh xung quanh (0–1). Lớp phản chiếu này phủ một màng xám lên chỗ đen và làm đỏ nhạt đi; với cá giữ màu Meshy (`baseTexture: 1`) để `0`, kèm `brightness` ~1.4 và `vibrance` ~1.2 để màu trong hồ gần với ảnh chụp. Ánh bóng từ đèn chính vẫn giữ. RoyalKoiBetta dùng `0`. |
| `baseTexture` | `0` | `1` = tô cá bằng màu texture gốc (cần build với `build.baseTexture: true`), bỏ qua `pattern` và các màu ở trên; vảy, độ trong của vây, độ bóng vẫn áp dụng. `0` = tô theo hoa văn. |
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

## Mẫu chung (`"base"`)
Nhiều cá cùng một dòng dùng chung `model` và `look` từ một file trong `Assets/_Project/Data/Betta/FishTemplates/<Mẫu>.json`.
JSON của cá ghi `"base": "<Mẫu>"`; khoá nào cá tự ghi thì thắng khoá của mẫu (từng khoá một, ví dụ chỉ `look.iridescence`).
`build` và `shop` luôn nằm trong file của cá. Sửa mẫu rồi chạy import là mọi cá dùng mẫu đó đổi theo.

| Mẫu | Dùng cho |
|---|---|
| `MeshyColour` | Cá giữ màu Meshy (build M3, `baseTexture`): KoiBetta, BlueFlameBetta, RedSteelBetta, SkyKoiBetta, SkyKoiBettaM4, GalaxyBetta |

Sheet giá (tab Cá) cũng hiện mẫu thành một dòng `mẫu: <Mẫu>` và cột `mẫu` của từng cá.

## Lỗi hay gặp
- **`no pattern …`**: sai tên `pattern` (phân biệt hoa thường, ví dụ `KoiGalaxy` chứ không phải `koigalaxy`).
- **`… needs [r, g, b]`** hoặc **`… needs [start, end]`**: màu phải có đúng 3 số, vùng phải có đúng 2 số.
- **`Copy <Tên>.fbx … into …`**: chưa build Blender (bước 3), hoặc build bị dừng giữa chừng.
- **`no template …`**: sai tên `base`, hoặc thiếu file trong `Data/Betta/FishTemplates/`.
- **`baseTexture needs the albedo bake`**: mesh đầu vào nhẹ hơn `faces`, nên không có bước bake màu; hạ `faces` xuống dưới số mặt của mesh.
- **Import không thấy cá mới**: tên file JSON phải trùng tên thư mục (`RedDragon/RedDragon.json`); khác tên thì file bị bỏ qua.
- **`unknown build keys`**: gõ sai tên khoá trong `build` (ví dụ `fin_thickness` thay vì `finThickness`).
- **Thiếu khoá**: được, khoá thiếu lấy giá trị của mẫu (`base`) nếu có, không thì cột "Mặc định" ở bảng trên (không phải giá trị trong file template ở `docs/`).
- JSON không cho viết chú thích (`//`). Muốn ghi chú thì ghi vào file này hoặc vào `description`.
