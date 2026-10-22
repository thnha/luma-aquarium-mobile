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
    [Tooltip("Galaxy: yellow patches on the body and the lower fins' rim. Koi Galaxy: fin colour.")]
    public Color finEdge = new Color(1f, 0.82f, 0.2f);
    [Tooltip("Metallic sheen on the body. High reads as silver/chrome; low keeps a soft satin (milky, pearl).")]
    [Range(0f, 1f)] public float iridescence = 0.6f;
    [Tooltip("How much of the painted texture's scales and fin rays darken the colours. Low = smooth, milky colour.")]
    [Range(0f, 1f)] public float textureDetail = 0.6f;
    [Tooltip("Silvery shine of the painted metallic map on body and fins. 1 = as painted; 0 = satin, non-metal (milky white).")]
    [Range(0f, 1f)] public float silverSheen = 1f;

    [Header("Surface")]
    [Tooltip("Scales every smoothness. Below 1 softens glazed, porcelain-like highlights into satin skin.")]
    [Range(0f, 1.5f)] public float gloss = 1f;
    [Tooltip("Koi Galaxy: dark scales and dark fin rims turn deep black, non-metal and highly polished (lacquer black). 0 = off.")]
    [Range(0f, 1f)] public float blackGloss;
    [Tooltip("Koi Galaxy: share of the dark scales that are white instead, scattered one by one. 0 = none.")]
    [Range(0f, 0.5f)] public float whiteScales;
    [Tooltip("Pigment is thickest on the head and gills and thins out towards the tail: darker, deeper head; clearer, brighter tail. 0 = even.")]
    [Range(0f, 1f)] public float pigmentDepth;
    [Tooltip("How much clearer and brighter the fin edges are (new growth, thin pigment). 0 = off.")]
    [Range(0f, 2f)] public float finEdgeClear;
    [Tooltip("Depth of the grooves between fin rays. 1 = as baked, lower = a flatter, thinner-looking membrane.")]
    [Range(0f, 1f)] public float finRelief = 1f;
    [Tooltip("Light from behind shining through the fins, more towards the edge. 0 = off.")]
    [Range(0f, 2f)] public float finTranslucency = 0.8f;
    [Tooltip("Height of the body scales. 1 = as authored, lower = thinner scales lying flat on the skin.")]
    [Range(0f, 1f)] public float scaleRelief = 1f;
    [Tooltip("Dark fin parts (rims, dark roots) deeper towards black, apart from the body's black. 0 = as the body.")]
    [Range(0f, 1f)] public float finBlack;
    [Tooltip("Brightness of the other fin colours. 1 = as painted, above 1 = lighter.")]
    [Range(0.5f, 2f)] public float finBrightness = 1f;
    [Tooltip("Saturation of the whole lit fish. Underwater light makes it read dull; 1 = as lit.")]
    [Range(0.5f, 2f)] public float vibrance = 1f;
    [Tooltip("Brightness of the whole lit fish. 1 = as lit.")]
    [Range(0.5f, 2f)] public float brightness = 1f;

    [Header("Clear fin rims")]
    [Tooltip("Opacity left where the fin rims are clearest. 1 = opaque fins (off).")]
    [Range(0f, 1f)] public float finRimAlpha = 1f;
    [Tooltip("Rim of the dorsal, anal, pectoral and pelvic fins: where it starts and where it is clearest (fin root 0 -> edge 1).")]
    public Vector2 finRim = new Vector2(0.9f, 1f);
    [Tooltip("Rim of the caudal fin, same units. Its mask peaks lower than the other fins'.")]
    public Vector2 caudalRim = new Vector2(0.62f, 0.8f);
    [Tooltip("Where along the fish (snout 0 -> tail tip 1) the caudal fin begins.")]
    [Range(0f, 1f)] public float caudalStart = 0.55f;

    [Tooltip("Moves the Marble/Koi spots; same seed = same spots.")]
    public int seed;
}
