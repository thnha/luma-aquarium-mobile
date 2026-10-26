# Shop Price Sort Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the shop's paired navigation arrows with the approved coin-and-direction price sort icon.

**Architecture:** The existing shop builder creates the icon hierarchy and serializes its image references into `ShopScreen`. Runtime refresh code toggles and positions the two direction images for Default, Low-to-High, and High-to-Low without changing filtering or sort behavior.

**Tech Stack:** Unity 6, uGUI, TextMeshPro, existing Ui3D sprites and editor builder.

**Spec:** `docs/superpowers/specs/2026-10-25-shop-price-sort-icon-design.md`

## Global Constraints

- Preserve the existing 116 × 116 sort button and three-state click cycle.
- Reuse the system coin and utility arrow sprites.
- Do not add dependencies or change unrelated shop layout.
- Do not commit; the user did not request a commit.

## Review Focus

- Default state visibly shows both arrow directions.
- Low-to-high visibly shows only the up direction.
- High-to-low visibly shows only the down direction.
- The coin remains visible in every state.
- Existing serialized shop references remain valid after prefab regeneration.

---

### Task 1: Build and drive the price sort glyph

**Files:**
- Modify: `Assets/_Project/Scripts/UI/ShopScreen.cs`
- Modify: `Assets/_Project/Scripts/Editor/ShopScreenBuilder.cs`
- Generated: `Assets/_Project/UI/Prefabs/Shop.prefab`

**Interfaces:**
- Consumes: `ShopSort` and the existing `Ui3D` sprite helpers.
- Produces: serialized `sortIcon` and `sortDownIcon` image references with runtime state updates.

- [ ] **Step 1:** Add a second serialized direction image and centralize the three-state visual update in `ShopScreen.Refresh()`.
- [ ] **Step 2:** Replace the builder's paired full-size navigation arrows with a system coin plus two compact direction images.
- [ ] **Step 3:** Recompile scripts and regenerate the shop prefab through the live Unity Editor.
- [ ] **Step 4:** Run the existing Shop editor tests and verify all pass.
- [ ] **Step 5:** Open the shop in Play Mode, cycle all three sort states, and capture the approved default state at 1080 × 1920.

