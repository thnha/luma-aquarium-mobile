// Run in Edit Mode with unity command eval_file --file docs/tests/betta-head-shape.cs.
// Checks visible skull proportions, neck continuity, variant switching and source preservation.
var checks = new System.Collections.Generic.List<string>();
System.Action<bool, string> check = (ok, name) => { if (!ok) throw new System.Exception("FAIL: " + name); checks.Add(name); };
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var variants = UnityEditor.AssetDatabase.FindAssets("t:BettaVariant", new[] { "Assets/_Project/Data/Betta/Variants" })
    .Select(id => UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>(UnityEditor.AssetDatabase.GUIDToAssetPath(id))).ToArray();
var clone = UnityEngine.Object.Instantiate(prefab);
clone.name = "BettaHeadShapeTest";
var baked = new UnityEngine.Mesh();
try
{
    var animator = clone.GetComponent<UnityEngine.Animator>();
    if (animator) animator.enabled = false;
    var clip = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Betta/Betta.fbx")
        .OfType<UnityEngine.AnimationClip>().First(c => c.name == "Idle");
    clip.SampleAnimation(clone, 0f);
    var appearance = clone.AddComponent<BettaAppearance>();
    var body = clone.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r => r.name == "BODY");
    var source = body.sharedMesh;
    var original = source.vertices;
    var boneNames = body.bones.Select(b => b.name).ToArray();
    var weights = source.boneWeights;
    float CoreWeight(UnityEngine.BoneWeight w)
    {
        int[] ids = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
        float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
        float sum = 0f;
        for (int j = 0; j < 4; j++)
            if (boneNames[ids[j]] == "Root" || boneNames[ids[j]] == "Head" || boneNames[ids[j]].StartsWith("Gill_") || boneNames[ids[j]].StartsWith("Jaw_")) sum += values[j];
        return sum;
    }
    var skull = Enumerable.Range(0, original.Length).Where(i => original[i].z > -.012f && original[i].z < .016f && CoreWeight(weights[i]) > .85f).ToArray();
    // Pure fin-root weights also occur on the belly. Only distant pectoral/pelvic
    // vertices are free fin edges; attachment vertices must follow the oval body.
    var fin = Enumerable.Range(0, original.Length).Where(i =>
        (boneNames[weights[i].boneIndex0].StartsWith("Pectoral_") || boneNames[weights[i].boneIndex0].StartsWith("Pelvic_"))
        && (Mathf.Abs(original[i].x) > .032f || original[i].y < -.09f)).ToArray();
    check(skull.Length > 20 && fin.Length > 20, "Real skull and fin samples exist");
    body.BakeMesh(baked);
    var neutral = baked.vertices;
    float neutralHeight = skull.Max(i => neutral[i].y) - skull.Min(i => neutral[i].y);
    var seams = original.Select((p, i) => new { key = UnityEngine.Vector3Int.RoundToInt(p * 100000f), i }).GroupBy(v => v.key).Where(g => g.Count() > 1).ToArray();
    foreach (var variant in variants)
    {
        appearance.Apply(variant);
        body.BakeMesh(baked);
        var positions = baked.vertices;
        float height = skull.Max(i => positions[i].y) - skull.Min(i => positions[i].y);
        float width = skull.Max(i => body.sharedMesh.vertices[i].x) - skull.Min(i => body.sharedMesh.vertices[i].x);
        float originalWidth = skull.Max(i => original[i].x) - skull.Min(i => original[i].x);
        check(width > originalWidth * 1.1f && width < originalWidth * 1.7f && height > neutralHeight * .7f && height < neutralHeight * 2f,
            variant.name + ": fuller cheek width with closed gills and bounded skull size");
        var shaped = body.sharedMesh.vertices;
        check(fin.All(i => (shaped[i] - original[i]).sqrMagnitude < 1e-12f), variant.name + ": fin geometry preserved");
        check(seams.All(g => { int first = g.First().i; return g.All(v => (shaped[v.i] - shaped[first]).sqrMagnitude < 1e-10f); }), variant.name + ": neck and UV seams remain joined");
        check(body.sharedMesh.vertexCount == source.vertexCount && body.sharedMesh.boneWeights.SequenceEqual(source.boneWeights), variant.name + ": skinning and topology preserved");
        check(body.sharedMesh.normals.All(n => !float.IsNaN(n.x) && n.sqrMagnitude > .99f && n.sqrMagnitude < 1.01f), variant.name + ": finite unit normals");
        var firstMesh = body.sharedMesh;
        var firstPositions = positions.ToArray();
        appearance.Apply(variants.First(v => v != variant));
        appearance.Apply(variant);
        body.BakeMesh(baked);
        check(body.sharedMesh == firstMesh && baked.vertices.Zip(firstPositions, (a, b) => (a - b).sqrMagnitude).All(d => d < 1e-10f), variant.name + ": switching fish never accumulates deformation");
    }
    var copy = UnityEngine.Object.Instantiate(clone);
    try
    {
        copy.GetComponent<BettaAppearance>().Apply(appearance.variant);
        var copyBody = copy.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r => r.name == "BODY");
        check(copyBody.sharedMesh == body.sharedMesh, "Cloning an equipped fish never applies the head shape twice");
        foreach (string name in new[] { "Eye_L", "Eye_R" })
        {
            var eye = clone.GetComponentsInChildren<UnityEngine.Transform>().First(t => t.name == name);
            var copyEye = copy.GetComponentsInChildren<UnityEngine.Transform>().First(t => t.name == name);
            check((eye.localPosition - copyEye.localPosition).sqrMagnitude < 1e-10f, "Cloned " + name + " stays aligned");
        }
    }
    finally { UnityEngine.Object.DestroyImmediate(copy); }
    check(source.vertices.SequenceEqual(original), "Authored source mesh remains unchanged");
    int headIndex = System.Array.FindIndex(body.bones, b => b.name == "Head");
    int neckIndex = System.Array.FindIndex(body.bones, b => b.name == "Spine_01");
    var head = source.bindposes[headIndex].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    var neck = source.bindposes[neckIndex].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    var similarHeads = new System.Collections.Generic.HashSet<UnityEngine.Mesh>();
    for (int i = 0; i < 20; i++) similarHeads.Add(BettaHeadShape.Get(source, body.bones, head, neck, 1f + i * .001f));
    check(similarHeads.Count <= 4, "Near-identical random proportions share a bounded set of meshes");
    return new { passed = checks.Count, variants = variants.Length, checks };
}
finally
{
    UnityEngine.Object.DestroyImmediate(baked);
    UnityEngine.Object.DestroyImmediate(clone);
}
