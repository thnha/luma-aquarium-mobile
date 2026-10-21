using UnityEngine;

// Colour pattern. Kind is the index read by BettaVariant.hlsl (_VariantPattern), keep them in sync.
[CreateAssetMenu(menuName = "Lumaquarium/Betta Pattern")]
public class BettaPattern : ScriptableObject
{
    public enum Kind { Solid = 0, Bicolor = 1, Butterfly = 2, Marble = 3, Koi = 4, Dragon = 5, Galaxy = 6, KoiGalaxy = 7 }

    public string displayName = "Solid";
    public Kind kind;
    [Tooltip("Marble/Koi: noise above this shows the primary colour (higher = fewer spots). Galaxy, Koi Galaxy: above this shows the secondary (dark patches).")]
    [Range(0f, 1f)] public float spotThreshold = 0.58f;
    [Tooltip("Marble/Koi: noise below this shows the accent colour (lower = fewer spots).")]
    [Range(0f, 1f)] public float accentThreshold = 0.3f;
    [Tooltip("Butterfly: where along the fin (0 root, 1 edge) the secondary band starts. Galaxy: where the fin edge colour starts. Koi Galaxy: where the dark rim starts.")]
    [Range(0f, 1f)] public float butterflyBand = 0.55f;
    [Tooltip("Butterfly: half-width of the blend into the band (small = hard line, large = soft fade like Pearl).")]
    [Range(0.01f, 0.5f)] public float butterflyBandSoftness = 0.04f;

    [Header("Galaxy, Koi Galaxy")]
    [Tooltip("Galaxy: speckles per UV unit (higher = finer dots). Koi Galaxy: glitter dots; dark scales are twice as large.")]
    public float speckleScale = 220f;
    [Tooltip("Galaxy: share of grid cells holding a speckle (clusters, fins and patches vary it).")]
    [Range(0f, 1f)] public float speckleDensity = 0.35f;

    [Header("Spawning")]
    public float rarityWeight = 10f;
}
