// Intersect the actual opaque skin, rather than testing the profile's width constants.
// A laterally compressed oval is intentional; small sharp departures from that oval are not.
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Betta.prefab");
var body=prefab.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).First(r=>r.name=="BODY");
var source=body.sharedMesh;
UnityEngine.Vector3 Bind(string name)=>source.bindposes[Array.FindIndex(body.bones,b=>b.name==name)].inverse.MultiplyPoint3x4(UnityEngine.Vector3.zero);
var head=Bind("Head");var tail=Bind("Tail_Base");var forward=(head-tail).normalized;var up=UnityEngine.Vector3.Cross(forward,UnityEngine.Vector3.right).normalized;
var reports=new System.Collections.Generic.List<object>();var failures=new System.Collections.Generic.List<string>();
foreach(float proportion in new[]{.8f,1f,1.3f}){
 var mesh=BettaHeadShape.Get(source,body.bones,head,Bind("Spine_01"),proportion);
 var v=mesh.vertices;var tri=mesh.GetTriangles(0);var torso=new System.Collections.Generic.List<float>();var cheek=new System.Collections.Generic.List<float>();
 foreach(float station in new[]{-.16f,-.14f,-.12f,-.10f,-.08f,-.06f,-.04f,-.02f,-.01f,0f}){
  var segments=new System.Collections.Generic.List<UnityEngine.Vector2[]>();
  for(int j=0;j<tri.Length;j+=3){var hits=new System.Collections.Generic.List<UnityEngine.Vector2>();
   for(int k=0;k<3;k++){var a=v[tri[j+k]]-head;var b=v[tri[j+(k+1)%3]]-head;float sa=UnityEngine.Vector3.Dot(a,forward),sb=UnityEngine.Vector3.Dot(b,forward);
    if((sa<=station&&station<sb)||(sb<=station&&station<sa)){var p=UnityEngine.Vector3.Lerp(a,b,(station-sa)/(sb-sa));hits.Add(new UnityEngine.Vector2(p.x,UnityEngine.Vector3.Dot(p,up)));}}
   if(hits.Count==2)segments.Add(hits.ToArray());
  }
  if(segments.Count<8)throw new Exception("Missing skin section "+station);
  var points=segments.SelectMany(x=>x).ToArray();float cx=(points.Max(p=>p.x)+points.Min(p=>p.x))*.5f,cy=(points.Max(p=>p.y)+points.Min(p=>p.y))*.5f;
  float rx=(points.Max(p=>p.x)-points.Min(p=>p.x))*.5f,ry=(points.Max(p=>p.y)-points.Min(p=>p.y))*.5f;
  float Cross(UnityEngine.Vector2 a,UnityEngine.Vector2 b)=>a.x*b.y-a.y*b.x;
  float squared=0;int samples=0;
  for(int angle=0;angle<360;angle+=2){float t=angle*Mathf.Deg2Rad;var dir=new UnityEngine.Vector2(Mathf.Cos(t),Mathf.Sin(t));float outer=0;
   foreach(var seg in segments){var a=new UnityEngine.Vector2((seg[0].x-cx)/rx,(seg[0].y-cy)/ry);var b=new UnityEngine.Vector2((seg[1].x-cx)/rx,(seg[1].y-cy)/ry);var edge=b-a;float den=Cross(dir,edge);if(Mathf.Abs(den)<1e-8f)continue;float r=Cross(a,edge)/den,u=Cross(a,dir)/den;if(r>0&&u>=0&&u<=1)outer=Mathf.Max(outer,r);}
   if(outer<=0)throw new Exception("Open section at "+station);squared+=(outer-1)*(outer-1);samples++;
  }
  float rms=Mathf.Sqrt(squared/samples);if(station<=-.04f)torso.Add(rms);else cheek.Add(rms);
  reports.Add(new{proportion,station,rms,widthHeight=rx/ry});
 }
 float torsoMean=torso.Average(),cheekMean=cheek.Average();
 if(torsoMean>.035f)failures.Add("Torso/caudal curvature retains flat panels: "+torsoMean+" at "+proportion);
 if(cheekMean>.055f)failures.Add("Cheek/shoulder curvature retains corners: "+cheekMean+" at "+proportion);
}
System.IO.File.WriteAllText("/private/tmp/betta-surface-curvature.json",Newtonsoft.Json.JsonConvert.SerializeObject(reports,Newtonsoft.Json.Formatting.Indented));
if(failures.Count>0)throw new Exception(string.Join("\n",failures));
return new{passed=6,sections=reports.Count};
