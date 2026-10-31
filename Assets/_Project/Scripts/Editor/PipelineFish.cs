using UnityEngine;

// One fish made by the Blender pipeline (betta-splendens/pipeline/build_fish.py). Lives in
// Art/Fish/<Name>/ next to <Name>.fbx, <Name>_VariantMask.png and <Name>_BaseColor.png;
// PipelineFishImporter builds the material, prefab, variant and shop offer from it.
[CreateAssetMenu(menuName = "Lumaquarium/Pipeline Fish")]
public class PipelineFish : ScriptableObject
{
    public string displayName = "Plakat Betta";
    [Header("Shop")]
    public bool inShop = true;
    public string shopId = "plakat";
    public string description = "";
    public int price;

    [Header("Model")]
    [Tooltip("Scale against the template rig in Art/Betta/Betta.fbx (1 = same body length). Koi Nemo is about 2.4.")]
    public float size = 2f;

    [Header("Colours (BettaAppearance paints them through the variant mask)")]
    public BettaPattern pattern;
    public Color primary = new Color(0.12f, 0.31f, 0.82f);
    public Color secondary = new Color(0.95f, 0.95f, 0.95f);
    public Color accent = new Color(0.09f, 0.09f, 0.09f);
    [Tooltip("Galaxy: yellow patches on the body and the lower fins' rim. Koi Galaxy: fin colour.")]
    public Color finEdge = new Color(1f, 0.82f, 0.2f);
    [Range(0f, 1f)] public float iridescence = 0.4f;
    public int seed;

    [Header("Motion (BettaSwimMotion)")]
    [Tooltip("Short-finned, stocky fish (Plakat) need less body bend than the long-finned shared Betta.")]
    public float bodySwimAmplitude = 3f;
    public float strokeSwimAmplitude = 8f;
    [Tooltip("BettaFinPhysics.caudalForwardHold: caudal rays lying forward under the dorsal don't fan forward (EmeraldRed).")]
    [Range(0f, 1f)] public float caudalForwardHold;
    [Tooltip("BettaFinPhysics.caudalEdgeCalm: the caudal's top and bottom edges swing less side to side (EmeraldRed).")]
    [Range(0f, 1f)] public float caudalEdgeCalm;
}
