# Coral collection implementation plan

Goal: ship the approved ten coral designs as real Unity prefabs usable from the decoration shop.
Spec: ../specs/2026-10-27-coral-collection-design.md. User has explicitly requested implementation of the approved visual direction; execute inline.

- [ ] Record all existing catalog IDs/kinds and demonstrate missing new species with an editor test.
- [ ] Add CoralCollectionBuilder.cs: deterministic geometry, ten distinct forms and natural rock mounts. Reuse five released IDs and append five new ones after large items in AquariumDecorCatalogBuilder; leave stones/rubble alone.
- [ ] Add AquariumCoral.shader: vertex color, fine procedural relief, lighting/shadows/night/fog compatible with existing aquarium.
- [ ] Add focused upgrade command to rebuild only corals, portraits and dependent shop/inventory bindings, with backups. Save generated meshes/prefabs/materials through Unity APIs.
- [ ] Verify catalog ID stability, meshes/indices/normals/colors/bounds and placement, material compilation, rendered contact sheet and separate in-tank preview scene. Inspect actual images and tune as needed.
- [ ] Run relevant catalog/placement/shop checks and play-mode frame advancement; preserve player data and restore original scene. Report actual results and device-test limits.

Review focus: legacy inventory identity; catalog rebuild stability; hollow tube openings and two-sided plates; shader/night compatibility; no scene or player-data loss.
