# Lumaquarium — đề xuất redesign giao diện (chờ duyệt)

Bản xem trước tương tác (sơ đồ luồng + 12 màn): https://claude.ai/artifact/13fS4ioUHtvvvDrN3bYrkj

Bản này dùng lại artwork, icon, font Nunito và bảng màu đã duyệt trong `Assets/_Project/UI/sprite-manifest.json` (ink `#343C2D`, cream `#F1E6C8`, sage `#CFDDBB`, forest `#455D3F`). Chưa thay đổi code hay scene Unity.

## Vấn đề ở bản hiện tại

- Hai cửa hàng khác nhau hoàn toàn: Tiệm cá có nền hồ, nút ← tròn và chip xu; Vườn thủy sinh có nền xanh phẳng, nút chữ "‹ Về hồ" và số xu dạng chữ.
- HUD hồ không hiện ví xu. Lối vào cửa hàng là hai nút chữ "Mua cá" và "Cây · Lũa · Đá" xếp dưới Cài đặt / Ẩn UI. Nút "Ban ngày" nằm giữa đỉnh màn.
- Có 3 đường vào shop trang trí (nút góc phải, panel Trang trí, ô Cây/Đá/Lũa). Ô trong panel Trang trí mở shop thay vì mở kho.
- Nhóm hàng không thống nhất: panel Trang trí dùng Cây / Đá / Lũa, shop dùng Cây thủy sinh / Lũa / Đá · Non bộ.
- Start chỉ có một nút "Bắt đầu" rồi sang Login để chọn chơi khách, nên người chơi phải bấm hai lần.
- Thanh đặt đồ dùng 4 nút chữ cùng kiểu, nút xác nhận "Đặt" không nổi bật.

## Quy tắc chung mới

| Thành phần | Quy tắc |
|---|---|
| Thanh trên | Trái: chip cá (chân dung, tên, thanh độ no) hoặc nút ← tròn 44 px. Phải: chip xu và nút Cài đặt. |
| Ví xu | Luôn ở góc phải trên, trong hồ và trong cửa hàng, cùng một kiểu chip. |
| Dock | 5 nút: Cho ăn · Chăm sóc · Trang trí · Cửa hàng · Ảnh. Nút đang mở được tô sage. |
| Sheet | Nền cream, bo 26 px, nổi trên dock, tiêu đề 22 px với nút ✕ ở bên phải. Chạm ✕ hoặc chạm lại nút dock thì đóng. |
| Nút | Chính: forest, chữ cream, cao 52–54 px. Phụ: cream đậm có viền. Nút khóa: xám be. Mỗi vùng chỉ có một nút chính. |
| Nhóm hàng | Cửa hàng: Cá · Cây · Lũa · Đá. Trong kho Trang trí, nhóm Cây tách ra thành Cây / Rêu / Bèo. |

## Luồng mới

1. **Khởi động** (gộp Start và Login): *Chơi ngay* → Đang tải. *Đăng nhập tài khoản* mở sheet có nhãn Sắp có. Người chơi quay lại thấy nút *Tiếp tục hồ của bạn*.
2. **Đang tải** → Hồ cá. Khi lỗi: *Thử lại* / *Quay lại*, nằm trong cùng một thẻ.
3. **Hồ cá** là màn trung tâm. Từ dock mở Chăm sóc, Trang trí (kho), Cửa hàng; nút Ảnh chụp rồi hiện thông báo. Chip cá mở Chăm sóc, chip xu mở Cửa hàng.
4. **Trang trí** chỉ quản lý kho. Chọn món còn trong kho → Đặt đồ. Món đã hết → Cửa hàng. Có thêm *Sắp xếp lại* và *Mua thêm*.
5. **Cửa hàng chung** có tab Cá / Cây / Lũa / Đá, khung trên xem trước món đang chọn, thanh dưới cố định hiện giá và nút chính. Mua → hộp Xác nhận → thông báo thành công (*Về hồ ngắm cá* / *Xem tiếp*).
6. **Đặt đồ**: chip món đang đặt và số còn trong kho, gợi ý chuyển đỏ khi vị trí không hợp lệ, công cụ Xoay / Cất / Hủy và nút chính *Đặt* (bị khóa khi vị trí không hợp lệ).
7. **Cài đặt**: Âm thanh, Ánh sáng ngày/đêm (chuyển từ nút giữa màn vào đây), Góc nhìn, Ẩn giao diện, Hồ sơ khách.

## Ảnh hưởng khi triển khai (sau khi duyệt)

- `FrontEndScreenBuilder` / `StartGameScreen` / `LoginGameScreen`: gộp Start và Login vào một màn, giữ `GuestIdKey`.
- `MainAquariumScreenBuilder` / `MainAquariumScreen`: thêm chip xu và nút dock Cửa hàng, bỏ hai nút chữ shop, chuyển ngày/đêm và Ẩn UI vào Cài đặt, panel Trang trí đổi thành lưới kho.
- `FishShopScreen` + `DecorShopScreen`: dùng chung một khung cửa hàng có tab. Logic ví và kho (`FishShopInventory`, `AquariumDecoration`) giữ nguyên.
- Icon dock Cửa hàng hiện tạm dùng `utility_coin`. Cần vẽ icon minh họa riêng cùng phong cách 4 icon hành động.
