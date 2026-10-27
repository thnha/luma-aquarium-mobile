# Ten new wall styles — 2026-10-27

All ten approved wall concepts are integrated into the existing surface catalog and shop. There are now 16 walls. Source textures live in `Assets/_Project/Art/Aquarium3D/Surfaces/`; 256 × 256 rounded shop sprites live in `Assets/_Project/UI/Portraits/Shop/`. Albedos import with a 1024 px cap, compression, mipmaps and Repeat wrapping, with CPU readability disabled.

| ID | Vietnamese name | Default coins |
|---|---|---:|
| wall_basalt | Đá núi lửa đen | 400 |
| wall_limestone | Đá vôi trắng | 400 |
| wall_sandstone | Sa thạch phân tầng | 450 |
| wall_amazon_roots | Rễ cây Amazon | 550 |
| wall_frosted_aqua | Kính mờ xanh sương | 350 |
| wall_matte_black | Đen nhám | 300 |
| wall_dragon_stone | Đá rồng | 500 |
| wall_pearl_marble | Cẩm thạch ngọc trai | 550 |
| wall_turquoise_mosaic | Mosaic xanh ngọc | 600 |
| wall_sunken_relic | Di tích chìm | 650 |

`all-ten-in-game.jpg` is a contact sheet of unaltered Unity camera captures, all rendered at the same game frame, camera and scene arrangement. The ten individual PNGs are 540 × 960. `shop-basalt.png` shows the actual shop details. The images are game output, not the earlier concept art.

## Verification

- Unity 6000.3.26f1: all 153 Editor tests passed, including the three new AquariumWallCatalogTests. These three initially failed because the ten styles did not exist.
- MainAquarium Play Mode: 76 checks passed for all ten purchases, duplicate refusal, persisted selection/reselection, exact coin spending using a unique isolated test save, client/server prices and slots, actual `_BaseMap` / `_Tiling` / tint application, and complete wall shop entries. The player's inventory was unchanged; the active wall was restored to `wall_river`.
- Verified the player loop advancing from frame 145 to 148 before runtime checks. The runtime checks reported frame 1129; all ten camera captures were taken at frame 1921.
- Rebuilding the catalog preserved its complete serialized metadata; SHA-256 checks confirmed all wall PNG source files were unchanged by the rebuild.
- All 52 Cloud Code .NET tests passed. Server catalog version: `0a9d02415f4ca03f`; catalog source and client version match. No deployment performed.
- Independent read-only review found no actionable correctness issues. No new console errors after the runtime/capture checks; compilationFailed=false. Earlier console entries include deliberate invalid-save test logs and a verification-script invocation before MainAquarium was loaded.

Run from the mobile project directory:

```sh
unity command run_tests --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --format json -- --mode editor --filter AquariumWallCatalogTests --async_tests true
unity command test_status --caller plugin --skill unity-cli --project-path /Users/nha-tran/Data/projects/lumaquarium/mobile --format json
dotnet test CloudCode/LumaquariumShop.Tests --no-restore --verbosity quiet
```

For runtime checks, load `Assets/_Project/Scenes/MainAquarium.unity` in Play Mode, then evaluate `docs/tests/ten-wall-styles-playmode.cs` through `unity command eval_file`. `docs/tests/ten-wall-styles-capture.cs` regenerates the camera captures and restores the active wall afterward.

## Rebuild and delivery

`Lumaquarium > Build Surface Catalog` reads the authored PNGs and rebuilds thumbnails without overwriting artwork. It preserves price-sheet names, descriptions and prices. `Lumaquarium > Shop > Write Server Catalog` updates the Cloud Code catalog and client version after price/catalog changes.

Built-in ImageGen prompts and source file provenance are retained in `docs/art-generation/ten-wall-styles.json`. The final frosted-glass texture uses the refined output recorded in `docs/art-generation/frosted-aqua-refinement.json`.

The existing albedo shader supplies lighting, fog and water effects; rock/root relief is painted, not added geometry. Frosted aqua is an opaque wall style, not physical glass transmission. No new shader, runtime dependency or player-save schema was added.

Not yet verified on a physical iOS/Android device. Installed builds need a matching remote content build and Cloud Code module deployment to receive these new purchasable styles.
