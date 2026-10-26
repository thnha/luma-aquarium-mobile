using System.Linq;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ShopScreenVisualTests
{
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
