using System.Linq;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShopScreenVisualTests
{
    private static ShopScreen Screen() => AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/_Project/UI/Prefabs/Shop.prefab").GetComponent<ShopScreen>();

    [Test]
    public void BalanceAndTopUpAreSeparateControls()
    {
        var screen = Screen();
        var topUp = screen.content.transform.Find("SafeArea/Header/TopUp");
        Assert.NotNull(topUp, "top-up needs its own labeled control");
        Assert.False(screen.balanceLabel.transform.IsChildOf(topUp));
        Assert.NotNull(topUp.GetComponent<UnityEngine.UI.Button>());
        Assert.IsNull(topUp.Find("WalletIcon"), "top-up is a text-only primary button");
        Assert.AreEqual("Nạp xu", topUp.GetComponentInChildren<TMPro.TMP_Text>().text);
    }

    [Test]
    public void ShopBackgroundCoversScreenOutsideSafeArea()
    {
        var screen = Screen();
        var paper = screen.content.transform.Find("Paper") as RectTransform;
        Assert.NotNull(paper, "background must cover the entire screen, including notch and home-indicator areas");
        Assert.AreEqual(Vector2.zero, paper.anchorMin);
        Assert.AreEqual(Vector2.one, paper.anchorMax);
        Assert.AreEqual(Vector2.zero, paper.offsetMin);
        Assert.AreEqual(Vector2.zero, paper.offsetMax);
    }

    [Test]
    public void TitleBalanceAndTopUpShareOneHeaderRow()
    {
        var screen = Screen();
        var header = screen.content.transform.Find("SafeArea/Header");
        var title = header.Find("Title") as RectTransform;
        var wallet = screen.balanceLabel.transform.parent as RectTransform;
        var topUp = screen.topUpButton.transform as RectTransform;
        Assert.AreEqual(header, wallet.parent);
        Assert.AreEqual(header, topUp.parent);
        Assert.AreEqual(.5f, wallet.anchorMin.y);
        Assert.AreEqual(.5f, topUp.anchorMin.y);
        Assert.AreEqual(wallet.anchoredPosition.y, topUp.anchoredPosition.y);
        Assert.AreEqual(0, title.offsetMin.y);
        Assert.AreEqual(0, title.offsetMax.y);
    }

    [Test]
    public void WalletAndDetailsAreDismissibleAndHiddenInPrefab()
    {
        var screen = Screen();
        var wallet = screen.content.transform.Find("WalletSheet");
        Assert.NotNull(wallet);
        Assert.False(wallet.gameObject.activeSelf);
        Assert.False(screen.details.activeSelf);
        Assert.NotNull(wallet.Find("SafeArea/Panel/Close"));
        Assert.NotNull(wallet.Find("SafeArea/Panel/CoinPacks"));
        Assert.NotNull(wallet.Find("SafeArea/Panel/RewardedAds"));
        Assert.NotNull(screen.details.transform.Find("SafeArea/Panel/Close"));
    }

    [Test]
    public void CatalogDoesNotReserveSpaceForClosedDetails()
    {
        var screen = Screen();
        var grid = (RectTransform)screen.scroll.transform;
        Assert.LessOrEqual(grid.offsetMin.y, 60, "closed details should not consume the bottom of the catalog");
    }

    [Test]
    public void SortButtonUsesCoinAndSeparateDirectionImages()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Prefabs/Shop.prefab");
        Assert.NotNull(prefab);
        var screen = prefab.GetComponentInChildren<ShopScreen>(true);
        Assert.NotNull(screen);
        Assert.NotNull(typeof(ShopScreen).GetField("sortDownIcon"), "the runtime needs a second direction image");

        var names = screen.sortButton.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "PriceSortCoin", "Ascending", "Descending" }, names);
    }
}
