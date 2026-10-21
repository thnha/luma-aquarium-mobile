// The new detail atlas must cover opaque skin, stay off fins/eyes, and contain
// distinct shallow scale edges instead of the original wrinkled normal texture.
var material=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Betta/Betta.mat");
if(!material.HasProperty("_ScaleMap")||!material.GetTexture("_ScaleMap"))throw new Exception("Ordered body-scale atlas is not installed in the material/builder");
var map=(UnityEngine.Texture2D)material.GetTexture("_ScaleMap");
var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(map));
if(importer.sRGBTexture||!importer.mipmapEnabled||map.width<1024)throw new Exception("Scale detail must be linear, mipmapped and high resolution");
var rt=UnityEngine.RenderTexture.GetTemporary(map.width,map.height,0,UnityEngine.RenderTextureFormat.ARGB32,UnityEngine.RenderTextureReadWrite.Linear);
var previous=UnityEngine.RenderTexture.active;
var pixels=new UnityEngine.Texture2D(map.width,map.height,UnityEngine.TextureFormat.RGBA32,false,true);
try{
 UnityEngine.Graphics.Blit(map,rt);UnityEngine.RenderTexture.active=rt;pixels.ReadPixels(new UnityEngine.Rect(0,0,map.width,map.height),0,0);pixels.Apply();
 var fbx=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Betta/Betta.fbx");
 var body=fbx.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().First(r=>r.name=="BODY");var mesh=body.sharedMesh;
 var uv=mesh.uv;var verts=mesh.vertices;var weights=mesh.boneWeights;var tris=mesh.triangles;
 var led=weights.Select(w=>{string n=body.bones[w.boneIndex0].name;return n=="Root"||n=="Head"||n.StartsWith("Spine_")||n.StartsWith("Gill_")||n.StartsWith("Jaw_")||n.StartsWith("Eye_")||n.StartsWith("Belly");}).ToArray();
 var skin=BettaFinSurface.BodyIslands(verts,uv,tris,led);
 var reds=new System.Collections.Generic.List<float>();int bodySamples=0,finSamples=0;
 for(int j=0;j<tris.Length;j+=3){int a=tris[j],b=tris[j+1],c=tris[j+2];var q=(uv[a]+uv[b]+uv[c])/3;var color=pixels.GetPixelBilinear(q.x,q.y);
  if(skin[a]){if(color.a>.9f){bodySamples++;reds.Add(color.r);}}
  else {finSamples++;if(color.a>.02f)throw new Exception("Scales bleed onto BODY fin membranes at "+q);}
 }
 foreach(var fin in fbx.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>().Where(r=>r.name.StartsWith("Fin_"))){var u=fin.sharedMesh.uv;var t=fin.sharedMesh.triangles;
  for(int j=0;j<t.Length;j+=3){var q=(u[t[j]]+u[t[j+1]]+u[t[j+2]])/3;var col=pixels.GetPixelBilinear(q.x,q.y);finSamples++;if(col.a>.02f)throw new Exception("Scales bleed onto "+fin.name);}}
 if(bodySamples<1500||finSamples<100)throw new Exception("Insufficient real body/fin coverage");
 reds.Sort();float lo=reds[reds.Count/20],hi=reds[reds.Count*19/20];if(hi-lo<.2f)throw new Exception("Scale atlas lacks distinct overlapping edges");
 return new{bodySamples,finSamples,contrast=hi-lo,size=map.width};
}finally{UnityEngine.RenderTexture.active=previous;UnityEngine.RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);}
