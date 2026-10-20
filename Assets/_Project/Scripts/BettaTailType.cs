using UnityEngine;

// One tail form (Plakat, Halfmoon, ...). Shares the Betta rig: the shape comes from fin-root bone scale
// and caudal spread applied by BettaAppearance, and forms with their own fin outline (Crowntail, Double Tail)
// switch to their Fin_* meshes in Betta.fbx; Rosetail folds the fin edges (BettaFinRuffle).
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
    [Tooltip("Fin_* meshes in Betta.fbx shown for this form (caudal, dorsal, anal). Empty = the Halfmoon ones.")]
    public string[] finMeshes = new string[0];
    [Tooltip("Base colour + alpha used instead of the material's _BaseMap (same UVs, fin edges cut in alpha). Empty = the material's.")]
    public Texture2D baseMap;
    [Tooltip("Root bones of fin chains that sit in a transparent cut and are left to the animation (Double Tail: Tail_01).")]
    public string[] skippedFinChains = new string[0];
    [Tooltip("Rosetail: folds the outer edge of the shown Fin_* meshes in waves. Depth 0 = flat fins.")]
    public BettaFinRuffle.Settings ruffle = new BettaFinRuffle.Settings();

    [Header("Fin physics")]
    public BettaFinPhysics.FinSettings caudal = new BettaFinPhysics.FinSettings();
    public BettaFinPhysics.FinSettings dorsalAnal = new BettaFinPhysics.FinSettings();

    [Header("Spawning")]
    public float rarityWeight = 10f;
}
