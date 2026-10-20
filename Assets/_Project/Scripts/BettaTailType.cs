using UnityEngine;

// One tail form (Plakat, Halfmoon, ...). Shares the Betta rig and mesh: the shape comes from fin-root
// bone scale and caudal spread applied by BettaAppearance, and fin outlines that differ (Crowntail
// spikes, Double Tail slit) are cut into the alpha of a per-form base map (Tools/betta_fin_cuts.py).
[CreateAssetMenu(menuName = "Lumaquarium/Betta Tail Type")]
public class BettaTailType : ScriptableObject
{
    public string displayName = "Halfmoon";

    [Header("Shape (multipliers on the shared rig)")]
    public float caudalLength = 1f;
    public float dorsalLength = 1f;
    public float analLength = 1f;
    public float pelvicLength = 1f;
    [Tooltip("Degrees the upper/lower caudal rays open (+) or close (-) relative to Betta.fbx, which is a Halfmoon.")]
    public float caudalSpread;
    [Tooltip("Degrees the whole caudal fin hangs down (Veiltail).")]
    public float caudalDroop;

    [Header("Fin outline")]
    [Tooltip("Base colour + alpha used instead of the material's _BaseMap (same UVs, fin edges cut in alpha). Empty = the material's.")]
    public Texture2D baseMap;
    [Tooltip("Root bones of fin chains that sit in a transparent cut and are left to the animation (Double Tail: Tail_01).")]
    public string[] skippedFinChains = new string[0];

    [Header("Fin physics")]
    public BettaFinPhysics.FinSettings caudal = new BettaFinPhysics.FinSettings();
    public BettaFinPhysics.FinSettings dorsalAnal = new BettaFinPhysics.FinSettings();

    [Header("Spawning")]
    public float rarityWeight = 10f;
}
