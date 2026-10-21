// Checks that the plain water background follows lighting and releases the camera on disable.
var d = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
if (d == null || !UnityEngine.Application.isPlaying) throw new System.Exception("Play MainAquarium first.");
var lighting = d.GetComponent<AquariumLighting>();
var camera = d.viewCamera;
var checks = new System.Collections.Generic.List<string>();
System.Action<bool, string> check = (ok, name) => { if (!ok) throw new System.Exception("FAIL: " + name); checks.Add(name); };
var original = camera.backgroundColor;
try
{
    lighting.ApplyImmediate(false);
    var day = camera.backgroundColor;
    check(day.g > day.r && day.b > day.r, "Day background has water tint");
    lighting.ApplyImmediate(true);
    var night = camera.backgroundColor;
    check(night.grayscale < day.grayscale * .6f, "Night darkens the water background");
    lighting.Toggle();
    lighting.SendMessage("Update");
    var transition = camera.backgroundColor;
    check(transition.grayscale > night.grayscale && transition.grayscale < day.grayscale, "Water background blends during lighting transition");
    lighting.enabled = false;
    camera.backgroundColor = UnityEngine.Color.magenta;
    lighting.enabled = true;
    lighting.ApplyImmediate(true);
    lighting.enabled = false;
    check(camera.backgroundColor == UnityEngine.Color.magenta, "Disabling lighting restores the previous camera background");
}
finally
{
    lighting.enabled = false;
    camera.backgroundColor = original;
    lighting.enabled = true;
    lighting.ApplyImmediate(false);
}
return new { passed = checks.Count, checks };
