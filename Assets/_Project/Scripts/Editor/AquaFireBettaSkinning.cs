using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Fits a copy of the existing Betta skeleton to the original Aqua Fire surface.
// The generated skin changes weights/bindposes only; source geometry, UVs and PNGs stay intact.
public static class AquaFireBettaSkinning
{
    const string Root = "Assets/_Project/Art/AquaFireBetta";
    struct Segment { public int a, b; public Vector3 start, end; }

    public static void Build(GameObject root, GameObject model, Material material)
    {
        var source = model.GetComponent<MeshFilter>().sharedMesh;
        var surface = UnityEngine.Object.Instantiate(source);
        surface.name = "AquaFireBetta_Skinned";
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix * model.transform.localToWorldMatrix;
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
        // Assigning a controller can reset local rotations. Do it before fitting and
        // calculating bindposes, so the serialized skeleton and skin share one pose.
        animator.runtimeAnimatorController = RotationClips(controller);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;
        idle.SampleAnimation(rig, 0f);
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>())
            UnityEngine.Object.DestroyImmediate(renderer.gameObject);
        Transform[] bones = rig.GetComponentsInChildren<Transform>().Where(t => t != rig.transform).ToArray();
        var fitted = FitPositions();
        // Parents are visited first; unweighted auxiliary bones keep their inherited offset.
        foreach (var bone in bones)
            if (fitted.TryGetValue(bone.name, out Vector3 position)) bone.position = root.transform.TransformPoint(position);

        var bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
        surface.bindposes = bindposes;
        surface.boneWeights = Weights(vertices, bones, root.transform);
        surface.RecalculateBounds();
        string meshPath = Root + "/AquaFireBetta_Skinned.asset";
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
        // This broad-bodied fish needs a longer heading delay to form a visible C
        // through a turn. Keep the extra tail kick restrained as the torso now follows.
        swim.segmentFollowRate = 3.2f;
        swim.maxJointBend = 11f;
        swim.bodyIdleAmplitude = .7f;
        swim.bodySwimAmplitude = 5f;
        swim.strokeSwimAmplitude = 3f;
        swim.turnTailKick = .025f;
        swim.maxTailKick = 4f;
        swim.headRecoil = .3f;
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
            ["Root"] = new Vector3(0,-.045f,.17f), ["Belly"] = new Vector3(0,-.09f,.17f),
            ["Head"] = new Vector3(0,-.065f,.285f),
            ["Spine_01"] = new Vector3(0,-.04f,.185f), ["Spine_02"] = new Vector3(0,-.04f,.095f),
            ["Spine_03"] = new Vector3(0,-.04f,.01f), ["Spine_04"] = new Vector3(0,-.045f,-.065f),
            ["Tail_Base"] = new Vector3(0,-.05f,-.105f),
            ["Pelvic_Base"] = new Vector3(0,-.13f,.21f)
        };
        string[] tails = { "Tail_Top_", "Tail_UpMid_", "Tail_", "Tail_LowMid_", "Tail_Low_", "Tail_Bot_" };
        float[] angles = { 55,35,12,-8,-28,-45 }, lengths = { .25f,.29f,.30f,.29f,.26f,.22f };
        for (int i = 0; i < tails.Length; i++)
        {
            float radians = angles[i] * Mathf.Deg2Rad;
            Vector3 origin = p["Tail_Base"];
            Ray(tails[i], 5, origin, origin + new Vector3(0, Mathf.Sin(radians), -Mathf.Cos(radians)) * lengths[i]);
        }
        Ray("Dorsal_A_",4,new Vector3(0,.015f,.11f),new Vector3(0,.25f,.05f));
        Ray("Dorsal_B_",4,new Vector3(0,.02f,-.035f),new Vector3(0,.30f,-.11f));
        Ray("Anal_A_",4,new Vector3(0,-.10f,.14f),new Vector3(0,-.31f,.10f));
        Ray("Anal_B_",4,new Vector3(0,-.10f,-.02f),new Vector3(0,-.30f,-.13f));
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            Ray("Pelvic_"+suffix+"_",3,new Vector3(side*.045f,-.13f,.21f),new Vector3(side*.11f,-.23f,.04f));
            p["Pectoral_"+suffix+"_01"] = new Vector3(side*.06f,-.065f,.20f);
            p["Pectoral_"+suffix+"_02"] = new Vector3(side*.09f,-.06f,.17f);
            p["Pectoral_"+suffix+"_03A"] = new Vector3(side*.115f,-.085f,.15f);
            p["Pectoral_"+suffix+"_03B"] = new Vector3(side*.10f,-.025f,.15f);
        }
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
            float head = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.19f,.245f,point.z))
                * Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.19f,-.15f,point.y));
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
    static readonly float[] Z = { -.12f,-.08f,0,.08f,.16f,.24f,.30f,.375f };
    static readonly float[] Width = { .012f,.023f,.035f,.042f,.060f,.065f,.052f,.005f };
    static readonly float[] Height = { .02f,.045f,.065f,.075f,.085f,.09f,.07f,.015f };
    static readonly float[] Center = { -.045f,-.045f,-.045f,-.045f,-.05f,-.06f,-.07f,-.07f };
    static float BodyShare(Vector3 point)
    {
        int i=1;while(i<Z.Length-1 && point.z>Z[i])i++;
        float t=Mathf.InverseLerp(Z[i-1],Z[i],point.z);
        float x=point.x/Mathf.Lerp(Width[i-1],Width[i],t);
        float y=(point.y-Mathf.Lerp(Center[i-1],Center[i],t))/Mathf.Lerp(Height[i-1],Height[i],t);
        // Protect the full body cross section, including its upper side corners.
        // An ellipse here misclassifies those opaque shoulder vertices as dorsal fin.
        float radial=Mathf.Max(Mathf.Abs(x),Mathf.Abs(y));
        return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.15f,1.65f,radial)))
            *Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.13f,-.08f,point.z));
    }

    static RuntimeAnimatorController RotationClips(RuntimeAnimatorController controller)
    {
        string path=Root+"/AquaFireBetta.overrideController";
        var overrides=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if(!overrides){overrides=new AnimatorOverrideController(controller);AssetDatabase.CreateAsset(overrides,path);}
        overrides.runtimeAnimatorController=controller;
        foreach(var clip in controller.animationClips.Distinct())
        {
            string clipPath=Root+"/AquaFire_"+clip.name+".anim";
            var rotation=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if(!rotation){rotation=new AnimationClip();AssetDatabase.CreateAsset(rotation,clipPath);}
            EditorUtility.CopySerialized(clip,rotation);
            rotation.name="AquaFire_"+clip.name;
            foreach(var binding in AnimationUtility.GetCurveBindings(rotation))
                if(binding.propertyName.StartsWith("m_LocalPosition") || binding.propertyName.StartsWith("m_LocalScale"))
                    AnimationUtility.SetEditorCurve(rotation,binding,null);
            EditorUtility.SetDirty(rotation);
            overrides[clip]=rotation;
        }
        EditorUtility.SetDirty(overrides);
        return overrides;
    }
}
