using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Fits a copy of the existing Betta skeleton to the Koi Nemo surface, as AquaFireBettaSkinning
// does for Aqua Fire. Only the fit table, head zone and body envelope differ: the Koi body sits
// higher in its bounds, its dorsal/anal fins are shorter and its caudal is a round fan.
// Coordinates are in the prefab root after the builder scales the fish to .75 long, head at +Z.
public static class KoiNemoBettaSkinning
{
    const string Root = "Assets/_Project/Art/KoiNemoBetta";
    struct Segment { public int a, b; public Vector3 start, end; }

    public static void Build(GameObject root, MeshRenderer renderer, GameObject model, Material material)
    {
        var source = renderer.GetComponent<MeshFilter>().sharedMesh;
        var surface = UnityEngine.Object.Instantiate(source);
        surface.name = "KoiNemoBetta_Skinned";
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
        Vector3[] vertices = source.vertices, normals = source.normals;
        Vector4[] tangents = source.tangents;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = toRoot.MultiplyPoint3x4(vertices[i]);
            if (normals.Length == vertices.Length) normals[i] = toRoot.MultiplyVector(normals[i]).normalized;
            if (tangents.Length == vertices.Length)
            {
                Vector3 direction = toRoot.MultiplyVector((Vector3)tangents[i]).normalized;
                tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
            }
        }
        surface.vertices = vertices;
        if (normals.Length == vertices.Length) surface.normals = normals;
        if (tangents.Length == vertices.Length) surface.tangents = tangents;

        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Betta.prefab");
        if (!template) throw new InvalidOperationException("Build the existing Betta prefab first.");
        var rig = UnityEngine.Object.Instantiate(template, root.transform, false);
        rig.name = "FittedBettaRig";
        rig.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        rig.transform.localScale = Vector3.one;
        var animator = rig.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController;
        var idle = controller.animationClips.First(c => c.name == "Idle");
        // The gill bones hang off Root in the shared rig, but here the gill covers are part
        // of the head: when Nibble dips the head they stayed behind and tore ~6 cm away.
        // Move them under Head and point the Koi's own clip copies at the new paths.
        var head = rig.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
        var gillRoots = rig.GetComponentsInChildren<Transform>().Where(t => t.name == "Gill_L_01" || t.name == "Gill_R_01").ToArray();
        var gillPaths = gillRoots.ToDictionary(t => AnimationUtility.CalculateTransformPath(t, rig.transform),
            t => AnimationUtility.CalculateTransformPath(head, rig.transform) + "/" + t.name);
        // Assigning a controller can reset local rotations. Do it before fitting and
        // calculating bindposes, so the serialized skeleton and skin share one pose.
        animator.runtimeAnimatorController = RotationClips(controller, gillPaths);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;
        // Idle does not key Gill_*_02, leaving the template's half-open default, while every
        // other clip holds it closed. Bind with the closed cover so Swim/Rest don't fold it in.
        var all = rig.GetComponentsInChildren<Transform>();
        var templatePose = all.Select(t => t.localRotation).ToArray();
        controller.animationClips.First(c => c.name == "Swim").SampleAnimation(rig, 0f);
        var closed = all.Where(t => t.name == "Gill_L_02" || t.name == "Gill_R_02").ToDictionary(t => t, t => t.localRotation);
        for (int i = 0; i < all.Length; i++) all[i].localRotation = templatePose[i];
        idle.SampleAnimation(rig, 0f);
        foreach (var gill in closed) gill.Key.localRotation = gill.Value;
        foreach (var gill in gillRoots) gill.SetParent(head, true);
        foreach (var rigRenderer in rig.GetComponentsInChildren<Renderer>())
            UnityEngine.Object.DestroyImmediate(rigRenderer.gameObject);
        Transform[] bones = rig.GetComponentsInChildren<Transform>().Where(t => t != rig.transform).ToArray();
        var fitted = FitPositions();
        // Parents are visited first; unweighted auxiliary bones keep their inherited offset.
        foreach (var bone in bones)
            if (fitted.TryGetValue(bone.name, out Vector3 position)) bone.position = root.transform.TransformPoint(position);

        var bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
        surface.bindposes = bindposes;
        surface.boneWeights = Weights(vertices, bones, root.transform);
        surface.RecalculateBounds();
        string meshPath = Root + "/KoiNemoBetta_Skinned.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (saved) { EditorUtility.CopySerialized(surface, saved); UnityEngine.Object.DestroyImmediate(surface); }
        else { AssetDatabase.CreateAsset(surface, meshPath); saved = surface; }
        EditorUtility.SetDirty(saved);
        var skin = root.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = saved;
        skin.sharedMaterial = material;
        skin.bones = bones;
        skin.rootBone = rig.transform;
        var bounds = saved.bounds;
        bounds.Expand(.3f);
        skin.localBounds = bounds;
        skin.quality = SkinQuality.Bone4;

        var swim = root.AddComponent<BettaSwimMotion>();
        swim.skeletonRoot = rig.transform;
        // Broad-bodied like Aqua Fire: same longer heading delay and restrained tail kick.
        swim.segmentFollowRate = 3.2f;
        swim.maxJointBend = 11f;
        swim.bodyIdleAmplitude = .7f;
        swim.bodySwimAmplitude = 5f;
        swim.strokeSwimAmplitude = 3f;
        swim.turnTailKick = .025f;
        swim.maxTailKick = 4f;
        swim.headRecoil = .3f;
        // The default 1.2° barely moves this short gill cover; breathe visibly instead.
        swim.gillAmplitude = 4f;
        var fins = root.AddComponent<BettaFinPhysics>();
        fins.skeletonRoot = rig.transform;
        fins.skippedChains = BettaFinPhysics.CrownChainRoots;
        var reactions = root.AddComponent<BettaReactions>();
        reactions.animator = animator;
        var binding = root.AddComponent<BettaRiggedModel>();
        binding.animator = animator;
        binding.reactions = reactions;
        UnityEngine.Object.DestroyImmediate(model);
    }

    static Dictionary<string, Vector3> FitPositions()
    {
        var p = new Dictionary<string, Vector3>
        {
            ["Root"] = new Vector3(0,.04f,.17f), ["Belly"] = new Vector3(0,-.01f,.17f),
            ["Head"] = new Vector3(0,.045f,.28f),
            ["Spine_01"] = new Vector3(0,.045f,.19f), ["Spine_02"] = new Vector3(0,.045f,.10f),
            ["Spine_03"] = new Vector3(0,.035f,.01f), ["Spine_04"] = new Vector3(0,.02f,-.08f),
            ["Tail_Base"] = new Vector3(0,.005f,-.14f),
            ["Pelvic_Base"] = new Vector3(0,-.03f,.20f)
        };
        string[] tails = { "Tail_Top_", "Tail_UpMid_", "Tail_", "Tail_LowMid_", "Tail_Low_", "Tail_Bot_" };
        float[] angles = { 70,36,-5,-36,-62,-80 }, lengths = { .16f,.17f,.195f,.205f,.21f,.17f };
        for (int i = 0; i < tails.Length; i++)
        {
            float radians = angles[i] * Mathf.Deg2Rad;
            Vector3 origin = p["Tail_Base"];
            Ray(tails[i], 5, origin, origin + new Vector3(0, Mathf.Sin(radians), -Mathf.Cos(radians)) * lengths[i]);
        }
        Ray("Dorsal_A_",4,new Vector3(0,.09f,.02f),new Vector3(0,.22f,-.02f));
        Ray("Dorsal_B_",4,new Vector3(0,.06f,-.10f),new Vector3(0,.21f,-.15f));
        Ray("Anal_A_",4,new Vector3(0,-.03f,.10f),new Vector3(0,-.16f,.04f));
        Ray("Anal_B_",4,new Vector3(0,-.03f,-.06f),new Vector3(0,-.21f,-.05f));
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            // The Koi's pelvics sweep forward under the head instead of trailing back.
            Ray("Pelvic_"+suffix+"_",3,new Vector3(side*.03f,-.035f,.20f),new Vector3(side*.06f,-.10f,.31f));
            p["Pectoral_"+suffix+"_01"] = new Vector3(side*.05f,.03f,.18f);
            p["Pectoral_"+suffix+"_02"] = new Vector3(side*.075f,.02f,.15f);
            p["Pectoral_"+suffix+"_03A"] = new Vector3(side*.10f,-.03f,.10f);
            p["Pectoral_"+suffix+"_03B"] = new Vector3(side*.10f,.06f,.10f);
        }
        // The table above is drawn on x=0; move every bone onto the fish's curved midline.
        foreach (var name in p.Keys.ToArray()) p[name] += new Vector3(MidX(p[name].z), 0, 0);
        // Face bones, measured directly (the head is turned slightly toward -X, so no mirroring).
        // Eyes pivot at the eyeball centre, one radius inside the surface; gill covers hinge
        // behind the eye and swing their rear edge; the lower jaw hinges at the mouth corner.
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            Vector3 eye = EyeSurface(side);
            p["Eye_"+suffix] = eye - new Vector3(side*EyeRadius, 0, 0);
            float mid = MidX(GillHingeZ);
            p["Gill_"+suffix+"_01"] = new Vector3(mid + side*.045f, .035f, GillHingeZ);
            p["Gill_"+suffix+"_02"] = new Vector3(mid + side*.05f, .035f, GillHingeZ);
            p["Gill_"+suffix+"_03"] = new Vector3(mid + side*.07f, .035f, .225f);
        }
        p["Jaw_Lower"] = JawHinge;
        p["Jaw_Upper"] = JawHinge + new Vector3(0, .006f, 0);
        return p;
        void Ray(string prefix, int count, Vector3 start, Vector3 end)
        {
            for (int i = 0; i < count; i++) p[prefix+(i+1).ToString("00")] = Vector3.Lerp(start,end,i/(float)(count-1));
        }
    }

    static BoneWeight[] Weights(Vector3[] vertices, Transform[] bones, Transform root)
    {
        var indices = bones.Select((b,i)=>(b.name,i)).ToDictionary(x=>x.name,x=>x.i);
        var segments = new List<Segment>();
        foreach (var bone in bones)
        {
            string name = bone.name;
            bool fin = name.StartsWith("Tail_") && name!="Tail_Base" || name.StartsWith("Dorsal_")
                || name.StartsWith("Anal_") || name.StartsWith("Pelvic_") && name!="Pelvic_Base" || name.StartsWith("Pectoral_");
            if (!fin || name.Contains("Crown")) continue;
            foreach (Transform child in bone)
                if (!child.name.Contains("Crown")) segments.Add(new Segment
                {
                    a=indices[name],b=indices[child.name],
                    start=root.InverseTransformPoint(bone.position),end=root.InverseTransformPoint(child.position)
                });
        }
        string[] spine = { "Tail_Base","Spine_04","Spine_03","Spine_02","Spine_01","Head" };
        float[] spineZ = spine.Select(n=>root.InverseTransformPoint(bones[indices[n]].position).z).ToArray();
        var result = new BoneWeight[vertices.Length];
        var weights = new float[bones.Length];
        var order = new int[bones.Length];
        for (int v = 0; v < vertices.Length; v++)
        {
            Array.Clear(weights,0,weights.Length);
            Vector3 point = vertices[v];
            // The eyes, lips and cheeks belong to one rigid head. A narrow body
            // envelope must never reclassify a protruding facial vertex as a fin.
            // Fade behind the cheek into the spine; keep the hanging pelvic fins free.
            float head = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.17f,.22f,point.z))
                * Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.06f,-.035f,point.y));
            weights[indices["Head"]] = head;
            float body = (1-head)*BodyShare(point);
            float finShare = 1-head-body;
            int right = 1;
            while (right < spine.Length-1 && point.z > spineZ[right]) right++;
            float t = Mathf.InverseLerp(spineZ[right-1],spineZ[right],point.z);
            weights[indices[spine[right-1]]] += body*(1-t);
            weights[indices[spine[right]]] += body*t;
            if (finShare > 0f)
            {
                int first=-1,second=-1;
                float d1=float.MaxValue,d2=float.MaxValue,t1=0,t2=0;
                for (int i=0;i<segments.Count;i++)
                {
                    var segment=segments[i];var axis=segment.end-segment.start;
                    float along=Mathf.Clamp01(Vector3.Dot(point-segment.start,axis)/Mathf.Max(axis.sqrMagnitude,1e-8f));
                    float distance=(point-Vector3.Lerp(segment.start,segment.end,along)).sqrMagnitude;
                    if(distance<d1){second=first;d2=d1;t2=t1;first=i;d1=distance;t1=along;}
                    else if(distance<d2){second=i;d2=distance;t2=along;}
                }
                float a=1f/Mathf.Pow(d1+.0001f,2),b=1f/Mathf.Pow(d2+.0001f,2);
                Add(segments[first],t1,finShare*a/(a+b));
                Add(segments[second],t2,finShare*b/(a+b));
            }
            // Face parts take their share from whatever owned the vertex (head, mostly).
            float eyeL=EyeShare(point,-1), eyeR=EyeShare(point,1), gillL=GillShare(point,-1), gillR=GillShare(point,1), jaw=JawShare(point);
            float face=Mathf.Min(1f,eyeL+eyeR+gillL+gillR+jaw);
            if (face > 0f)
            {
                for (int i=0;i<weights.Length;i++) weights[i]*=1f-face;
                float norm=face/Mathf.Max(eyeL+eyeR+gillL+gillR+jaw,1e-6f);
                weights[indices["Eye_L"]]+=eyeL*norm; weights[indices["Eye_R"]]+=eyeR*norm;
                weights[indices["Gill_L_03"]]+=gillL*norm; weights[indices["Gill_R_03"]]+=gillR*norm;
                weights[indices["Jaw_Lower"]]+=jaw*norm;
            }
            for(int i=0;i<order.Length;i++)order[i]=i;
            for(int i=0;i<4;i++)for(int j=i+1;j<order.Length;j++)
                if(weights[order[j]]>weights[order[i]])(order[i],order[j])=(order[j],order[i]);
            float sum=weights[order[0]]+weights[order[1]]+weights[order[2]]+weights[order[3]];
            result[v]=new BoneWeight
            {
                boneIndex0=order[0],weight0=weights[order[0]]/sum,boneIndex1=order[1],weight1=weights[order[1]]/sum,
                boneIndex2=order[2],weight2=weights[order[2]]/sum,boneIndex3=order[3],weight3=weights[order[3]]/sum
            };
        }
        return result;
        void Add(Segment segment,float along,float value){weights[segment.a]+=value*(1-along);weights[segment.b]+=value*along;}
    }

    // A smooth body envelope protects the opaque torso while allowing the surrounding
    // membranes and protruding pectorals/pelvics to follow their own ray chains.
    // The Meshy Koi is not straight: its body bends toward -X from mid-body to the tail
    // (midline ≈ -.045 at the peduncle, dorsal fin at ≈ -.025). Centred on x=0, the left
    // half of the back and peduncle fell outside the envelope and peeled off with the dorsal.
    static readonly float[] MidZ = { -.38f,-.34f,-.30f,-.22f,-.14f,-.10f,-.06f,-.02f,.06f,.22f,.375f };
    static readonly float[] MidOffset = { .01f,-.007f,-.027f,-.045f,-.044f,-.037f,-.026f,-.016f,-.002f,-.008f,-.017f };
    static float MidX(float z)
    {
        int i=1;while(i<MidZ.Length-1 && z>MidZ[i])i++;
        return Mathf.Lerp(MidOffset[i-1],MidOffset[i],Mathf.InverseLerp(MidZ[i-1],MidZ[i],z));
    }

    // Widths are the measured body half-width / 1.15 (the radial at which the envelope starts fading).
    static readonly float[] Z = { -.15f,-.08f,0,.10f,.20f,.28f,.345f };
    static readonly float[] Width = { .025f,.038f,.048f,.05f,.065f,.055f,.012f };
    // Koi's back rises to y≈.14 ahead of the dorsal; a lower envelope gave that skin to the
    // pectoral and dorsal chains, so the back flapped with them. Belly edge stays at y≈-.045.
    static readonly float[] Height = { .033f,.05f,.065f,.085f,.076f,.059f,.015f };
    static readonly float[] Center = { .003f,.018f,.03f,.0525f,.052f,.043f,.056f };
    static float BodyShare(Vector3 point)
    {
        int i=1;while(i<Z.Length-1 && point.z>Z[i])i++;
        float t=Mathf.InverseLerp(Z[i-1],Z[i],point.z);
        float x=(point.x-MidX(point.z))/Mathf.Lerp(Width[i-1],Width[i],t);
        float y=(point.y-Mathf.Lerp(Center[i-1],Center[i],t))/Mathf.Lerp(Height[i-1],Height[i],t);
        // Protect the full body cross section, including its upper side corners.
        // An ellipse here misclassifies those opaque shoulder vertices as dorsal fin.
        float radial=Mathf.Max(Mathf.Abs(x),Mathf.Abs(y));
        return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.15f,1.65f,radial)))
            *Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.17f,-.12f,point.z));
    }

    // Face landmarks, read from close-ups with a 1 cm grid. Without weights on these bones
    // the eyes, gill covers and mouth stayed welded to the rigid head.
    const float EyeRadius = .02f, GillHingeZ = .27f;
    static readonly Vector3 JawHinge = new Vector3(-.013f,.058f,.343f);
    static Vector3 EyeSurface(int side) => side < 0 ? new Vector3(-.070f,.064f,.308f) : new Vector3(.041f,.066f,.309f);

    // The eye dome follows Eye_L/R fully, blending into the socket rim.
    static float EyeShare(Vector3 point, int side)
    {
        if (side*(point.x-MidX(point.z)) <= 0f) return 0f;
        return 1f-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.013f,.022f,Vector3.Distance(point,EyeSurface(side))));
    }

    // Operculum: the side of the head from the gill slit (z≈.19) forward to the hinge behind the eye.
    static float GillShare(Vector3 point, int side)
    {
        float lateral = side*(point.x-MidX(point.z));
        return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,.045f,lateral))
            * Mathf.SmoothStep(0,1,Mathf.InverseLerp(.178f,.19f,point.z))
            * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.225f,.285f,point.z)))
            * Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.035f,-.02f,point.y))
            * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.095f,point.y)));
    }

    // Lower lip and chin, below the mouth line, ahead of the mouth corner.
    static float JawShare(Vector3 point)
    {
        return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.33f,.345f,point.z))
            * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.052f,.062f,point.y)))
            * Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.005f,.01f,point.y));
    }

    // The shared rig's flare swings the cover ~60°; this fused Meshy cover creases at its hinge
    // that far. Keep the timing but open it only part way from its closed (first-key) pose.
    const float GillFlareShare = .6f;
    static void SoftenGillFlare(AnimationClip clip, EditorCurveBinding[] bindings)
    {
        var curves = new AnimationCurve[4];
        foreach (var b in bindings) curves["xyzw".IndexOf(b.propertyName[b.propertyName.Length-1])] = AnimationUtility.GetEditorCurve(clip,b);
        Quaternion At(float time) => new Quaternion(curves[0].Evaluate(time),curves[1].Evaluate(time),curves[2].Evaluate(time),curves[3].Evaluate(time));
        Quaternion closed = At(curves[0].keys[0].time);
        var times = curves.SelectMany(c=>c.keys.Select(k=>k.time)).Distinct().OrderBy(t=>t).ToArray();
        var soft = new AnimationCurve[4];
        for (int i=0;i<4;i++) soft[i]=new AnimationCurve();
        foreach (float time in times)
        {
            var q = Quaternion.Slerp(closed, At(time), GillFlareShare);
            soft[0].AddKey(time,q.x); soft[1].AddKey(time,q.y); soft[2].AddKey(time,q.z); soft[3].AddKey(time,q.w);
        }
        foreach (var b in bindings) AnimationUtility.SetEditorCurve(clip,b,soft["xyzw".IndexOf(b.propertyName[b.propertyName.Length-1])]);
    }

    static RuntimeAnimatorController RotationClips(RuntimeAnimatorController controller, Dictionary<string,string> movedPaths)
    {
        string path=Root+"/KoiNemoBetta.overrideController";
        var overrides=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if(!overrides){overrides=new AnimatorOverrideController(controller);AssetDatabase.CreateAsset(overrides,path);}
        overrides.runtimeAnimatorController=controller;
        foreach(var clip in controller.animationClips.Distinct())
        {
            string clipPath=Root+"/KoiNemo_"+clip.name+".anim";
            var rotation=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if(!rotation){rotation=new AnimationClip();AssetDatabase.CreateAsset(rotation,clipPath);}
            EditorUtility.CopySerialized(clip,rotation);
            rotation.name="KoiNemo_"+clip.name;
            foreach(var binding in AnimationUtility.GetCurveBindings(rotation))
                if(binding.propertyName.StartsWith("m_LocalPosition") || binding.propertyName.StartsWith("m_LocalScale"))
                    AnimationUtility.SetEditorCurve(rotation,binding,null);
            foreach(var binding in AnimationUtility.GetCurveBindings(rotation))
                foreach(var moved in movedPaths)
                    // The moved bone's own keys are constant rotations relative to its old parent;
                    // drop them so it keeps its bind rotation under Head. Children move with it.
                    if(binding.path==moved.Key) AnimationUtility.SetEditorCurve(rotation,binding,null);
                    else if(binding.path.StartsWith(moved.Key+"/"))
                    {
                        var curve=AnimationUtility.GetEditorCurve(rotation,binding);
                        AnimationUtility.SetEditorCurve(rotation,binding,null);
                        var target=binding; target.path=moved.Value+binding.path.Substring(moved.Key.Length);
                        AnimationUtility.SetEditorCurve(rotation,target,curve);
                    }
            // Clips hold the gill covers and eyes at constant poses from the shared rig (Idle/Nibble
            // half-open gills, Nibble eyes rolled ~40°). Without those keys they stay at the bind
            // pose; animated keys (Idle breathing and glances, Flare opening) are kept.
            string[] holdBind={"/Gill_L_02","/Gill_R_02","/Gill_L_03","/Gill_R_03","/Eye_L","/Eye_R"};
            foreach(var gill in AnimationUtility.GetCurveBindings(rotation).Where(b=>holdBind.Any(b.path.EndsWith)).GroupBy(b=>b.path))
                if(gill.All(b=>{var keys=AnimationUtility.GetEditorCurve(rotation,b).keys;return keys.Max(k=>k.value)-keys.Min(k=>k.value)<.001f;}))
                    foreach(var b in gill) AnimationUtility.SetEditorCurve(rotation,b,null);
                else if(gill.Key.EndsWith("_02") && gill.Count()==4) SoftenGillFlare(rotation,gill.ToArray());
            EditorUtility.SetDirty(rotation);
            overrides[clip]=rotation;
        }
        EditorUtility.SetDirty(overrides);
        return overrides;
    }
}
