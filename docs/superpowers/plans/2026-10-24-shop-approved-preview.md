# Approved shop preview implementation plan

**Goal:** Apply the approved compact shop preview to the existing Unity uGUI shop.
**Architecture:** Keep catalog IDs, save data, backend purchases and Ui3D sprites. Add three top-level groups and contextual subcategories; add tank offers to the existing shop transaction path. Rebuild only the generated shop prefab through its existing builder, backing up the scene first.
**Tech stack:** Unity 6000.3.26f1, uGUI, TextMeshPro, existing Ui3D assets.
**Spec:** User-approved `aquarium-shop-system.html` preview and the conversation on 2026-10-24.

## Constraints

- Text-only short Cá / Tiểu cảnh / Hồ tabs; contextual decoration filters and Tank / Thành hồ / Nền hồ.
- Search and icon-only sort on one row; no result count.
- Two-column cards; coin + numeric price without a pill; a large check for the active item.
- Preserve transactions, inventory, localization and existing uncommitted user work. Do not commit.

## Tasks

- [ ] Add meaningful query tests for grouped categories, moss/floating combined filter and tank catalog prices. Run before implementing, then implement and rerun.
- [ ] Update ShopScreen/ShopCard and ShopScreenBuilder for the approved layout and tank buy/use. Preserve existing OpenTab entry points and route the HUD tank shortcut to the tank tab.
- [ ] Recompile, rebuild Shop.prefab in the current MainAquarium scene with backups, and run query/economy tests. Check play-mode navigation, search, sort, item selection and tank actions without modifying the user's saved purchases.
- [ ] Capture actual Unity shop screens, inspect the diff and perform a fresh review.

## Review focus

Group switching clears searches; direct inventory links open the correct subcategory; the default tank is active when the save has no explicit tank; tank prices follow the loaded catalog; short/narrow screens retain a scrollable grid and accessible footer.

## Execution notes

Execute directly in the existing project because the running Unity Editor owns this checkout. User explicitly authorized applying the approved preview. Existing font/settings modifications predate this task and must be preserved.
