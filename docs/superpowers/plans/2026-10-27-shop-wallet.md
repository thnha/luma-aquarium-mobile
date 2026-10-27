# Shop Wallet Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan inline. No commits are authorized.

**Goal:** Apply the approved separate-wallet shop preview to the Unity game.
**Architecture:** Extend ShopScreen and its existing builder with serialized wallet/detail sheet controls. Reparent existing coin-pack and rewarded-ad presentation into the wallet; keep backend services unchanged.
**Tech Stack:** Unity 6000.3, uGUI, TextMeshPro, existing Unity IAP and AdMob adapters.
**Spec:** mobile/docs/superpowers/specs/2026-10-27-shop-wallet-design.md

## Global Constraints
- Preserve catalog data, user saves, existing real purchase/reward validation and project assets.
- No new dependencies, environment changes or commits; edit the current non-Git workspace.

## Review Focus
- Wallet opens without resetting a selected item, search or scroll.
- Unsupported IAP/ads produce a visible message, never a fake transaction.
- Confirmation, wallet, details and shop close in the expected Back order.
- Ads/IAP feedback and effects remain visible in the wallet; stale callbacks cannot buy hidden products.
- Layout remains usable at portrait aspect ratios and in both supported languages.

### Task 1: Wallet and item sheets
- [x] Extend ShopScreenVisualTests with separate top-up/balance, default-hidden wallet/detail sheets and expanded-grid checks; run and observe failures.
- [x] Add serialized controls and sheet state/actions in ShopScreen; update ShopScreenBuilder header, sheets and catalog layout.
- [x] Move CoinPackShopPanel and RewardedAdShopPanel controls into the corresponding wallet area; preserve service/retry behavior and add unavailable states.
- [x] Add translations, rebuild Shop.prefab in the connected Editor, run relevant editor tests.

### Task 2: Runtime verification
- [x] Enter Play Mode, prove frames advance, exercise catalog tabs, details, wallet tabs, insufficient funds route and Back order without real payments.
- [x] Capture catalog, wallet and item-detail screenshots; inspect layout and console, repair issues.
- [x] Run shop query/economy/service/inventory and localization checks; record results and limitations.
