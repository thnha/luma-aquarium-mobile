# Shop trên server

Ví và đồ của người chơi nằm trên server (Unity Gaming Services). Game hỏi server cho mọi lần mua và đổi đồ, rồi giữ câu trả lời trên máy để đọc và để chơi khi không có mạng. Thiết kế: [spec](../../docs/superpowers/specs/2026-10-23-monetization-design.md).

## Thành phần

| Phần | Ở đâu | Làm gì |
|---|---|---|
| Luật mua bán | `Scripts/Shop/Shared/` | `ShopSave`, `ShopRules`, `ShopCatalog`, `ShopReply`. Unity và module Cloud Code biên dịch **cùng các file này** |
| Module Cloud Code | `CloudCode/LumaquariumShop/` | Hàm `GetState`, `BuyItem`, `SelectItem`, `ApplyGifts`, `ImportLocalSave`. Save là item protected `shop` của Cloud Save |
| Bảng giá server | `CloudCode/LumaquariumShop/LumaquariumShop/ShopCatalog.json` | Sinh bởi **Lumaquarium › Shop › Write Server Catalog**, tự chạy sau Pull và trước khi build content |
| Client | `Scripts/Shop/` | `ShopService` chọn backend; `ServerShopBackend` gọi server, `LocalShopBackend` chạy luật trên máy |

## Chế độ

- **Bản build** luôn dùng server.
- **Editor** mặc định chạy luật trên máy (như trước). Bật **Lumaquarium › Shop › Use Server in Editor** để chơi với server thật.
- Khi dùng server: màn tải game đăng nhập ẩn danh, gửi save cũ của máy lên (giữ bản gốc ở `Lumaquarium.FishShop.v1.imported.bak`), rồi phát gói quà. Không có mạng (hoặc server chậm quá ~8 giây) thì hồ vẫn mở với save trên máy, còn mua và đổi đồ báo "Cần kết nối mạng".

## Gửi save cũ lên server

Server không tin save của máy, nên `ImportLocalSave` chỉ giữ phần hợp lệ:

- Chỉ giữ món có trong bảng giá hoặc gói quà. Cá, hồ, vách, nền tối đa 1 mỗi loại; mỗi đồ trang trí tối đa 99 (`ImportItemCap`).
- Ví chỉ giữ xu, tối đa 5.000 (`ImportCoinCap`).
- `starter_v1` luôn được tính là đã nhận, nên người chơi cũ không nhận thêm gói khởi đầu.
- Món đang dùng chỉ giữ khi người chơi sở hữu và đúng loại.
- Save không dùng được thì server trả `SaveUnreadable`.

Phía client (`ShopService.Prepare`):

- Chỉ một `Prepare` chạy tại một thời điểm; lời gọi đến trong lúc đó nhận cùng một task.
- `<key>.imported.bak` được ghi trước lần gửi save cũ đầu tiên và giữ nguyên từ lần thử đầu (không bị ghi đè ở các lần sau). Save cũ không đọc được được giữ ở `Lumaquarium.FishShop.v1.unreadable.bak` và bị save của server thay ở câu trả lời đầu tiên.
- `<key>.imported` chỉ được đặt khi server trả `None`, `AlreadyImported` hoặc `SaveUnreadable`. Với câu trả lời khác (Offline, Rejected...), `Prepare` dừng trước `ApplyGifts` và thử lại lần sau, để save cũ không bị save mới đè.
- Màn shop chờ `Prepare` xong rồi mới mua hoặc đổi đồ. Trong lúc việc gửi save cũ còn chờ, `ServerShopBackend` từ chối mua và đổi đồ với "Cần kết nối mạng để mua và đổi đồ."

## Đổi giá hoặc gói quà

Như [shop-data.md](shop-data.md): sửa sheet, Pull, chạy test, rồi `Tools/publish_content.sh`. `upload_content_r2.sh` tự chạy `Tools/deploy_cloud_code.sh` (test module, rồi `ugs deploy`) **trước** khi upload, nên server có giá mới trước khi người chơi tải content mới. Một máy còn content cũ mà mua sẽ nhận "Giá vừa được cập nhật".

## Cài đặt một lần trên máy làm việc

1. .NET SDK (đã có), UGS CLI: `npm install -g ugs`.
2. `ugs login` bằng service account `lumaquarium-deploy` (Cloud Code Editor, Unity Environments Viewer). Không lưu Key ID và Secret key vào repo. Nếu `ugs deploy` báo thiếu quyền, thêm vai trò Cloud Code cho service account trong dashboard.
3. `ugs config set project-id <id>` và `ugs config set environment-name production`.

## Kiểm tra

- Module: `dotnet test CloudCode/LumaquariumShop.Tests` (22 test).
- Game: Test Runner › EditMode, `FishShopInventoryTests`, `ShopServiceTests`, `ServerCatalogWriterTests`, `ShopEconomyTests`.

## Chưa có (các mốc sau)

Xem quảng cáo nhận xu (M-B), IAP gói xu (M-C), cá premium và Khôi phục giao dịch (M-D), liên kết và xoá tài khoản (M-E). Hiện chưa liên kết tài khoản: cài lại app là một người chơi mới.
