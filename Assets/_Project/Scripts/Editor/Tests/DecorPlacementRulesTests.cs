using NUnit.Framework;
using UnityEngine;

public sealed class DecorPlacementRulesTests
{
    [TestCase(DecorRole.Rock, DecorRole.Rock, true)]
    [TestCase(DecorRole.Wood, DecorRole.Rock, true)]
    [TestCase(DecorRole.Coral, DecorRole.Rock, true)]
    [TestCase(DecorRole.Rock, DecorRole.Wood, false)]
    [TestCase(DecorRole.Wood, DecorRole.Wood, false)]
    [TestCase(DecorRole.Coral, DecorRole.Wood, false)]
    [TestCase(DecorRole.Rock, DecorRole.Coral, false)]
    [TestCase(DecorRole.Coral, DecorRole.Coral, false)]
    [TestCase(DecorRole.Plant, DecorRole.Rock, false)]
    public void StandOn(DecorRole role, DecorRole host, bool expected) =>
        Assert.AreEqual(expected, DecorPlacementRules.CanStandOn(role, host));

    [TestCase(DecorRole.Moss, DecorRole.Rock, true)]
    [TestCase(DecorRole.Moss, DecorRole.Wood, true)]
    [TestCase(DecorRole.Moss, DecorRole.Coral, false)]
    [TestCase(DecorRole.Plant, DecorRole.Rock, true)]
    [TestCase(DecorRole.Plant, DecorRole.Wood, false)]
    [TestCase(DecorRole.Plant, DecorRole.Coral, false)]
    [TestCase(DecorRole.Rock, DecorRole.Rock, false)]
    [TestCase(DecorRole.Floating, DecorRole.Wood, false)]
    public void MountOn(DecorRole role, DecorRole host, bool expected) =>
        Assert.AreEqual(expected, DecorPlacementRules.CanMountOn(role, host));

    [Test]
    public void PlantsTuckIntoWoodOnly()
    {
        Assert.AreEqual(.9f, DecorPlacementRules.Overlap(DecorRole.Plant, DecorRole.Wood, .35f));
        Assert.AreEqual(.9f, DecorPlacementRules.Overlap(DecorRole.Wood, DecorRole.Plant, .35f));
        Assert.AreEqual(.35f, DecorPlacementRules.Overlap(DecorRole.Plant, DecorRole.Rock, .35f));
        Assert.AreEqual(.35f, DecorPlacementRules.Overlap(DecorRole.Rock, DecorRole.Rock, .35f));
    }

    [Test]
    public void RoleFromFlags()
    {
        var go = new GameObject("item");
        try
        {
            var item = go.AddComponent<AquariumDecorItem>();
            item.wallMounted = true;
            Assert.AreEqual(DecorRole.Wall, DecorPlacementRules.Role(item, null));
            item.wallMounted = false; item.floats = true;
            Assert.AreEqual(DecorRole.Floating, DecorPlacementRules.Role(item, null));
            item.floats = false; item.attachable = true;
            Assert.AreEqual(DecorRole.Moss, DecorPlacementRules.Role(item, null));
            item.attachable = false; item.foliage = new GameObject("leaves").transform;
            item.foliage.SetParent(go.transform);
            Assert.AreEqual(DecorRole.Plant, DecorPlacementRules.Role(item, null));
            item.foliage = null;
            item.tiltable = true; item.supportsPlants = true;
            Assert.AreEqual(DecorRole.Wood, DecorPlacementRules.Role(item, null));
            item.tiltable = false; item.supportsPlants = false;
            Assert.AreEqual(DecorRole.Coral, DecorPlacementRules.Role(item, null));
            item.supportsPlants = true;
            Assert.AreEqual(DecorRole.Rock, DecorPlacementRules.Role(item, null));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void RoleFromCatalogCategory()
    {
        var go = new GameObject("item");
        var catalog = ScriptableObject.CreateInstance<AquariumDecorCatalog>();
        try
        {
            var item = go.AddComponent<AquariumDecorItem>();
            item.kind = 50; item.supportsPlants = true;
            var offer = new AquariumDecorOffer { id = "x", prefab = item, category = AquariumDecorCategory.Wood };
            catalog.offers = new[] { offer };
            // The starter driftwood is not tiltable; the category still makes it wood.
            Assert.AreEqual(DecorRole.Wood, DecorPlacementRules.Role(item, catalog));
            offer.category = AquariumDecorCategory.Coral;
            Assert.AreEqual(DecorRole.Coral, DecorPlacementRules.Role(item, catalog));
            offer.category = AquariumDecorCategory.Rocks;
            Assert.AreEqual(DecorRole.Rock, DecorPlacementRules.Role(item, catalog));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(catalog); }
    }

    [TestCase(AquariumDecorCategory.Plants, AquariumDecorCategory.Plants)]
    [TestCase(AquariumDecorCategory.Wood, AquariumDecorCategory.Wood)]
    [TestCase(AquariumDecorCategory.Coral, AquariumDecorCategory.Coral)]
    [TestCase(AquariumDecorCategory.Moss, AquariumDecorCategory.Plants)]
    [TestCase(AquariumDecorCategory.Floating, AquariumDecorCategory.Plants)]
    public void InventorySheetFoldsMossAndFloatingIntoPlants(AquariumDecorCategory category, AquariumDecorCategory group) =>
        Assert.AreEqual(group, AquariumDecorCatalog.InventoryGroup(category));

    [TestCase(AquariumDecorCategory.Plants, true)]
    [TestCase(AquariumDecorCategory.Moss, true)]
    [TestCase(AquariumDecorCategory.Floating, true)]
    [TestCase(AquariumDecorCategory.Wood, false)]
    [TestCase(AquariumDecorCategory.Rocks, false)]
    [TestCase(AquariumDecorCategory.Coral, false)]
    public void PlantsMossAndFloatingAreSoft(AquariumDecorCategory category, bool soft) =>
        Assert.AreEqual(soft, AquariumDecorCatalog.IsPlant(category));
}
