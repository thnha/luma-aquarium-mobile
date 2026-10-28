# Settings UI Implementation Plan

**Goal:** Apply the approved Settings preview with rounded group frames, labels interrupting the top border, and readable typography.
**Architecture:** Keep the current uGUI rows and persistent button actions. Update the Settings-only builder and saved MainAquarium scene; use a small non-interactive Graphic for rounded frames whose top gap follows the localized legend.
**Tech Stack:** Unity 6.3, uGUI, TextMeshPro.
**Spec:** Approved preview `exec-94ad21f1-2c5f-454b-99e5-77f393b344da.png` in this chat.

## Constraints
- Preserve existing user changes, row names, controller references, localization and button actions.
- No new dependencies or commits.
- Group labels 44, item titles 40, descriptions 36, buttons 42 in 1080-wide canvas units.
- Limit changes to Settings, its frame component and verification.

## Task 1
- [x] Inspect scene and existing builder; preserve a scene copy before applying changes.
- [x] Implement a rounded outline with a localized legend gap and Settings-only typography/spacing.
- [x] Apply through the existing Group Settings Rows builder in the open Editor.
- [x] Verify compilation, English/Vietnamese legend gaps, text bounds, persistent button actions and repeated rebuilds.
- [x] Capture the real UI at portrait phone sizes and inspect it.

## Review focus
English and Vietnamese strings; long signed-in account captions; no row/button overlap; inactive panel initialization; repeated builder calls preserve actions. Visual styling will be checked in the Editor rather than with tests that duplicate numeric layout constants.

Verification: Unity recompile: 0 errors, 0 warnings. LocalizationTests: 6/6 passed. Play mode English/Vietnamese text bounds, outline meshes, six persistent button actions, signed-in caption, and repeated builder calls checked. Captured iPhone 12 Pro and iPhone SE sizes. Final review corrected the panel anchor to preserve its 390-unit bottom offset. No commits.
