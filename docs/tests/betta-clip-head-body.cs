// Regression: anterior cheek/shoulder fullness, a short mouth and a rising posterior belly.
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
    var failures=new System.Collections.Generic.List<string>();var reports=new System.Collections.Generic.List<object>();int checks=0;
    void Check(bool ok,string message){checks++;if(!ok)failures.Add(message);}
    foreach(float proportion in new[]{.8f,1f,1.3f}){
        var shape=BettaHeadShape.Get(source,renderer.bones,head,source.bindposes[Array.FindIndex(renderer.bones,b=>b.name=="Spine_01")].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero),proportion);
        vertices=shape.vertices;triangles=shape.GetTriangles(0);
        var shoulder=Section(-.035f/length);var abdomen=Section(-.06f/length);var rear=Section(-.10f/length);
        Check(shoulder.z>abdomen.z*1.005f,"Fullest width must sit behind operculum, rather than halfway along body at "+proportion);
        Check(shoulder.y<abdomen.y-.0015f/length,"Anterior belly must have a soft, visible convex arc at "+proportion);
        Check(rear.y-shoulder.y>.007f/length,"Belly must lift progressively toward tail at "+proportion);
        // Sample the actual skull, excluding fin membranes, in the common body frame.
        float oldSnout=original.Max(p=>UnityEngine.Vector3.Dot(p-head,forward));
        float newSnout=vertices.Max(p=>UnityEngine.Vector3.Dot(p-head,forward));
        Check(newSnout<=oldSnout-.0012f,"Mouth remains too extended at "+proportion);
        reports.Add(new{proportion,shoulder=new[]{shoulder.x,shoulder.y,shoulder.z,shoulder.w},abdomen=new[]{abdomen.x,abdomen.y,abdomen.z,abdomen.w},rear=new[]{rear.x,rear.y,rear.z,rear.w},oldSnout,newSnout});
    }
    System.IO.File.WriteAllText("/private/tmp/betta-clip-head-body.json",Newtonsoft.Json.JsonConvert.SerializeObject(reports,Newtonsoft.Json.Formatting.Indented));
    if(failures.Count>0)throw new Exception(string.Join("\n",failures));
    return new{passed=checks,proportions=3};
}finally{UnityEngine.Object.DestroyImmediate(fish);}
