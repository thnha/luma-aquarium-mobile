// Run in Play Mode. The test model is separate from the player's aquarium fish.
if (!UnityEngine.Application.isPlaying) throw new System.Exception("Play MainAquarium first.");
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var clone = UnityEngine.Object.Instantiate(prefab);
clone.name = "HeadAnimationTest";
clone.transform.position = new UnityEngine.Vector3(1000, 1000, 1000);
var baked = new UnityEngine.Mesh();
var checks = new System.Collections.Generic.List<string>();
System.Action<bool, string> check = (ok, name) => { if (!ok) throw new System.Exception("FAIL: " + name); checks.Add(name); };
try
{
    clone.GetComponent<UnityEngine.Animator>().enabled = false;
    var appearance = clone.AddComponent<BettaAppearance>();
    var transforms = clone.GetComponentsInChildren<UnityEngine.Transform>();
    var eyes = transforms.Where(t => t.name == "Eye_L" || t.name == "Eye_R").ToArray();
    var body = clone.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r => r.name == "BODY");
    var eyeRenderer = clone.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r => r.name == "EYES");
    var eyeMesh = eyeRenderer.sharedMesh;
    var source = BettaHeadShape.Source(body.sharedMesh);
    var seams = source.vertices.Select((p, i) => new { key = UnityEngine.Vector3Int.RoundToInt(p * 100000f), i }).GroupBy(v => v.key).Where(g => g.Count() > 1).ToArray();
    var clips = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Betta/Betta.fbx")
        .OfType<UnityEngine.AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
    var variants = UnityEditor.AssetDatabase.FindAssets("t:BettaVariant", new[] { "Assets/_Project/Data/Betta/Variants" })
        .Select(id => UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>(UnityEditor.AssetDatabase.GUIDToAssetPath(id))).ToArray();
    var lateUpdate = typeof(BettaAppearance).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    foreach (var variant in variants)
    {
        appearance.Apply(variant);
        var anchors = eyes.Select(e => e.localPosition).ToArray();
        foreach (var clip in clips)
        {
            clip.SampleAnimation(clone, clip.length * .37f);
            lateUpdate.Invoke(appearance, null);
            check(eyes.Select((eye, i) => (eye.localPosition - anchors[i]).sqrMagnitude < 1e-10f
                && (eye.localScale - UnityEngine.Vector3.one * variant.eyeScale).sqrMagnitude < 1e-10f).All(ok => ok), variant.name + "/" + clip.name + ": eye anchors survive animation without stretching");
            check(eyeRenderer.sharedMesh == eyeMesh, variant.name + "/" + clip.name + ": original round eye mesh retained");
            body.BakeMesh(baked);
            var positions = baked.vertices;
            check(positions.All(p => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z))
                && seams.All(g => { int first = g.First().i; return g.All(v => (positions[v.i] - positions[first]).sqrMagnitude < 1e-10f); }), variant.name + "/" + clip.name + ": animated skull and neck remain finite and joined");
        }
    }
    return new { passed = checks.Count, variants = variants.Length, clips = clips.Length, checks };
}
finally
{
    UnityEngine.Object.DestroyImmediate(baked);
    UnityEngine.Object.DestroyImmediate(clone);
}
