using UnityEngine;

// One tail form (Plakat, Halfmoon, ...). Shares the Betta rig: until per-form fin meshes exist,
// the shape comes from fin-root bone scale and caudal spread applied by BettaAppearance.
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

    [Header("Fin meshes")]
    [Tooltip("SkinnedMeshRenderers named Fin_* to show for this form; the other Fin_* renderers are hidden. Leave empty while the fins are still part of BODY.")]
    public string[] finMeshes = new string[0];

    [Header("Fin physics")]
    public BettaFinPhysics.FinSettings caudal = new BettaFinPhysics.FinSettings();
    public BettaFinPhysics.FinSettings dorsalAnal = new BettaFinPhysics.FinSettings();

    [Header("Spawning")]
    public float rarityWeight = 10f;
}
