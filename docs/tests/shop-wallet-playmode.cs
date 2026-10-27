// Run with unity command eval_file in Play Mode. No purchase is made.

var shop = UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.ShopScreen>(FindObjectsInactive.Include);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
float hudAlpha = shop.aquarium.hud.alpha;
shop.Open();
Check(shop.IsOpen && !shop.WalletOpen && !shop.details.activeSelf, "shop opens on the catalog");
Check(shop.balanceLabel.transform.parent != shop.topUpButton.transform, "balance separate from top-up");
Check(((RectTransform)shop.scroll.transform).offsetMin.y < 60, "catalog uses bottom area");
foreach (Lumaquarium.UI.ShopGroup group in Enum.GetValues(typeof(Lumaquarium.UI.ShopGroup))) {
    shop.SelectGroup(group);
    Check(shop.scroll.content.GetComponentsInChildren<Lumaquarium.UI.ShopCard>(false).Length > 0, "group has cards: " + group);
}
shop.SelectGroup(Lumaquarium.UI.ShopGroup.Fish);
shop.search.text = "koi";
var card = shop.scroll.content.GetComponentsInChildren<Lumaquarium.UI.ShopCard>(false).First();
card.button.onClick.Invoke();
Check(shop.details.activeSelf, "card opens details");
string itemTitle = shop.title.text;
shop.scroll.verticalNormalizedPosition = .4f;
float scrollPosition = shop.scroll.verticalNormalizedPosition;
var gridOffset = ((RectTransform)shop.scroll.transform).offsetMax;
shop.OpenWallet();
Check(shop.WalletOpen, "top-up opens wallet");
Check(shop.coinPackArea.gameObject.activeInHierarchy && !shop.rewardedAdArea.gameObject.activeInHierarchy, "wallet starts on coin packs");
Check(shop.search.text == "koi" && shop.title.text == itemTitle, "wallet preserves query and selection");
Check(Mathf.Abs(shop.scroll.verticalNormalizedPosition - scrollPosition) < .001f, "wallet preserves scroll");
shop.rewardCoinsTab.onClick.Invoke();
Check(shop.rewardedAdArea.gameObject.activeInHierarchy && !shop.coinPackArea.gameObject.activeInHierarchy, "free rewards tab");
shop.buyCoinsTab.onClick.Invoke();
Check(shop.coinPackArea.gameObject.activeInHierarchy, "return to purchase tab");
Check(((RectTransform)shop.scroll.transform).offsetMax == gridOffset, "wallet never shifts catalog");
int children = shop.coinPackArea.GetComponentsInChildren<Transform>(true).Length;
shop.GoBack();
Check(!shop.WalletOpen && shop.details.activeSelf && shop.IsOpen, "Back dismisses wallet before details");
shop.OpenWallet(); shop.CloseWallet();
Check(shop.coinPackArea.GetComponentsInChildren<Transform>(true).Length == children, "reopening never duplicates packs");
var expensive = new Lumaquarium.UI.ShopEntry { id = "wallet-test-not-a-purchase", price = shop.Balance + 1 };
typeof(Lumaquarium.UI.ShopScreen).GetMethod("RequestPurchase", flags).Invoke(shop, new object[] { expensive });
Check(shop.WalletOpen && !shop.confirmation.activeSelf, "insufficient coins open wallet without purchasing");
shop.GoBack();
Check(shop.title.text == itemTitle && shop.details.activeSelf, "return to selected item");
shop.GoBack();
Check(!shop.details.activeSelf && shop.IsOpen, "Back dismisses details before shop");
shop.OpenWallet();
shop.Close();
Check(!shop.IsOpen && !shop.walletContent.activeSelf, "closing shop closes wallet");
Check(Mathf.Approximately(shop.aquarium.hud.alpha, hudAlpha), "closing restores HUD");
shop.Open();
return new { passed = checks, balance = shop.Balance, packs = shop.coinPackArea.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length, adsAvailable = shop.rewardedAdArea.Find("Unavailable").gameObject.activeSelf == false, catalogReady = shop.IsOpen };
