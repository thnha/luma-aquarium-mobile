using UnityEngine;

// One kind of fish: tail form + pattern + colours + proportions. Applied by BettaAppearance.
[CreateAssetMenu(menuName = "Lumaquarium/Betta Variant")]
public class BettaVariant : ScriptableObject
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    public string displayName = "Royal Blue Halfmoon";
    public BettaTailType tail;
    public BettaPattern pattern;

    [Header("Colours")]
    public Color primary = new Color(0.12f, 0.31f, 0.82f);
    public Color secondary = new Color(0.24f, 0.48f, 1f);
    [Tooltip("Iridescent sheen, Koi black spots, Dragon scales.")]
    public Color accent = new Color(0.62f, 0.83f, 1f);
    [Tooltip("Metallic sheen on the body. High reads as silver/chrome; low keeps a soft satin (milky, pearl).")]
    [Range(0f, 1f)] public float iridescence = 0.6f;
    [Tooltip("How much of the painted texture's scales and fin rays darken the colours. Low = smooth, milky colour.")]
    [Range(0f, 1f)] public float textureDetail = 0.6f;

    [Header("Proportions")]
    [Range(0.8f, 1.3f)] public float headScale = 1f;
    [Range(0.6f, 1.4f)] public float finLength = 1f;

    [Tooltip("Moves the Marble/Koi spots; same seed = same spots.")]
    public int seed;

    // Tail and pattern weights both add up to about 100 in the presets, so the
    // product / 10000 is roughly the chance of rolling this combination.
    public float RarityWeight => (tail ? tail.rarityWeight : 10f) * (pattern ? pattern.rarityWeight : 10f);

    public Rarity GetRarity()
    {
        float chance = RarityWeight / 10000f;
        return chance > 0.08f ? Rarity.Common : chance > 0.03f ? Rarity.Uncommon : chance > 0.008f ? Rarity.Rare : Rarity.Legendary;
    }
}
