# Shop with separate wallet — 2026-10-27

Applied the approved preview to Shop.prefab and MainAquarium. Coin balance and gold Nạp xu button are separate. IAP packs and rewarded ads now belong to the wallet sheet; item details open on tap. Search, selection and scrolling survive wallet visits. Insufficient coins open the wallet without issuing a purchase.

Changed: ShopScreen.cs, ShopScreenBuilder.cs, CoinPackShopPanel.cs, RewardedAdShopPanel.cs, Shop.prefab, MainAquarium.unity, Localization/en.txt; visual and localization tests. No backend, catalog prices or saved-wallet changes.

Validation: 148/148 EditMode tests; 21/21 PlayMode interaction checks. Verified advancing frames and visually inspected catalog, wallet and item detail in Game View. Independent code review found no actionable defects. Results are in editmode-tests.json and playmode-verification.json.

Re-run in Edit Mode:

    unity command run_tests -- --mode editor --async_tests true
    unity command test_status

After entering Play Mode and loading MainAquarium:

    unity command eval_file docs/tests/shop-wallet-playmode.cs

Screenshots: catalog-9x16.png, wallet-9x16.png, details-9x16.png. Store prices in the wallet image come from Unity Fake Store. Real store payment and live rewarded ads on a physical device were not exercised. Ads are unavailable with the local Editor backend. Tooling screenshot captures can select an old Editor render target; final images were checked against the visible Game View.
