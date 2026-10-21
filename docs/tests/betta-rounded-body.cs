// Real BODY sections: lower the anterior hump, retain an oval cross section and
// taper continuously into the peduncle. Run on clones in Edit Mode.
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var fish=UnityEngine.Object.Instantiate(prefab);
try {
    fish.GetComponent<UnityEngine.Animator>().enabled=false;
    var renderer=fish.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r=>r.name=="BODY");
    var source=renderer.sharedMesh;
    var original=source.vertices;
    int hi=Array.FindIndex(renderer.bones,b=>b.name=="Head"),ti=Array.FindIndex(renderer.bones,b=>b.name=="Tail_Base");
    var head=source.bindposes[hi].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    var tail=source.bindposes[ti].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
    float length=UnityEngine.Vector3.Distance(head,tail);
    var forward=(head-tail).normalized;var up=UnityEngine.Vector3.Cross(forward,UnityEngine.Vector3.right).normalized;
    UnityEngine.Vector3[] vertices=null;int[] triangles=null;
    UnityEngine.Vector4 Section(float station) {
        var segments=new System.Collections.Generic.List<UnityEngine.Vector2[]>();
        for(int j=0;j<triangles.Length;j+=3){var hits=new System.Collections.Generic.List<UnityEngine.Vector2>();
            for(int k=0;k<3;k++){
                var a=vertices[triangles[j+k]]-head;var b=vertices[triangles[j+(k+1)%3]]-head;
                float sa=UnityEngine.Vector3.Dot(a,forward)/length,sb=UnityEngine.Vector3.Dot(b,forward)/length;
                if((sa<=station&&station<sb)||(sb<=station&&station<sa)){
                    var p=UnityEngine.Vector3.Lerp(a,b,(station-sa)/(sb-sa));hits.Add(new UnityEngine.Vector2(p.x,UnityEngine.Vector3.Dot(p,up))/length);
                }
            }
            if(hits.Count==2)segments.Add(hits.ToArray());
        }
        float top=float.PositiveInfinity,bottom=float.NegativeInfinity;
        foreach(var seg in segments){var a=seg[0];var b=seg[1];
            if((a.x<=0&&b.x>0)||(b.x<=0&&a.x>0)){
                float y=UnityEngine.Vector2.Lerp(a,b,-a.x/(b.x-a.x)).y;
                if(y>0)top=Mathf.Min(top,y);else bottom=Mathf.Max(bottom,y);
            }
        }
        float middle=(top+bottom)*.5f,left=float.NegativeInfinity,right=float.PositiveInfinity;
        foreach(var seg in segments){var a=seg[0];var b=seg[1];
            if((a.y<=middle&&b.y>middle)||(b.y<=middle&&a.y>middle)){
                float x=UnityEngine.Vector2.Lerp(a,b,(middle-a.y)/(b.y-a.y)).x;
                if(x>0)right=Mathf.Min(right,x);else left=Mathf.Max(left,x);
            }
        }
        if(float.IsInfinity(top)||float.IsInfinity(bottom)||float.IsInfinity(left)||float.IsInfinity(right))throw new Exception("Missing closed body section at "+station);
        return new UnityEngine.Vector4(top,bottom,right-left,(right-left)/(top-bottom));
    }
    var variants=UnityEditor.AssetDatabase.FindAssets("t:BettaVariant",new[]{"Assets/_Project/Data/Betta/Variants"})
        .Select(id=>UnityEditor.AssetDatabase.LoadAssetAtPath<BettaVariant>(UnityEditor.AssetDatabase.GUIDToAssetPath(id)));
    var failures=new System.Collections.Generic.List<string>();var reports=new System.Collections.Generic.List<object>();int checks=0;
    void Check(bool ok,string name){checks++;if(!ok)failures.Add(name);}
    foreach(var variant in variants){
        var mesh=BettaHeadShape.Get(source,renderer.bones,head,source.bindposes[Array.FindIndex(renderer.bones,b=>b.name=="Spine_01")].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero),variant.headScale);
        vertices=mesh.vertices;triangles=mesh.GetTriangles(0);
        var front=Section(-.3f);var middle=Section(-.55f);var rear=Section(-.85f);
        Check(front.x<.225f,variant.name+": high anterior hump ("+front.x+")");
        Check(front.w>=.48f&&front.w<=.65f,variant.name+": forebody too flat or swollen ("+front.w+")");
        Check(middle.w>=.4f&&middle.w<=.65f,variant.name+": midbody lacks rounded volume ("+middle.w+")");
        Check(rear.z<middle.z&&middle.z<front.z,variant.name+": width must taper progressively to peduncle");
        Check(rear.x-rear.y<middle.x-middle.y&&middle.x-middle.y<front.x-front.y,variant.name+": height must taper progressively to peduncle");
        Check(source.vertices.SequenceEqual(original),variant.name+": source asset changed");
        reports.Add(new{variant=variant.name,front,middle,rear});
    }
    if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    return new{passed=checks,reports};
} finally {UnityEngine.Object.DestroyImmediate(fish);}
