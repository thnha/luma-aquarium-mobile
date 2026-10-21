// Edit Mode. Measure the BODY alone in a frame running from peduncle to head.
// A continuous roof must descend towards the mouth without a neck valley/rebound.
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var fish=UnityEngine.Object.Instantiate(prefab);
var baked=new UnityEngine.Mesh();
try
{
    fish.GetComponent<UnityEngine.Animator>().enabled=false;
    var idle=UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Betta/Betta.fbx").OfType<UnityEngine.AnimationClip>().First(c=>c.name=="Idle");
    idle.SampleAnimation(fish,0);
    var appearance=fish.AddComponent<BettaAppearance>();
    appearance.Apply(UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>("Assets/_Project/Data/Betta/Variants/AquaPearlRosetail.asset"));
    typeof(BettaAppearance).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(appearance,null);
    var renderer=fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r=>r.name=="BODY");
    var source=BettaHeadShape.Source(renderer.sharedMesh);
    int hi=Array.FindIndex(renderer.bones,b=>b.name=="Head"),ti=Array.FindIndex(renderer.bones,b=>b.name=="Tail_Base");
    var head=source.bindposes[hi].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    var tail=source.bindposes[ti].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    float length=UnityEngine.Vector3.Distance(head,tail);
    var forward=(head-tail).normalized;
    var up=UnityEngine.Vector3.Cross(forward,UnityEngine.Vector3.right).normalized;
    renderer.BakeMesh(baked);
    var vertices=baked.vertices;var triangles=baked.triangles;
    float Roof(float s)
    {
        float plane=s*length,top=float.NegativeInfinity;
        for(int i=0;i<triangles.Length;i+=3)
            for(int edge=0;edge<3;edge++)
            {
                var a=vertices[triangles[i+edge]]-head;var b=vertices[triangles[i+(edge+1)%3]]-head;
                float x=UnityEngine.Vector3.Dot(a,forward),y=UnityEngine.Vector3.Dot(b,forward);
                if((x<=plane&&plane<y)||(y<=plane&&plane<x))
                {var p=UnityEngine.Vector3.Lerp(a,b,(plane-x)/(y-x));top=UnityEngine.Mathf.Max(top,UnityEngine.Vector3.Dot(p,up));}
            }
        if(float.IsInfinity(top))throw new Exception("Missing cross section at "+s);
        return top/length;
    }
    float Thickness(float s)
    {
        float plane=s*length,above=float.PositiveInfinity,below=float.NegativeInfinity;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var points=new System.Collections.Generic.List<UnityEngine.Vector3>();
            for(int edge=0;edge<3;edge++)
            {
                var a=vertices[triangles[i+edge]]-head;var b=vertices[triangles[i+(edge+1)%3]]-head;
                float x=UnityEngine.Vector3.Dot(a,forward),y=UnityEngine.Vector3.Dot(b,forward);
                if((x<=plane&&plane<y)||(y<=plane&&plane<x))points.Add(UnityEngine.Vector3.Lerp(a,b,(plane-x)/(y-x)));
            }
            if(points.Count!=2)continue;
            var p=points[0];var q=points[1];
            if((p.x<=0&&q.x>0)||(q.x<=0&&p.x>0))
            {
                float height=UnityEngine.Vector3.Dot(UnityEngine.Vector3.Lerp(p,q,-p.x/(q.x-p.x)),up);
                if(height>0)above=UnityEngine.Mathf.Min(above,height);
                else below=UnityEngine.Mathf.Max(below,height);
            }
        }
        if(float.IsInfinity(above)||float.IsInfinity(below))throw new Exception("Missing body surface at "+s);
        return (above-below)/length;
    }
    var variants=UnityEditor.AssetDatabase.FindAssets("t:BettaVariant",new[]{"Assets/_Project/Data/Betta/Variants"})
        .Select(id=>UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>(UnityEditor.AssetDatabase.GUIDToAssetPath(id))).ToArray();
    var report=new System.Collections.Generic.List<object>();
    float[] sections={-.25f,-.2f,-.15f,-.1f,-.05f,0,.05f,.1f};
    var late=typeof(BettaAppearance).GetMethod("LateUpdate",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
    foreach(var variant in variants)
    {
        appearance.Apply(variant);idle.SampleAnimation(fish,0);late.Invoke(appearance,null);
        renderer.BakeMesh(baked);vertices=baked.vertices;triangles=baked.triangles;
        var roof=sections.Select(Roof).ToArray();
        for(int i=1;i<roof.Length;i++)
            if(roof[i]>roof[i-1]+.006f)throw new Exception(variant.name+": neck valley / forehead rebound at "+sections[i]);
        float front=Thickness(-.3f),rear=Thickness(-.8f);
        if(front<rear*1.3f)throw new Exception(variant.name+": forebody is not fuller than the peduncle/rear body");
        var headSkin=renderer.bones[hi].localToWorldMatrix*source.bindposes[hi];
        foreach(string name in new[]{"Gill_L_02","Gill_L_03","Gill_R_02","Gill_R_03"})
        {
            int i=Array.FindIndex(renderer.bones,b=>b.name==name);
            var skin=renderer.bones[i].localToWorldMatrix*source.bindposes[i];
            if(UnityEngine.Quaternion.Angle(headSkin.rotation,skin.rotation)>3f)throw new Exception(name+": resting gill folded away from cheek");
        }
        report.Add(new{variant=variant.name,roof,front,rear});
    }
    var animator=fish.GetComponent<UnityEngine.Animator>();
    animator.enabled=true;animator.Rebind();animator.Play("Flare",0,.5f);animator.Update(0);late.Invoke(appearance,null);
    var flareHead=renderer.bones[hi].localToWorldMatrix*source.bindposes[hi];
    foreach(string name in new[]{"Gill_L_03","Gill_R_03"})
    {
        int i=Array.FindIndex(renderer.bones,b=>b.name==name);
        float angle=UnityEngine.Quaternion.Angle(flareHead.rotation,(renderer.bones[i].localToWorldMatrix*source.bindposes[i]).rotation);
        if(angle<4f||angle>10f)throw new Exception("Flare reaction lost or exaggerated: "+name+" "+angle);
    }

    return new{passed=variants.Length*12+2,variants=variants.Length,report};
}
finally{UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(fish);}
