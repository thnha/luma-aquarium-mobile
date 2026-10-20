# Front end screens implementation plan

## Goal and approved design

Import the approved natural aquarium UI into the mobile Unity project as real sprites, then create StartGame, LoginGame and LoadingGame scenes. Preserve the existing MainAquarium scene and fish work. Use the approved underwater artwork without room furniture or exterior views. The user selected guest play first, with account login UI prepared for later.

## Constraints

- Unity 6000.3.26f1, existing URP and uGUI packages. No new game dependency.
- Use TextMeshPro and Vietnamese Nunito static font instances, with OFL included.
- Keep icon artwork, backgrounds and labels separate; apply manifest nine slice borders.
- Guest access works locally. No fake account authentication or password storage.
- Loading progress comes from the real asynchronous MainAquarium scene load.
- Do not commit, overwrite user fish changes or regenerate MainAquarium.

## Tasks

- [x] Import 29 existing PNG sprites and clean front end artwork under Assets/_Project/UI. Add scoped TextureImporter configuration and an atlas for small UI assets.
- [x] Create reusable uGUI screen prefabs and three scenes using an Editor builder. Use a safe area, portrait CanvasScaler and the approved color palette.
- [x] Wire Start to Login, guest access to Loading, and Loading to MainAquarium. Keep account form visibly unavailable until an authentication service is implemented.
- [x] Verify sprite imports, nine slice borders, Vietnamese glyphs and enabled scene order. Play through the real guest flow, verify frame advancement, inspect screenshots and console errors.

## Files

- Assets/_Project/UI: imported images, fonts, atlas and prefabs.
- Assets/_Project/Scripts/UI: small screen controllers and shared scene names.
- Assets/_Project/Scripts/Editor/FrontEndAssetImporter.cs: texture import rules.
- Assets/_Project/Scripts/Editor/FrontEndScreenBuilder.cs: repeatable screen creation and validation.
- Assets/_Project/Scenes: StartGame, LoginGame and LoadingGame.
- ProjectSettings/EditorBuildSettings.asset: add entry scenes while preserving existing entries.

## Review focus

- Repeated taps must not create repeated scene loads or replace the existing guest ID.
- Back from the account form clears password text; account form never performs authentication.
- Missing target scene must leave a readable retry or back action instead of a stuck loading page.
- Long phones and notches must retain reachable buttons and readable labels.
- Building front end assets must not discard a dirty user scene.

## Verification baseline

Observed before implementation: 30 PNG files, 0 imported Sprite objects; all three front end scenes absent. Existing MainAquarium was open, not playing, and not dirty. Guest login selected by the user.

## Verification result

- Unity 6000.3.26f1 compiled the new scripts; final console query returned no new errors since the UI test baseline.
- 31/31 PNG files imported as Sprite; 0 incorrect borders. Both TMP fonts contain every glyph in the Vietnamese screen sample. All three prefabs exist and all four scene entries are enabled in the intended order.
- Play Mode frame count advanced. Start invoked twice reached Login. Guest invoked twice reached Loading; a second guest entry preserved the existing local ID. Account controls and submit are disabled; returning to guest cleared temporary password text.
- Captured Start, guest Login, account Login and Loading in the real Game view. Loading reached 100% and activated MainAquarium. A temporary runtime hold was used only to photograph loading; the saved minimum remains 1.2 seconds.
- Code review resolved the repeated Back tap guard. A follow-up Play Mode check simulated ShowError before automatic loading: the error message rendered correctly, the Retry button bounds were measured inside the safe area, Back invoked twice reached Login, and Retry from the simulated failure reached MainAquarium. Unity Editor still allowed loading a disabled scene, so this was a simulated loader failure rather than a genuine missing-scene reproduction. No device build was run.
- Refreshed all four main screenshots to 540×1169 and saved a fifth screenshot for the simulated loading error. Restored all four build entries, StartGame as the Editor start scene, and the original dirty MainAquarium scene after testing.
- Editor was being used concurrently, causing some interrupted checks; the successful checks above were repeated after scenes were ready. Existing fish errors observed before the UI baseline were outside this change.
