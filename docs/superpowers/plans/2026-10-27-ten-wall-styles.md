# Ten aquarium wall styles

Approved design: all ten wall studies from the preview board, selected by the user on 2026-10-27. Add basalt, limestone, sandstone, Amazon roots, frosted aqua, matte black, dragon stone, pearl marble, turquoise mosaic and sunken relic. Keep the existing six walls, floors, tanks and player saves.

Implementation uses the existing albedo-based wall shader, catalog and purchase flow. Relief is painted into textures; no new geometry or shader dependencies. Textures repeat on all three walls. English names/descriptions follow the existing localization table. Default prices follow the existing 300–450 coin range, with more detailed decorative designs up to 650; existing sheet overrides survive rebuilds.

- [x] Generate ten separate material assets using built-in ImageGen; retain prompts and provenance.
- [x] Extend AquariumSurfaceBuilder to load authored images without overwriting them, create existing-format shop portraits, and append stable catalog IDs. Verify catalog, imports, shop entries and preservation of old content.
- [x] Add Vietnamese/English labels, regenerate the surface and authoritative server catalogs in Unity. Do not publish remote content or deploy Cloud Code.
- [x] Verify compilation, purchase/save selection using isolated test inventory, runtime application of all ten textures, and capture actual gameplay previews. Restore the active wall after checks; leave the Walls shop tab open for inspection as requested by the integration task.

Verification: 153/153 Editor tests, 76/76 runtime checks, 52/52 Cloud Code tests; independent review clear. Evidence and device/deployment limits: `docs/previews/TenWallStyles/README.md`.

Review focus: missing textures must fail clearly; repeated builder runs preserve artwork and customized prices; new shop entries need thumbnails and translation; client/server IDs and prices match; tests must not spend the user's balance or alter ownership.

No commits or deployment requested. Execute directly in the current workspace as instructed by the user.
