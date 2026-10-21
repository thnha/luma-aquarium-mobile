using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Soft focus at the fish's depth, so the carpet against the front glass and the far back blur a
// little. Bokeh blur is costly: it runs only on devices with enough memory and pauses while decor
// is being placed.
public sealed class AquariumDepthOfField : MonoBehaviour
{
    public int minimumMemoryMB = 6000;
    public float aperture = 4, focalLength = 42;
    private AquariumDecoration decoration;
    private Volume volume;
    private VolumeProfile profile;
    private DepthOfField focus;

    private void Start()
    {
        decoration = GetComponent<AquariumDecoration>();
        if (decoration == null || decoration.viewCamera == null || SystemInfo.systemMemorySize < minimumMemoryMB ||
            SystemInfo.graphicsShaderLevel < 45) { enabled = false; return; }
        decoration.viewCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        focus = profile.Add<DepthOfField>(true);
        focus.mode.Override(DepthOfFieldMode.Bokeh);
        focus.aperture.Override(aperture);
        focus.focalLength.Override(focalLength);
        focus.focusDistance.Override(3.5f);
        var go = new GameObject("AquariumDepthOfField");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        volume.sharedProfile = profile;
    }

    private void LateUpdate()
    {
        if (volume == null) return;
        volume.weight = decoration.Editing ? 0 : 1;
        var view = decoration.viewCamera.transform;
        var fish = decoration.screen != null && decoration.screen.fish != null ? decoration.screen.fish.transform : null;
        float target = fish != null ? Vector3.Dot(fish.position - view.position, view.forward) : 3.5f;
        focus.focusDistance.value = Mathf.Lerp(focus.focusDistance.value, Mathf.Max(.5f, target), Time.deltaTime * 2);
    }

    private void OnDestroy()
    {
        if (profile != null) Destroy(profile);
    }
}
