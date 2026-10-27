# Đăng nhập Apple / Google (Firebase → Unity Authentication)

Luồng: nút trên màn **LoginGame** → SDK native (AppleAuth trên iOS, Google Sign-In) → Firebase Auth (`SignInWithCredentialAsync`) → Firebase ID token → Unity Authentication OpenID Connect `oidc-firebase`.

- Lần đầu: **link** vào player khách hiện tại → xu, cá, đồ mua trên Cloud Code giữ nguyên.
- Tài khoản Firebase đã thuộc player khác (cài lại app, máy mới) → máy chuyển sang player đó; tiến trình khách mới trên máy này bỏ lại.
- Unity Auth lưu session token, nên `CloudCodeShopCloud` (`SignInAnonymouslyAsync`) tự vào lại player đã đăng nhập ở các lần mở sau.
- Apple chỉ hiện trên iOS; Android chỉ có Google (Google chiếm chỗ nút Apple).

Code: `Scripts/Account/AccountSignIn.cs`, `Scripts/UI/LoginGameScreen.cs`, `Scripts/Editor/SignInIosPostprocessor.cs`, builder `Lumaquarium > UI > Build Login Screen`.

SDK native chỉ được biên dịch khi có define **`LUMA_SOCIAL_SIGNIN`**. Chưa bật thì nút vẫn hiện nhưng báo "Cách đăng nhập này chưa dùng được trên máy này".

## Đã cài (2026-10-27)

- `Packages/com.google.firebase.app`, `com.google.firebase.auth` 13.17.1 (nhúng từ .tgz; 14.x bắt buộc Swift Packages nên chưa lên).
- `Packages/com.google.signin`: fork Thaina/google-signin-unity nhánh `newmigration` commit d5ab541 (Android Credential Manager, iOS GoogleSignIn 9). Plugin tự ghi GIDClientID + URL scheme vào Info.plist.
- AppleAuth `com.lupidan.apple-signin-unity` v1.5.0 (git).
- Ba gói nhúng đã bỏ phụ thuộc `com.google.external-dependency-manager`: dự án dùng `com.unity.external-dependency-manager` (cùng `Assets/MobileDependencyResolver` của AdMob).
- Define `LUMA_SOCIAL_SIGNIN` bật cho iOS + Android; `GoogleWebClientId` lấy từ google-services.json (Firebase project `betta-fish-aquarium-3d`).

## Còn phải làm

1. ~~Tải lại `GoogleService-Info.plist`~~: xong 2026-10-27, đã có `CLIENT_ID`/`REVERSED_CLIENT_ID` (Web client ID lấy từ `GoogleWebClientId` trong code).
2. **Android SHA-1**: thêm SHA-1 (debug + release keystore, và Play App Signing) vào app Android trong Firebase, rồi tải lại `google-services.json`. Thiếu thì Google sign-in báo DEVELOPER_ERROR.
3. **Firebase console → Authentication**: bật provider Apple và Google.
4. **Apple Developer**: bật capability Sign in with Apple cho App ID (postprocessor thêm entitlement vào Xcode).
5. **Unity Dashboard → Authentication → Identity providers → OpenID Connect**: tên `oidc-firebase`, Issuer `https://securetoken.google.com/betta-fish-aquarium-3d`.
6. Thử trên máy thật: khách nhận xu → đăng nhập → xoá app, cài lại → đăng nhập → xu còn.

Chưa có: nút đăng xuất, hiển thị trạng thái "đã đăng nhập" ở StartGame/MainAquarium (badge "Đăng nhập · Sắp có" vẫn còn).
