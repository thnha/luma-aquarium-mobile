using UnityEngine;

// Colour pattern. Kind is the index read by BettaVariant.hlsl (_VariantPattern), keep them in sync.
[CreateAssetMenu(menuName = "Lumaquarium/Betta Pattern")]
public class BettaPattern : ScriptableObject
{
    public enum Kind { Solid = 0, Bicolor = 1, Butterfly = 2, Marble = 3, Koi = 4, Dragon = 5 }

    public string displayName = "Solid";
    public Kind kind;
    [Tooltip("Marble/Koi: noise above this shows the primary colour (higher = fewer spots).")]
    [Range(0f, 1f)] public float spotThreshold = 0.58f;
    [Tooltip("Marble/Koi: noise below this shows the accent colour (lower = fewer spots).")]
    [Range(0f, 1f)] public float accentThreshold = 0.3f;
    [Tooltip("Butterfly: where along the fin (0 root, 1 edge) the secondary band starts.")]
    [Range(0f, 1f)] public float butterflyBand = 0.55f;
    [Tooltip("Butterfly: half-width of the blend into the band (small = hard line, large = soft fade like Pearl).")]
    [Range(0.01f, 0.5f)] public float butterflyBandSoftness = 0.04f;

    [Header("Spawning")]
    public float rarityWeight = 10f;
}
