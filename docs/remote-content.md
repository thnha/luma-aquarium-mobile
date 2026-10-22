# Remote content (Addressables + Cloudflare R2)

The app ships only code and the front end scenes (Start, Login, Loading). `MainAquarium`, with the fish and
decor it uses, is Addressables content hosted on Cloudflare R2. The loading screen downloads it on the first
launch. Later launches fetch only the bundles that changed, and with no network the cached copy opens.

## What can change without a new app build

| Change | Remote update? |
|---|---|
| Fish (models, textures, JSON configs, materials), decor prefabs, decor catalog, MainAquarium layout and UI, shaders, materials | Yes, `Tools/publish_content.sh` |
| New fish folder in `Art/Fish/` | Yes (`RemoteContent.SetUp` adds each fish folder on every build) |
| C# changes: new scripts, new serialized fields, new component types | **No**: needs a new app build and a new Player Settings > Version |
| Start / Login / Loading scenes, app icon, Player Settings | No |

Each app version reads its own folder, `<BaseUrl>/<iOS|Android>/<Player Settings Version>/`, so installs on an
older version keep the content that matches their code. **Bump Version** (e.g. 0.1.0 -> 0.2.0) whenever the
code changes in a way the content depends on. The first upload for a new version comes from the app build.

## Layout

- `Assets/_Project/Scripts/Editor/RemoteContent.cs`: `BaseUrl` (the bucket's public URL), groups, profile and
  content build. Menu **Lumaquarium > Remote Content**.
- Group `Remote Aquarium`, packed separately: the `MainAquarium` scene, `Art/Betta`, `Art/Aquarium3D` (decor and
  the decor catalog) and one entry per `Art/Fish/<Name>` folder. Changing one fish re-downloads only that fish.
- The catalog name is fixed (`catalog_lumaquarium`), so installed apps keep finding new ones.
- MainAquarium is no longer in Build Settings; `GameLoadingScreen` opens it with `Addressables.LoadSceneAsync`.
- Play in the Editor reads the assets directly (Play Mode Script "Use Asset Database"): no build or upload needed.
- Output: `ServerData/<BuildTarget>/<version>/` (gitignored).

## One-time setup (Cloudflare)

1. Cloudflare dashboard > R2 > Create bucket, e.g. `lumaquarium-content`.
2. Bucket > Settings > Public access: enable the r2.dev subdomain (or connect a custom domain).
   Put that URL in `RemoteContent.BaseUrl`, without a trailing slash.
3. R2 > Manage API tokens > Create token with Object Read & Write on that bucket.
4. `cp Tools/r2.env.example Tools/r2.env` and fill in the account ID, key ID, secret and bucket.
5. `brew install awscli`

## Day to day

Content only (art, fish, decor, scene), with the Unity editor closed:

```bash
Tools/publish_content.sh ios      # or android / all
```

With the editor open: switch to the platform, **Lumaquarium > Remote Content > Build Content (Active Platform)**,
then `Tools/upload_content_r2.sh iOS`.

New app build (code changes): `Tools/build_testflight.sh` builds the content, uploads it to R2, then uploads the app.
`Tools/build_android.sh` does the same for Android (`--no-content-upload` to skip).

## Notes

- App Store: downloading art and scenes is allowed; downloading code is not, which is why scripts stay in the app.
- The first launch needs a network connection (the loading screen shows retry on failure).
- Uploads never delete old bundles; clean up old version folders in the bucket by hand if storage matters.
