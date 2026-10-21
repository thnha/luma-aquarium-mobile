using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Art/Betta/Betta.controller, the shared state machine (Idle/Swim/Nibble/Rest/Flare) driven by
// BettaFishAI and BettaReactions. Its clips come from Art/Betta/Betta.fbx, the template rig exported from
// betta-splendens/source/Betta_Splendens_M3.blend; every pipeline fish plays it through an override
// controller with its own same-named clips (PipelineFishImporter).
// Menu: Lumaquarium > Fish > Rebuild Betta Animator Controller (safe to re-run).
public static class BettaControllerBuilder
{
    const string ArtDir = "Assets/_Project/Art/Betta";
    const string FbxPath = ArtDir + "/Betta.fbx";
    public const string ControllerPath = ArtDir + "/Betta.controller";

    [MenuItem("Lumaquarium/Fish/Rebuild Betta Animator Controller")]
    public static AnimatorController Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode before rebuilding the Betta controller.");
        var controller = Controller();
        AssetDatabase.SaveAssetIfDirty(controller);
        Debug.Log("[Lumaquarium] Built " + ControllerPath);
        return controller;
    }

    [MenuItem("Lumaquarium/Fish/Rebuild Betta Animator Controller", true)]
    static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Speed / Nibble match BettaFishAI. Clips not exported yet fall back to Idle.
    static AnimatorController Controller()
    {
        // Reuse the asset (keeps its GUID and the override controllers pointing at it) and rebuild its contents.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        while (controller.parameters.Length > 0) controller.RemoveParameter(0);
        var oldStates = controller.layers[0].stateMachine;
        foreach (var t in oldStates.anyStateTransitions) oldStates.RemoveAnyStateTransition(t);
        foreach (var s in oldStates.states) oldStates.RemoveState(s.state);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Nibble", AnimatorControllerParameterType.Trigger);

        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip Clip(string n) => clips.FirstOrDefault(c => c.name == n) ?? clips.FirstOrDefault(c => c.name == "Idle");

        var sm = controller.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Clip("Idle");
        var swim = sm.AddState("Swim"); swim.motion = Clip("Swim");
        sm.defaultState = idle;

        var toSwim = idle.AddTransition(swim); toSwim.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed"); toSwim.hasExitTime = false; toSwim.duration = 0.25f;
        var toIdle = swim.AddTransition(idle); toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed"); toIdle.hasExitTime = false; toIdle.duration = 0.25f;

        var nibbleClip = clips.FirstOrDefault(c => c.name == "Nibble");
        if (nibbleClip)
        {
            var nibble = sm.AddState("Nibble"); nibble.motion = nibbleClip;
            var toNibble = sm.AddAnyStateTransition(nibble); toNibble.AddCondition(AnimatorConditionMode.If, 0, "Nibble"); toNibble.duration = 0.1f;
            var back = nibble.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
        }

        // Rest loops while BettaReactions holds Resting (AI State.Rest).
        controller.AddParameter("Resting", AnimatorControllerParameterType.Bool);
        var restClip = clips.FirstOrDefault(c => c.name == "Rest");
        if (restClip)
        {
            var rest = sm.AddState("Rest"); rest.motion = restClip;
            var toRest = idle.AddTransition(rest); toRest.AddCondition(AnimatorConditionMode.If, 0, "Resting"); toRest.hasExitTime = false; toRest.duration = 0.6f;
            var fromRest = rest.AddTransition(idle); fromRest.AddCondition(AnimatorConditionMode.IfNot, 0, "Resting"); fromRest.hasExitTime = false; fromRest.duration = 0.4f;
        }

        // Flare plays once from any state when the fish is tapped (BettaReactions).
        controller.AddParameter("Flare", AnimatorControllerParameterType.Trigger);
        var flareClip = clips.FirstOrDefault(c => c.name == "Flare");
        if (flareClip)
        {
            var flare = sm.AddState("Flare"); flare.motion = flareClip;
            var toFlare = sm.AddAnyStateTransition(flare); toFlare.AddCondition(AnimatorConditionMode.If, 0, "Flare"); toFlare.duration = 0.1f; toFlare.canTransitionToSelf = false;
            var back = flare.AddTransition(idle); back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.2f;
        }
        return controller;
    }
}
