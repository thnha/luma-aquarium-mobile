# Hiệu ứng cộng xu khi xem quảng cáo

Ngày: 2026-10-27 · Trạng thái: đã duyệt

## Mục tiêu

Khi server xác nhận thưởng quảng cáo (`ClaimAdReward` thành công), người chơi thấy xu bay vào ví trong cửa hàng, thay vì số dư chỉ nhảy lên.

## Người chơi thấy gì (khoảng 1,2 giây, giờ thật)

1. 10 đồng xu (sprite `utility_coin`) bung ra từ nút "Xem video", lệch nhau 0,05 giây.
2. Mỗi đồng bay theo đường cong bậc hai vào ô ví ở góc cửa hàng, nhỏ dần khi tới nơi.
3. Đồng nào chạm ví thì ví nảy nhẹ và kêu "ting". Tiếng "ting" là clip `ton_*` của AquariumAudio, cao độ tăng dần, phát thẳng (không qua mixer) để không bị snapshot Muffled của cửa hàng làm đục.
4. Số dư trong ví đếm dần từ số cũ lên số mới, từ lúc đồng đầu tiên chạm ví đến lúc đồng cuối chạm.
5. Chữ "+20" màu vàng nổi lên dưới ví rồi mờ dần.

## Thành phần

- `UI/CoinRewardEffect.cs`: chỉ làm hiệu ứng. `Setup(ví, nhãn số dư, sprite xu)` và `Play(nguồn, số cũ, số mới)`. Lớp phủ, các đồng xu và chữ "+20" được tạo bằng code một lần rồi dùng lại; chúng không nhận chạm. Trong lúc chạy, `LateUpdate` ghi con số đang đếm vào nhãn số dư, đè lên `ShopScreen.Refresh()`. Hàm tĩnh `CountAt(from, to, t)` tính số hiển thị, có test EditMode.
- `AdMobRewardedAds.RewardGranted(int coins)`: chỉ phát khi `ClaimAdReward` trả về `ShopError.None`.
- `RewardedAdShopPanel`: tạo hiệu ứng khi dựng hàng nút, nghe `RewardGranted`, chạy hiệu ứng nếu cửa hàng đang mở. Thưởng được nhận bù khi cửa hàng đóng thì chỉ cập nhật số như cũ.
- `ShopScreen.Balance`: số dư hiện tại (đọc lại save).

## Ngoài phạm vi

Hiệu ứng cho các nguồn xu khác, ví ở màn hình bể cá, clip âm thanh xu riêng (để sau, thay clip là đủ).

## Kiểm tra

Test EditMode cho `CountAt`. Thử trong Editor: bật Lumaquarium → Shop → Use Server in Editor, Play, mở cửa hàng, xem quảng cáo test, chụp màn hình lúc xu đang bay. Trên iPhone cần bản TestFlight mới.
