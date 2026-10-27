using System.Linq;
using Lumaquarium;
using Lumaquarium.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AquariumWallCatalogTests
{
    public static readonly string[] NewWallIds =
    {
        "wall_basalt", "wall_limestone", "wall_sandstone", "wall_amazon_roots", "wall_frosted_aqua",
        "wall_matte_black", "wall_dragon_stone", "wall_pearl_marble", "wall_turquoise_mosaic", "wall_sunken_relic"
    };

    [Test]
    public void AuthoredWallsAreAvailableAlongsideTheOriginalSix()
    {
        var catalog = SurfaceCatalogSheet.Load();
        Assert.IsNotNull(catalog);
        CollectionAssert.IsSubsetOf(NewWallIds, catalog.walls.Select(w => w.id).ToArray());
        Assert.AreEqual(catalog.walls.Length, catalog.walls.Select(w => w.id).Distinct().Count());
        CollectionAssert.IsSubsetOf(new[] { "wall_river", "wall_slate", "wall_brick", "wall_planks", "wall_gold", "wall_jade" },
            catalog.walls.Select(w => w.id).ToArray());
        Assert.AreEqual("wall_river", catalog.walls[0].id);
        Assert.IsNull(catalog.walls[0].albedo);
        Assert.GreaterOrEqual(catalog.walls[0].price, 0, "The price sheet may override even the original wall.");
    }

    [Test]
    public void AuthoredWallTexturesAndPortraitsAreReadyForMobile()
    {
        var catalog = SurfaceCatalogSheet.Load();
        foreach (string id in NewWallIds)
        {
            var wall = catalog.walls.Single(w => w.id == id);
            Assert.IsNotNull(wall.albedo, id);
            Assert.IsNotNull(wall.portrait, id);
            Assert.AreEqual(new Vector2(256, 256), wall.portrait.rect.size, id);
            Assert.Greater(wall.tiling, 0, id);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(wall.albedo));
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode, id);
            Assert.IsTrue(importer.mipmapEnabled, id);
            Assert.IsFalse(importer.isReadable, id);
            Assert.LessOrEqual(importer.maxTextureSize, 1024, id);
        }
    }

    [Test]
    public void AuthoredWallsHaveShopCardsAndEnglishCopy()
    {
        var catalog = SurfaceCatalogSheet.Load();
        var entries = ShopEntry.Build(null, null, catalog);
        foreach (string id in NewWallIds)
        {
            var wall = catalog.walls.Single(w => w.id == id);
            var entry = entries.Single(e => e.id == id);
            Assert.AreEqual(ShopKind.Wall, entry.kind);
            Assert.AreEqual(ShopTab.Walls, entry.tab);
            Assert.AreSame(wall.portrait, entry.portrait);
            Assert.AreEqual(wall.price, entry.price);
            Assert.IsTrue(Loc.HasEnglish(wall.displayName), wall.displayName);
            Assert.IsTrue(Loc.HasEnglish(wall.description), wall.description);
        }
    }
}
