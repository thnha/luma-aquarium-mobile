# Đăng nhập Apple / Google (Firebase → Unity Authentication)

Luồng: nút trên màn **LoginGame** → SDK native (AppleAuth trên iOS, Google Sign-In) → Firebase Auth (`SignInWithCredentialAsync`) → Firebase ID token → Unity Authentication OpenID Connect `oidc-firebase`.

- Lần đầu: **link** vào player khách hiện tại → xu, cá, đồ mua trên Cloud Code giữ nguyên.
- Tài khoản Firebase đã thuộc player khác (cài lại app, máy mới) → máy chuyển sang player đó; tiến trình khách mới trên máy này bỏ lại.
- Unity Auth lưu session token, nên `CloudCodeShopCloud` (`SignInAnonymouslyAsync`) tự vào lại player đã đăng nhập ở các lần mở sau.
- Apple chỉ hiện trên iOS; Android chỉ có Google (Google chiếm chỗ nút Apple).

Code: `Scripts/Account/AccountSignIn.cs`, `Scripts/UI/LoginGameScreen.cs`, `Scripts/Editor/SignInIosPostprocessor.cs`, builder `Lumaquarium > UI > Build Login Screen`.

SDK native chỉ được biên dịch khi có define **`LUMA_SOCIAL_SIGNIN`**. Chưa bật thì nút vẫn hiện nhưng báo "Cách đăng nhập này chưa dùng được trên máy này".

## Còn phải làm

1. **Gói** (Packages/manifest.json):
   - Firebase App + Firebase Auth (.tgz từ developers.google.com/unity/archive). Dự án đã có `com.unity.external-dependency-manager`; nếu trùng với `com.google.external-dependency-manager` của Firebase thì giữ một bản.
   - AppleAuth: `"com.lupidan.apple-signin-unity": "https://github.com/lupidan/apple-signin-unity.git?path=Source#v1.4.3"`.
   - Google Sign-In Unity plugin (API `Google.GoogleSignIn`, `GoogleSignInConfiguration`).
2. **Firebase config**: `GoogleService-Info.plist` và `google-services.json` đặt trong `Assets/`. Bundle id `com.thnha.bettafishaquarium`. Android cần SHA-1 của keystore trong Firebase console.
3. **`AccountSignIn.GoogleWebClientId`**: client_id loại 3 (Web client) trong `google-services.json`.
4. **Firebase console → Authentication**: bật provider Apple và Google.
5. **Apple Developer**: bật capability Sign in with Apple cho App ID (postprocessor thêm entitlement vào Xcode).
6. **Unity Dashboard → Authentication → Identity providers → OpenID Connect**: tên `oidc-firebase`, Issuer `https://securetoken.google.com/<firebase project id>`. Lỗi "invalid audience" thường do project id không khớp.
7. Player Settings → Scripting Define Symbols (iOS + Android): thêm `LUMA_SOCIAL_SIGNIN`.
8. Thử trên máy thật: khách mua/nhận xu → đăng nhập → xoá app, cài lại → đăng nhập → xu còn.

Chưa có: nút đăng xuất, hiển thị trạng thái "đã đăng nhập" ở StartGame/MainAquarium (badge "Đăng nhập · Sắp có" vẫn còn).
