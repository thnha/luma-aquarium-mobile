using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AquariumHardscapeBuilder
{
    private const string Root = "Assets/_Project/Art/Aquarium3D";
    private const string NightPath = "Assets/_Project/UI/Backgrounds/AquariumEnvironmentNight-v3.png";

    [MenuItem("Lumaquarium/Preview/Upgrade Hardscape and Day Night")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (SceneManager.GetActiveScene().path != Aquarium3DPreviewBuilder.PreviewPath)
        {
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            EditorSceneManager.OpenScene(Aquarium3DPreviewBuilder.PreviewPath);
        }
        ApplyToScene(true);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static void ApplyToScene(bool backup)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != Aquarium3DPreviewBuilder.PreviewPath)
            throw new InvalidOperationException("Open the preview in Edit Mode first.");
        if (backup) EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),
            $"Assets/_Project/Scenes/Backups/Aquarium3D-before-hardscape-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.unity", true);
        foreach (string texture in new[] { "RiverStone", "RiverSand", "Driftwood" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Textures/" + texture + "-v3.png");
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4; importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        var d = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
        var stone = Material("RiverStone",3,.66f);
        var sand = Material("RiverSand",5,.91f);
        var wood = Material("Driftwood",2,.8f); wood.SetFloat("_UVMode",1);
        var rockMesh = SaveMesh(RockMesh(20,32),"RiverStone-v3");
        var rock = Prefab("RiverRockNatural",1,.19f,rockMesh,stone,new Vector3(.35f,.26f,.3f),new Vector3(0,.115f,0));
        var driftwood = Prefab("BranchingDriftwoodNatural",2,.24f,SaveMesh(WoodMesh(),"Driftwood-v3"),wood,Vector3.one,Vector3.zero);
        Undo.RecordObject(d,"Upgrade hardscape inventory");
        d.prefabs[1]=rock; d.prefabs[2]=driftwood;
        foreach (var old in d.itemsRoot.GetComponentsInChildren<AquariumDecorItem>().Where(x=>x.kind==1 || x.kind==2).ToArray())
        {
            var next = (AquariumDecorItem)PrefabUtility.InstantiatePrefab(d.prefabs[old.kind],d.itemsRoot);
            Undo.RegisterCreatedObjectUndo(next.gameObject,"Upgrade placed hardscape");
            next.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);
            next.transform.localScale=old.transform.localScale;
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        var floor = GameObject.Find("SandBed3D");
        Undo.RecordObject(floor.GetComponent<MeshRenderer>(),"Natural sand material");
        floor.GetComponent<MeshRenderer>().sharedMaterial=sand;
        BuildPebbles(floor.transform,stone);
        BuildLighting(d);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static Material Material(string name,float tiling,float roughness)
    {
        string path=Root+"/"+name+"-v3.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null) { mat=new Material(Shader.Find("Lumaquarium/Aquarium Hardscape")); AssetDatabase.CreateAsset(mat,path); }
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+"-v3.png"));
        mat.SetColor("_BaseColor",Color.white); mat.SetFloat("_Tiling",tiling); mat.SetFloat("_Roughness",roughness);
        EditorUtility.SetDirty(mat); return mat;
    }

    private static AquariumDecorItem Prefab(string name,int kind,float footprint,Mesh mesh,Material material,Vector3 scale,Vector3 offset)
    {
        var root=new GameObject(name); root.layer=8;
        var item=root.AddComponent<AquariumDecorItem>(); item.kind=kind; item.footprint=footprint;
        var model=MeshObject("NaturalSurface",root.transform,mesh,material);
        model.transform.localScale=scale; model.transform.localPosition=offset;
        var collider=root.AddComponent<BoxCollider>(); collider.isTrigger=true;
        collider.center=kind==1?new Vector3(0,.12f,0):new Vector3(0,.33f,0);
        collider.size=kind==1?new Vector3(.38f,.3f,.34f):new Vector3(.46f,.7f,.34f);
        var saved=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+name+".prefab");
        UnityEngine.Object.DestroyImmediate(root); return saved.GetComponent<AquariumDecorItem>();
    }

    private static Mesh RockMesh(int rows,int columns)
    {
        var v=new List<Vector3>(); var uv=new List<Vector2>(); var c=new List<Color>(); var tri=new List<int>();
        for(int row=0;row<=rows;row++) for(int col=0;col<=columns;col++)
        {
            float phi=row*Mathf.PI/rows,theta=col*Mathf.PI*2/columns;
            Vector3 p=new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta));
            float shape=.47f+.065f*Mathf.PerlinNoise(p.x*2.2f+4,p.y*1.8f+p.z*.9f+8)+.025f*Mathf.Sin(p.z*8+p.y*3);
            v.Add(p*shape); uv.Add(new Vector2(col/(float)columns,row/(float)rows)); c.Add(Color.white);
            if(row<rows && col<columns) { int i=row*(columns+1)+col; tri.AddRange(new[]{i,i+1,i+columns+1,i+1,i+columns+2,i+columns+1}); }
        }
        return Mesh(v,uv,c,tri);
    }

    private static Mesh WoodMesh()
    {
        var v=new List<Vector3>(); var uv=new List<Vector2>(); var c=new List<Color>(); var tri=new List<int>();
        System.Action<Vector3,Vector3,Vector3,float,float> branch=(a,b,end,r0,r1)=>
        {
            const int rings=22,sides=14; int start=v.Count;
            Vector3 previous=a; float distance=0;
            for(int ring=0;ring<=rings;ring++)
            {
                float t=ring/(float)rings;
                Vector3 center=(1-t)*(1-t)*a+2*(1-t)*t*b+t*t*end;
                Vector3 tangent=(2*(1-t)*(b-a)+2*t*(end-b)).normalized;
                Vector3 right=Vector3.Cross(tangent,Mathf.Abs(tangent.z)<.9f?Vector3.forward:Vector3.up).normalized;
                Vector3 forward=Vector3.Cross(right,tangent).normalized;
                distance+=Vector3.Distance(center,previous); previous=center;
                for(int side=0;side<=sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    float radius=Mathf.Lerp(r0,r1,Mathf.Pow(t,.8f))*(1+.08f*Mathf.Sin(angle*5+t*7)+.06f*Mathf.Sin(t*23));
                    v.Add(center+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*radius);
                    uv.Add(new Vector2(side/(float)sides,distance*4)); c.Add(Color.white);
                    if(ring<rings && side<sides) {int i=start+ring*(sides+1)+side; tri.AddRange(new[]{i,i+sides+1,i+1,i+1,i+sides+1,i+sides+2});}
                }
            }
            // End caps also close the trunk when viewed from above in placement mode.
            int bottom=v.Count; v.Add(a);uv.Add(Vector2.zero);c.Add(new Color(.6f,.55f,.48f));
            int top=v.Count; v.Add(end);uv.Add(Vector2.one);c.Add(new Color(.65f,.59f,.5f));
            for(int side=0;side<sides;side++) {tri.AddRange(new[]{bottom,start+side,start+side+1});int i=start+rings*(sides+1)+side;tri.AddRange(new[]{top,i+1,i});}
        };
        branch(new Vector3(-.13f,.02f,0),new Vector3(-.08f,.25f,.02f),new Vector3(.12f,.68f,.03f),.063f,.005f);
        branch(new Vector3(-.04f,.24f,.02f),new Vector3(-.17f,.38f,.06f),new Vector3(-.15f,.54f,.04f),.028f,.003f);
        branch(new Vector3(.035f,.42f,.025f),new Vector3(.2f,.4f,-.08f),new Vector3(.22f,.52f,-.1f),.021f,.002f);
        branch(new Vector3(-.09f,.09f,0),new Vector3(.08f,.015f,.08f),new Vector3(.19f,.008f,.1f),.04f,.005f);
        branch(new Vector3(-.11f,.06f,0),new Vector3(-.17f,.025f,-.08f),new Vector3(-.19f,.006f,-.12f),.032f,.004f);
        return Mesh(v,uv,c,tri);
    }

    private static void BuildPebbles(Transform floor,Material material)
    {
        var old=floor.parent.Find("PebbleBed3D"); if(old!=null) Undo.DestroyObjectImmediate(old.gameObject);
        Mesh pebble=RockMesh(6,10);var instances=new List<CombineInstance>();var meshes=new List<Mesh>();
        var rng=new System.Random(4731);
        for(int i=0;i<190;i++)
        {
            float x=(float)rng.NextDouble()*2.4f-1.2f,z=(float)rng.NextDouble()*3.4f-2.2f;
            float radius=.009f+(float)rng.NextDouble()*.024f;
            var copy=UnityEngine.Object.Instantiate(pebble); meshes.Add(copy);
            Color tint=Color.Lerp(new Color(.54f,.43f,.32f),new Color(1.12f,1.08f,.93f),(float)rng.NextDouble());
            copy.colors=Enumerable.Repeat(tint,copy.vertexCount).ToArray();
            float height=.28f+.025f*Mathf.PerlinNoise((x+1.35f)*6+10,((z+.6f)/floor.localScale.z+1.6f)*6+20);
            instances.Add(new CombineInstance {mesh=copy,transform=Matrix4x4.TRS(new Vector3(x,height+radius*.2f,z),Quaternion.Euler(0,(float)rng.NextDouble()*360,0),new Vector3(radius*2,radius,radius*1.5f))});
        }
        var merged=new Mesh();merged.CombineMeshes(instances.ToArray(),true,true);
        foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(pebble);
        MeshObject("PebbleBed3D",floor.parent,SaveMesh(merged,"PebbleBed-v3"),material);
    }

    private static void BuildLighting(AquariumDecoration d)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(NightPath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.SaveAndReimport();
        // The painted backdrop was removed; the night image is only kept for scenes that still have one.
        var day=GameObject.Find("AquariumBackdrop/Environment");
        Image image=null;
        if(day!=null)
        {
            var night=day.transform.parent.Find("NightEnvironment");
            if(night==null) { var go=UnityEngine.Object.Instantiate(day,day.transform.parent);go.name="NightEnvironment";night=go.transform;Undo.RegisterCreatedObjectUndo(go,"Night backdrop"); }
            image=night.GetComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(NightPath);image.color=new Color(1,1,1,0);image.raycastTarget=false;
            var aspect=night.GetComponent<AspectRatioFitter>();if(aspect!=null)aspect.aspectRatio=image.sprite.rect.width/image.sprite.rect.height;
        }
        var lighting=d.GetComponent<AquariumLighting>();if(lighting==null)lighting=Undo.AddComponent<AquariumLighting>(d.gameObject);
        Undo.RecordObject(lighting,"Configure aquarium lighting");
        lighting.sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(x=>x.type==LightType.Directional);
        lighting.nightBackdrop=image;
        // The redesigned HUD keeps this switch in Cài đặt; only older HUDs get one in the header.
        var header=d.screen.header.transform;var button=d.screen.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DayNightButton");
        if(button==null)
        {
            var go=new GameObject("DayNightButton",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(header,false);button=go.transform;
            Undo.RegisterCreatedObjectUndo(go,"Lighting switch");
            var rect=(RectTransform)button;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(210,80);rect.anchoredPosition=new Vector2(30,-12);
            var surface=go.GetComponent<Image>();surface.sprite=AssetDatabase.FindAssets("button_primary t:Sprite").Select(x=>AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(x))).FirstOrDefault();surface.type=Image.Type.Sliced;
            var textGo=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));textGo.transform.SetParent(button,false);
            var tr=(RectTransform)textGo.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.sizeDelta=Vector2.zero;
            var text=textGo.GetComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Fonts/Nunito-SemiBold SDF.asset");text.fontSize=26;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.2f,.15f,.08f);text.raycastTarget=false;text.text="Ban ngày";
        }
        lighting.label=button.GetComponentInChildren<TMP_Text>();
        var control=button.GetComponent<Button>();control.targetGraphic=button.GetComponent<Image>();
        while(control.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(control.onClick,0);
        UnityEventTools.AddPersistentListener(control.onClick,lighting.Toggle);
        foreach(var mat in new[]{d.water.surface.sharedMaterial,d.water.bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial})
        {mat.SetTexture("_NightEnvironmentTex",AssetDatabase.LoadAssetAtPath<Texture2D>(NightPath));EditorUtility.SetDirty(mat);}
    }

    private static Mesh Mesh(List<Vector3> v,List<Vector2> uv,List<Color> c,List<int> tri)
    {var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(c);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    private static Mesh SaveMesh(Mesh mesh,string name)
    {
        string path=Root+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    private static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
    {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;}
}
