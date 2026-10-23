# Tích hợp IAP và quảng cáo

Hướng dẫn từng bước để làm M-B (xem quảng cáo nhận xu) và M-C/M-D (mua xu, mua cá bằng tiền thật), dựa trên server shop đã có ở M-A. Thiết kế: [spec](../../docs/superpowers/specs/2026-10-23-monetization-design.md). Server hiện tại: [shop-server.md](shop-server.md).

Nguyên tắc chung: **tiền thật chỉ đi qua store hoặc mạng quảng cáo, xu chỉ do server cộng.** Game không bao giờ tự cộng xu trên máy.

```
Người chơi ── trả tiền ──▶ Google Play / App Store ──▶ biên lai ──▶ Cloud Code RedeemPurchase ──▶ +xu / +cá
Người chơi ── xem video ─▶ LevelPlay ──▶ "xem xong" ──▶ Cloud Code ClaimAdReward ──▶ +20 xu
Google / Apple / LevelPlay ── chuyển khoản hằng tháng ──▶ ngân hàng của bạn
```

## 0. Chặn trước khi bán bằng tiền thật

Làm ở M-B, **trước** M-C (xem `.superpowers/sdd/progress.md`, phần Final review):

- [ ] `ImportLocalSave` không còn cho đồ miễn phí trên tài khoản mới: bỏ id cá premium (cột `iap`) khỏi import, giới hạn tổng giá trị đồ import, và đặt hạn chót cho việc import (sau ngày X thì server trả `SaveUnreadable`).
- [ ] Test EditMode so giá cá trong scene (`ShopScreen.fishOffers`) với `FishCatalogSheet.Items()`, để giá hiện ra luôn là giá server trừ.

## 1. Tài khoản (làm một lần)

| Việc | Ở đâu | Ghi chú |
|---|---|---|
| Google Play Console | play.google.com/console | 25 USD một lần. Tài khoản cá nhân mới cần 12 người test kín trong 14 ngày trước khi phát hành |
| Payments profile (merchant) | Play Console › Thiết lập › Hồ sơ thanh toán | Ngân hàng nhận ngoại tệ (SWIFT), thông tin thuế. Chưa có thì không tạo được sản phẩm IAP |
| Apple Developer Program | developer.apple.com | 99 USD/năm |
| Paid Apps Agreement | App Store Connect › Business (Agreements, Tax, and Banking) | Ký thoả thuận, khai ngân hàng và form thuế (W-8BEN / W-8BEN-E). Chưa ký thì IAP không chạy, kể cả sandbox |
| Small Business Program | developer.apple.com/app-store/small-business-program | Phí 15% thay vì 30% khi doanh thu dưới 1 triệu USD/năm. Google tự áp 15% cho 1 triệu USD đầu mỗi năm |
| LevelPlay | Unity Dashboard › Monetization (LevelPlay) | Tạo app Android và iOS, lấy **App Key**; khai thông tin thanh toán để nhận tiền quảng cáo (ngưỡng trả khoảng 100 USD/tháng) |
| Website nhỏ | Trang tĩnh bất kỳ (có thể dùng Cloudflare Pages cùng tài khoản R2) | Đặt `privacy-policy` và `app-ads.txt` ở gốc tên miền; tên miền này khai trong trang store của app |

Bundle id phải giống nhau ở Unity (Player Settings), Play Console và App Store Connect.

## 2. Xem quảng cáo nhận xu (M-B)

### 2.1 Cài đặt

1. Cài package **Ads Mediation** (`com.unity.services.levelplay`) bằng Package Manager. Dùng skill `unity:levelplay-unity-integration` nếu làm cùng Claude.
2. Bật mạng quảng cáo trong LevelPlay (Unity Ads có sẵn; thêm AdMob, Meta... sau khi có lượng người chơi), rồi chạy **Ads Mediation › Network Manager** để lấy adapter.
3. Tạo một ad unit **Rewarded** cho mỗi nền tảng, ghi lại id.
4. iOS: thêm `NSUserTrackingUsageDescription` và các `SKAdNetworkIdentifier` mà LevelPlay liệt kê. Android: kiểm tra Gradle resolve được sau khi thêm adapter.

### 2.2 Luồng trong game

| Bước | Code | Ghi chú |
|---|---|---|
| Khởi động | Sau `ShopService.Prepare()` ở màn tải game | iOS: hỏi ATT **trước** khi khởi tạo LevelPlay. Người chơi từ chối thì vẫn có quảng cáo, chỉ không cá nhân hoá |
| Tải quảng cáo | `LevelPlayRewardedAd.LoadAd()` | Tải trước, tải lại sau mỗi lần xem |
| Nút trong shop | Cạnh ví: "Xem quảng cáo +20 xu" | Hiện số lượt còn lại; chưa tải xong thì mờ "Đang tải quảng cáo"; hết lượt thì mờ kèm giờ mở lại |
| Xem xong | Sự kiện *rewarded* của LevelPlay | Lúc này mới gọi `ClaimAdReward`. Đóng sớm thì không gọi |
| Server trả lời | `ServerShopBackend` ghi `State` như các hàm khác | Bị từ chối (hết lượt, chưa đủ 60 giây) thì hiện lý do, không cộng |

Quảng cáo luôn do người chơi bấm xem và ghi rõ phần thưởng trước khi xem. Không chèn quảng cáo tự bật (interstitial) nếu chưa có gói "Tắt quảng cáo".

### 2.3 Server

- Hàm mới `ClaimAdReward` trong `ShopModule`, logic trong `ShopLogic`, cùng kiểu read → luật → ghi có khoá → thử lại như `Change`.
- Bộ đếm ở item protected `ads` của Cloud Save: `{ "day", "claimed", "lastClaimUtc" }`. Ngày tính theo giờ server UTC+7.
- Luật: tối đa 10 lần mỗi ngày, cách nhau ít nhất 60 giây, mỗi lần 20 xu. Đặt các con số này trong `ShopCatalog.json` (sinh từ sheet) để đổi bằng Pull, không cần sửa code.
- Test trong `LumaquariumShop.Tests`: đủ lượt, hết lượt, chưa đủ thời gian chờ, qua ngày mới.

Rủi ro đã chấp nhận: kẻ gian gọi thẳng `ClaimAdReward` mà không xem quảng cáo, tối đa 200 xu/ngày/người. Muốn chặn hẳn thì dùng **server-to-server callback** của LevelPlay: LevelPlay gọi một URL của mình kèm chữ ký sau mỗi lượt xem. Cloud Code không nhận được webhook từ ngoài, nên cần thêm một Cloudflare Worker kiểm chữ ký rồi ghi lượt thưởng chờ nhận vào Cloud Save bằng service account. Chỉ làm khi có dấu hiệu bị lạm dụng.

### 2.4 Kiểm tra

- Editor và máy thật: bật **test suite** của LevelPlay, dùng quảng cáo test. Không bấm quảng cáo thật của chính mình (có thể bị khoá tài khoản).
- Xem đủ 10 lần thì lần 11 bị chặn; đóng video sớm thì không có xu; tắt mạng thì nút báo "Cần kết nối mạng để mua và đổi đồ."

## 3. Mua xu và cá bằng tiền thật (M-C, M-D)

### 3.1 Sản phẩm trên store

Tạo cùng một bộ id trên cả hai store, có tiền tố bundle id:

| Id | Loại | Nhận được |
|---|---|---|
| `<bundle>.coins_small` | Consumable | 500 xu |
| `<bundle>.coins_medium` | Consumable | 3.000 xu |
| `<bundle>.coins_large` | Consumable | 7.000 xu |
| `<bundle>.fish_<shopId>` | Non-consumable | 1 cá premium (M-D) |

- Giá đặt trên store theo bậc giá (price tier), store tự đổi sang tiền địa phương. Shop hiện giá do store trả về, không tự viết giá.
- Cá premium khai trong tab **Cá** của sheet bằng cột `iap`. Dòng có `iap` thì không có giá xu.
- Bảng "id sản phẩm → phần thưởng" nằm trong `ShopCatalog.json` trên server. Server không bao giờ tin số xu do máy gửi lên.

### 3.2 Cài đặt

1. Cài **Unity IAP 5** (`com.unity.purchasing`). Dùng skill `unity:implement-in-app-purchases` nếu làm cùng Claude. Không dùng IAP 4 (đã ngừng hỗ trợ).
2. Bật In-App Purchasing trong Unity Dashboard cho project.
3. Android: dán **license key** (Play Console › Kiếm tiền › Thiết lập kiếm tiền) vào Unity Dashboard. iOS: bật capability In-App Purchase trong Xcode.

### 3.3 Luồng mua (hai bước, không mất tiền khi rớt mạng)

1. Người chơi chọn gói, Unity IAP mở hộp thanh toán của store.
2. Store trả về đơn ở trạng thái **pending** kèm biên lai.
3. Client gọi `RedeemPurchase(receipt)`. **Chưa confirm** đơn.
4. Server:
   - xác thực biên lai với store (bước 3.4);
   - kiểm tra mã giao dịch chưa có trong save (lưu danh sách mã đã đổi trong save, ví dụ `purchases`);
   - tra phần thưởng trong catalog, cộng xu hoặc thêm cá, ghi save có khoá;
   - trả `State` như mọi hàm khác.
5. Server trả `None` (hoặc báo mã đã dùng) thì client **confirm** đơn với Unity IAP.
6. Tắt app hoặc mất mạng giữa chừng: đơn vẫn pending, lần mở app sau Unity IAP đưa lại và client làm lại từ bước 3. Vì server từ chối mã đã dùng, xu không bị cộng hai lần.
7. Biên lai bị từ chối (giả, bị hoàn tiền) thì báo lỗi và **không** confirm.

Gửi `RedeemPurchase` sau `ShopService.Prepare()`, giống `BuyItem`, để không đụng việc gửi save cũ.

### 3.4 Xác thực biên lai trên server

| Store | Cách | Bí mật |
|---|---|---|
| Apple (StoreKit 2) | Giao dịch là JWS đã ký; server kiểm chữ ký theo chứng chỉ Apple, đối chiếu bundle id, product id, transaction id | Không cần khoá để kiểm chữ ký. Muốn tra hoàn tiền thì cần khoá App Store Server API |
| Google Play | Gọi Play Developer API `purchases.products.get` với purchase token | Service account Google Cloud có quyền xem đơn hàng trong Play Console |

- Bí mật để trong **Secret Manager** của UGS, đọc trong Cloud Code. Không đưa vào repo, không đưa vào game.
- Trước khi tự làm: xem Unity IAP 5 có dịch vụ xác thực sẵn không (câu hỏi mở số 5 trong spec).

### 3.5 Khôi phục giao dịch (M-D, bắt buộc trên iOS)

- Nút **Khôi phục giao dịch** trong Settings. Unity IAP restore và đưa lại các giao dịch non-consumable; client gửi từng cái cho `RedeemPurchase`. Cá đã có thì không thêm.
- Gói xu (consumable) không khôi phục được, đây là hành vi bình thường của store.

### 3.6 Kiểm tra

- iOS: tài khoản **Sandbox** (App Store Connect › Users and Access › Sandbox), hoặc file StoreKit Configuration trong Xcode khi chưa có sản phẩm thật.
- Android: thêm Gmail vào **License testing**, phát hành bản build lên track **Internal testing**; sản phẩm chỉ hiện khi app đã có trên track.
- Tình huống phải thử: mua thành công; huỷ giữa chừng; tắt app ngay sau khi trả tiền (mở lại phải nhận đúng một lần); mua trùng cá đã có; khôi phục sau khi cài lại app.

## 4. Chính sách store và quyền riêng tư

- [ ] Hàng hoá số (xu, cá) chỉ bán qua IAP, không có link thanh toán ngoài.
- [ ] Privacy policy công khai, link trong trang store và trong Settings của game.
- [ ] `app-ads.txt` ở gốc website khai trong store, lấy nội dung từ LevelPlay.
- [ ] Apple **App Privacy** và Google **Data safety**: khai dữ liệu do LevelPlay (quảng cáo, định danh thiết bị) và UGS (Player ID, dữ liệu game) thu thập.
- [ ] iOS: hộp thoại ATT trước khi khởi tạo LevelPlay.
- [ ] Người chơi ở châu Âu: hộp đồng ý (consent) theo GDPR; LevelPlay có hướng dẫn tích hợp Google UMP.
- [ ] Nếu nhắm cả trẻ dưới 13 tuổi: theo Families Policy của Google, đánh dấu trong LevelPlay, tắt quảng cáo cá nhân hoá.
- [ ] Khi có liên kết tài khoản (M-E): nút **Xoá tài khoản** trong Settings và trang web yêu cầu xoá cho Google Play.

## 5. Phát hành

1. Deploy server **trước** bản build có hàm mới: `Tools/deploy_cloud_code.sh` (hoặc `upload_content_r2.sh`, tự chạy nó).
2. Đổi giá hoặc phần thưởng: sửa sheet, Pull, rồi publish content cho **mọi nền tảng** cùng lúc. Nếu chỉ publish một nền tảng, máy ở nền tảng kia sẽ nhận "Giá vừa được cập nhật" mỗi lần mua.
3. Theo dõi tiền về:
   - Play Console › Báo cáo tài chính;
   - App Store Connect › Payments and Financial Reports;
   - LevelPlay › Reports (doanh thu, eCPM theo quốc gia).
4. Thu nhập từ store và mạng quảng cáo phải khai thuế ở Việt Nam; hỏi kế toán về cách khai cho cá nhân hay doanh nghiệp.
