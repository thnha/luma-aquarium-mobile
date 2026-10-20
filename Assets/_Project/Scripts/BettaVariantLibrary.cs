using UnityEngine;

// Every variant the game can spawn, plus the parts used to roll new random fish.
[CreateAssetMenu(menuName = "Lumaquarium/Betta Variant Library")]
public class BettaVariantLibrary : ScriptableObject
{
    [Tooltip("Hand-made variants, picked by rarity weight.")]
    public BettaVariant[] variants = new BettaVariant[0];

    [Header("Random generation")]
    public BettaTailType[] tails = new BettaTailType[0];
    public BettaPattern[] patterns = new BettaPattern[0];
    public Color[] palette = new Color[0];

    public BettaVariant PickPreset(System.Random rng = null)
    {
        rng ??= new System.Random();
        return Weighted(variants, v => v ? v.RarityWeight : 0f, rng);
    }

    // Builds a new variant in memory from a weighted tail + pattern and palette colours.
    public BettaVariant Generate(int seed)
    {
        var rng = new System.Random(seed);
        var v = CreateInstance<BettaVariant>();
        v.tail = Weighted(tails, t => t ? t.rarityWeight : 0f, rng);
        v.pattern = Weighted(patterns, p => p ? p.rarityWeight : 0f, rng);
        Color Pick() => palette.Length > 0 ? palette[rng.Next(palette.Length)] : Color.HSVToRGB((float)rng.NextDouble(), 0.8f, 0.8f);
        v.primary = Pick();
        v.secondary = Pick();
        v.accent = Color.Lerp(Pick(), Color.white, 0.5f);
        var kind = v.pattern ? v.pattern.kind : BettaPattern.Kind.Solid;
        if (kind == BettaPattern.Kind.Solid) v.secondary = Color.Lerp(v.primary, Color.white, 0.2f);
        if (kind == BettaPattern.Kind.Marble || kind == BettaPattern.Kind.Koi) v.secondary = new Color(0.95f, 0.93f, 0.89f);
        if (kind == BettaPattern.Kind.Koi) v.accent = new Color(0.09f, 0.09f, 0.09f);
        v.iridescence = Mathf.Lerp(0.1f, 1f, (float)rng.NextDouble());
        v.headScale = Mathf.Lerp(0.92f, 1.12f, (float)rng.NextDouble());
        v.finLength = Mathf.Lerp(0.85f, 1.15f, (float)rng.NextDouble());
        v.seed = rng.Next(1000);
        v.displayName = (v.pattern ? v.pattern.displayName : "Solid") + " " + (v.tail ? v.tail.displayName : "Betta");
        v.name = v.displayName;
        return v;
    }

    static T Weighted<T>(T[] items, System.Func<T, float> weight, System.Random rng) where T : Object
    {
        float total = 0f;
        foreach (var item in items) total += Mathf.Max(0f, weight(item));
        if (total <= 0f) return items.Length > 0 ? items[0] : null;
        float roll = (float)rng.NextDouble() * total;
        foreach (var item in items)
        {
            roll -= Mathf.Max(0f, weight(item));
            if (roll <= 0f) return item;
        }
        return items[items.Length - 1];
    }
}
