// What a piece is for placement: stones carry stones, wood and coral; moss mounts on stone or wood;
// rooted plants mount on stone and may tuck right into the foot of a piece of wood.
public enum DecorRole { Rock, Wood, Coral, Plant, Moss, Floating, Wall }

public static class DecorPlacementRules
{
    public const float PlantInWoodOverlap = .9f;

    // Read from the prefab flags, with the shop category telling wood and coral from stone
    // (the starter driftwood is not tiltable, and corals carry no plants).
    public static DecorRole Role(AquariumDecorItem item, AquariumDecorCatalog catalog)
    {
        if (item.wallMounted) return DecorRole.Wall;
        if (item.floats) return DecorRole.Floating;
        if (item.attachable) return DecorRole.Moss;
        if (!item.Stacks) return DecorRole.Plant;
        var offer = catalog != null ? catalog.Find(item.kind) : null;
        if (offer != null)
            return offer.category == AquariumDecorCategory.Wood ? DecorRole.Wood :
                offer.category == AquariumDecorCategory.Coral ? DecorRole.Coral : DecorRole.Rock;
        return item.tiltable ? DecorRole.Wood : !item.supportsPlants ? DecorRole.Coral : DecorRole.Rock;
    }

    public static bool CanStandOn(DecorRole role, DecorRole host) =>
        host == DecorRole.Rock && (role == DecorRole.Rock || role == DecorRole.Wood || role == DecorRole.Coral);

    public static bool CanMountOn(DecorRole role, DecorRole host) =>
        role == DecorRole.Moss ? host == DecorRole.Rock || host == DecorRole.Wood :
        role == DecorRole.Plant && host == DecorRole.Rock;

    // Share of two footprints that may overlap on the sand.
    public static float Overlap(DecorRole role, DecorRole other, float fallback) =>
        role == DecorRole.Plant && other == DecorRole.Wood || role == DecorRole.Wood && other == DecorRole.Plant
            ? PlantInWoodOverlap : fallback;
}
