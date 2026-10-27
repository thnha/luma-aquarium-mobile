# Approved coral collection

The user approved the ten-specimen concept and its in-tank mockup, and requested actual 3D models integrated into the game, as close visually as possible. References: generated images exec-6526dbe0-1909-4742-9da6-0462182a35da.png and exec-6e28d18d-5fa0-4d34-a348-c0ebde11b3d9.png in the conversation.

Build ten sculptural corals: peach staghorn, red lace fan, green plating coral, purple branches, gold torch, green/purple mushrooms, orange zoanthids, green/gold brain, blue hollow tubes, salmon fleshy lobes. Use small organic rock mounts, rich vertex color and a dedicated opaque URP coral shader with micro-relief and restrained transmission. Real meshes, colliders, catalog offers and rendered portraits; no image billboards. Aim for <=30,000 triangles each, shared materials and baked editor geometry; no added dependencies.

Approaches: existing procedural Unity authoring fits the current catalog, preserves editable generation and avoids dependencies (selected implementation choice). External generated meshes add tooling/dependencies and inconsistent topology. Flat cutouts cannot preserve 3D rotation.

Upgrade five matching released coral IDs/kinds in place. Keep rubble unchanged. Append five new kinds AFTER all released items, including large pieces, so saves and inventory retain their meaning. Preserve current prices for existing IDs. Do not change user wallet/layout. Build a separate preview scene and renders from real prefabs. Existing scene edits must be backed up. Verify geometry, stable IDs, catalog/shop wiring, shader compilation, and play-mode frame advancement. Device GPU performance remains a separate check.
