using UnityEngine;

// One fish in the shop: its model (a pipeline fish) + pattern + colours. Applied by BettaAppearance.
// PipelineFishImporter writes these from each Art/Fish/<Name>/<Name>.asset.
[CreateAssetMenu(menuName = "Lumaquarium/Betta Variant")]
public class BettaVariant : ScriptableObject
{
    public string displayName = "Plakat Betta";
    [Tooltip("The fish's own rigged model (Art/Fish/<Name>/<Name>.prefab).")]
    public GameObject modelPrefab;
    public BettaPattern pattern;

    [Header("Colours")]
    public Color primary = new Color(0.12f, 0.31f, 0.82f);
    public Color secondary = new Color(0.24f, 0.48f, 1f);
    [Tooltip("Iridescent sheen, Koi black spots, Dragon scales.")]
    public Color accent = new Color(0.62f, 0.83f, 1f);
    [Tooltip("Galaxy pattern: fin edge colour.")]
    public Color finEdge = new Color(1f, 0.82f, 0.2f);
    [Tooltip("Metallic sheen on the body. High reads as silver/chrome; low keeps a soft satin (milky, pearl).")]
    [Range(0f, 1f)] public float iridescence = 0.6f;
    [Tooltip("How much of the painted texture's scales and fin rays darken the colours. Low = smooth, milky colour.")]
    [Range(0f, 1f)] public float textureDetail = 0.6f;
    [Tooltip("Silvery shine of the painted metallic map on body and fins. 1 = as painted; 0 = satin, non-metal (milky white).")]
    [Range(0f, 1f)] public float silverSheen = 1f;

    [Tooltip("Moves the Marble/Koi spots; same seed = same spots.")]
    public int seed;
}
