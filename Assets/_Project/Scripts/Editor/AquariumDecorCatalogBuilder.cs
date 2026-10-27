using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Species use distinct silhouettes; meshes and existing materials are shared by all placed copies.
public static partial class AquariumDecorCatalogBuilder
{
    public const string Root = "Assets/_Project/Art/Aquarium3D/DecorShop";
    private const string Art = "Assets/_Project/Art/Aquarium3D/";
    private static readonly string[] Ids = { "hornwort", "cabomba", "vallisneria", "rotala", "anubias-petite", "java-fern",
        "java-moss", "christmas-moss", "mini-fiss", "duckweed", "salvinia", "frogbit",
        "wood-short", "wood-long", "wood-arch", "wood-branches", "wood-roots", "wood-stump",
        "river-pebbles", "dragon-stone", "lava-rock", "seiryu", "rock-cluster", "mini-landscape" };
    private static readonly string[] Names = { "Rong đuôi chó", "Rong la hán", "Cỏ Vallisneria", "Hồng liễu Rotala", "Ráy nana petite", "Dương xỉ Java",
        "Rêu Java", "Rêu Christmas", "Rêu mini fiss", "Bèo tấm", "Bèo ong", "Bèo Nhật",
        "Lũa khúc ngắn", "Lũa thân dài", "Lũa cong", "Lũa phân nhánh", "Lũa chùm rễ", "Lũa gốc cây",
        "Đá cuội suối", "Đá tai mèo", "Đá nham thạch", "Đá vân Seiryu", "Cụm đá tự nhiên", "Hòn non bộ" };
    private static readonly int[] Prices = { 60, 75, 80, 100, 120, 130, 80, 100, 110, 40, 55, 70,
        120, 180, 200, 230, 250, 220, 70, 140, 100, 160, 200, 320 };

    public static AquariumDecorCatalog Build(AquariumDecorItem[] starterPrefabs)
    {
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        string path = Root + "/AquariumDecorCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(path);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<AquariumDecorCatalog>(); AssetDatabase.CreateAsset(catalog,path); }
        var offers = new List<AquariumDecorOffer>();
        for (int i = 0; i < Ids.Length; i++)
        {
            var root = new GameObject(Ids[i]);
            try
            {
                root.layer = 8;
                var item = root.AddComponent<AquariumDecorItem>();
                item.kind = i + 3;
                item.footprint = i < 12 ? (i >= 9 ? .12f : i < 4 ? .16f : .11f) : i < 18 ? (i == 13 ? .30f : .23f) : (i >= 22 ? .29f : .18f);
                item.attachable = i >= 4 && i <= 8;
                item.floats = i >= 9 && i <= 11;
                item.supportsPlants = i >= 12;
                item.tiltable = i >= 12 && i < 18;
                if (i == 8) BuildMoss(item,MossSpeciesBuilder.MiniFiss);
                else if (i < 12) BuildPlant(item,i);
                else if (i < 18) BuildWood(item,i-12);
                else BuildRock(item,i-18);
                var bounds = new Bounds(Vector3.zero,Vector3.one*.02f);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(renderer.bounds);
                var collider = root.AddComponent<BoxCollider>(); collider.isTrigger=true;
                collider.center=bounds.center; collider.size=bounds.size + Vector3.one*.025f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + Ids[i] + ".prefab").GetComponent<AquariumDecorItem>();
                offers.Add(new AquariumDecorOffer { id=Ids[i], displayName=Names[i], price=Prices[i], prefab=prefab,
                    category=i<6?AquariumDecorCategory.Plants:i<9?AquariumDecorCategory.Moss:i<12?AquariumDecorCategory.Floating
                        :i<18?AquariumDecorCategory.Wood:AquariumDecorCategory.Rocks,
                    description=i>=12&&i<18?"Lũa tự nhiên · Có 3 vị trí gắn ráy, dương xỉ hoặc rêu":
                        item.floats?"Bèo nổi trên mặt nước · Kéo để sắp xếp từng cụm":
                        item.attachable?"Trồng trên nền hoặc kéo lên lũa, đá để gắn":i<12?"Cây thủy sinh trồng nền · Lá đung đưa theo dòng nước":
                        i==23?"Tiểu cảnh núi đá nhiều tầng · Gắn rêu lên các mỏm đá":"Đá tự nhiên · Gắn rêu, ráy hoặc dương xỉ lên mặt đá",
                    portrait=Portrait(prefab.gameObject,Ids[i]) });
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        // Named driftwood species follow the 24 shop items; kinds 27+ keep every released kind unchanged.
        var speciesWood = SpeciesWood();
        for (int i = 0; i < DriftwoodSpeciesBuilder.All.Length; i++)
        {
            var species = DriftwoodSpeciesBuilder.All[i];
            var root = new GameObject(species.id);
            try
            {
                root.layer = 8;
                var item = root.AddComponent<AquariumDecorItem>();
                item.kind = Ids.Length + 3 + i;
                item.supportsPlants = true;
                item.tiltable = true;
                var mesh = Store(DriftwoodSpeciesBuilder.Build(species,1,out item.plantMounts,out item.footprint),species.id);
                Model("Wood",root.transform,mesh,speciesWood);
                var bounds = mesh.bounds;
                var collider = root.AddComponent<BoxCollider>(); collider.isTrigger=true;
                collider.center=bounds.center; collider.size=bounds.size + Vector3.one*.025f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + species.id + ".prefab").GetComponent<AquariumDecorItem>();
                offers.Add(new AquariumDecorOffer { id=species.id, displayName=species.name, price=species.price, prefab=prefab,
                    category=AquariumDecorCategory.Wood, description=species.description, portrait=Portrait(prefab.gameObject,species.id) });
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        // Named mosses follow the driftwood species, again keeping every released kind unchanged.
        for (int i = 0; i < MossSpeciesBuilder.All.Length; i++)
        {
            var species = MossSpeciesBuilder.All[i];
            var root = new GameObject(species.id);
            try
            {
                root.layer = 8;
                var item = root.AddComponent<AquariumDecorItem>();
                item.kind = Ids.Length + 3 + DriftwoodSpeciesBuilder.All.Length + i;
                item.attachable = true;
                item.footprint = BuildMoss(item,species);
                var bounds = new Bounds(Vector3.zero,Vector3.one*.02f);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(renderer.bounds);
                var collider = root.AddComponent<BoxCollider>(); collider.isTrigger=true;
                collider.center=bounds.center; collider.size=bounds.size + Vector3.one*.025f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + species.id + ".prefab").GetComponent<AquariumDecorItem>();
                offers.Add(new AquariumDecorOffer { id=species.id, displayName=species.name, price=species.price, prefab=prefab,
                    category=AquariumDecorCategory.Moss, description=species.description, portrait=Portrait(prefab.gameObject,species.id) });
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        // Named stones and corals follow on kinds 40+; corals get their own San hô tab. Kinds 38 and 39 were
        // the retired moss walls and stay unused, so released stones keep their kinds.
        var speciesStone = SpeciesStone(); var smoothCoral = SmoothCoral();
        const int retiredWallKinds = 2;
        int firstStoneKind = Ids.Length + 3 + DriftwoodSpeciesBuilder.All.Length + MossSpeciesBuilder.All.Length + retiredWallKinds;
        for (int i = 0; i < StoneCoralBuilder.All.Length; i++)
        {
            var piece = StoneCoralBuilder.All[i];
            var root = new GameObject(piece.id);
            try
            {
                root.layer = 8;
                var item = root.AddComponent<AquariumDecorItem>();
                item.kind = firstStoneKind + i;
                var mesh = Store(StoneCoralBuilder.Build(piece,1,out item.footprint),piece.id);
                Model("Stone",root.transform,mesh,piece.smooth?smoothCoral:speciesStone);
                // Stones take moss on their highest faces like the other rocks; corals stay bare.
                item.supportsPlants = !piece.IsCoral;
                if (item.supportsPlants) item.plantMounts = TopMounts(root);
                var bounds = mesh.bounds;
                var collider = root.AddComponent<BoxCollider>(); collider.isTrigger=true;
                collider.center=bounds.center; collider.size=bounds.size + Vector3.one*.025f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + piece.id + ".prefab").GetComponent<AquariumDecorItem>();
                offers.Add(new AquariumDecorOffer { id=piece.id, displayName=piece.name, price=piece.price, prefab=prefab,
                    category=piece.IsCoral?AquariumDecorCategory.Coral:AquariumDecorCategory.Rocks, description=piece.description, portrait=Portrait(prefab.gameObject,piece.id) });
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        string[] starterIds = { "starter-anubias", "starter-stone", "starter-wood" };
        string[] starterNames = { "Ráy Anubias", "Đá suối tròn", "Lũa nhánh cổ thụ" };
        int[] starterPrices = { 140, 80, 200 };
        for (int i = 0; i < 3; i++)
        {
            string prefabPath = AssetDatabase.GetAssetPath(starterPrefabs[i]);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var item = root.GetComponent<AquariumDecorItem>();
                item.attachable = i == 0;
                item.supportsPlants = i != 0;
                if (i == 1) item.plantMounts = TopMounts(root);
                if (i == 2) ApplyStarterWood(root);
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            offers.Add(new AquariumDecorOffer { id=starterIds[i], displayName=starterNames[i], price=starterPrices[i], prefab=starterPrefabs[i],
                category=i==0?AquariumDecorCategory.Plants:i==1?AquariumDecorCategory.Rocks:AquariumDecorCategory.Wood,
                description=i==2?"Lũa phân nhánh cao · Gắn cây hoặc rêu tại 3 vị trí":i==0?"Cây lá rộng · Trồng nền hoặc gắn lên lũa, đá":"Đá cuội tròn · Gắn rêu hoặc ráy lên mặt đá",
                portrait=Portrait(starterPrefabs[i].gameObject,starterIds[i]) });
        }
        // Large pieces come after the starters so every earlier offer keeps its place in built sheets.
        AddLarge(offers,firstStoneKind+StoneCoralBuilder.All.Length,speciesWood,speciesStone);
        AddCoralCollection(offers);
        AddClimbingCarpets(offers);
        AddNewPlants(offers);
        // Names and prices edited in decor_catalog.csv (or the Google Sheet) win over the defaults above.
        DecorCatalogSheet.ApplyIfPresent(offers);
        catalog.offers=offers.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); return catalog;
    }

    // A height above 1 grows the same species taller, with more leaf levels, under its own mesh name.
    private static void BuildPlant(AquariumDecorItem item,int style,float tall=1,string name=null)
    {
        name=name??Ids[style];
        item.foliage=new GameObject("SwayingFoliage").transform; item.foliage.SetParent(item.transform,false);
        item.phase=style*1.37f;
        var leaves=new Geometry(); var stems=new Geometry();
        var rng=new System.Random(217+style);
        Func<float> random=()=> (float)rng.NextDouble();
        if(style<=3)
        {
            int count=style==2?13:7;
            for(int n=0;n<count;n++)
            {
                float angle=n*2.39996f;
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 start=radial*(.025f+random()*.04f);
                float height=(.29f+random()*.23f)*tall;
                Vector3 tip=start+radial*.035f+Vector3.up*height;
                if(style==2) { leaves.Blade(start,tip+radial*.07f*tall,tall>1?.02f:.012f,radial,Mathf.RoundToInt(5*tall)); continue; }
                stems.Tube(start,(start+tip)*.5f,tip,.003f*Mathf.Sqrt(tall),.001f,Mathf.RoundToInt(7*tall),5);
                int levels=Mathf.RoundToInt(7*tall);
                for(int level=1;level<=levels;level++)
                {
                    Vector3 center=Vector3.Lerp(start,tip,level/(levels+1f));
                    int whorl=style==3?2:5;
                    for(int k=0;k<whorl;k++)
                    {
                        float a=k*Mathf.PI*2/whorl+level*.6f+n;
                        var direction=new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a));
                        float length=(style==3?.045f:.065f)*(1-level*.385f/levels);
                        if(style==3) leaves.Blade(center,center+direction*length,.014f,Vector3.up,4);
                        else
                        {
                            Vector3 end=center+direction*length;
                            stems.Tube(center,Vector3.Lerp(center,end,.5f),end,.0011f,.0004f,3,4);
                            for(int tooth=1;tooth<=5;tooth++)
                            {
                                Vector3 point=Vector3.Lerp(center,end,tooth/6f);
                                Vector3 side=Vector3.Cross(direction,Vector3.up).normalized*(.016f*(1-tooth/7f));
                                leaves.Blade(point,point+side+direction*.008f,.0025f,Vector3.up,2);
                                leaves.Blade(point,point-side+direction*.008f,.0025f,Vector3.up,2);
                            }
                        }
                    }
                }
            }
        }
        else if(style==4 || style==5)
        {
            for(int n=0;n<(style==4?12:16);n++)
            {
                float a=n*2.39996f; var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 basePoint=radial*.025f;
                Vector3 petiole=radial*.035f+Vector3.up*(.04f+random()*.065f);
                stems.Tube(basePoint,petiole*.65f,petiole,.003f,.002f,5,5);
                float length=style==4?.07f+random()*.055f:.13f+random()*.10f;
                leaves.Blade(petiole,petiole+(radial*.7f+Vector3.up)*length,style==4?.045f:.023f,radial,10);
            }
        }
        else if(style<=8)
        {
            int count=style==8?38:24;
            for(int n=0;n<count;n++)
            {
                float a=n*2.39996f; float radius=Mathf.Sqrt(random())*.085f;
                Vector3 start=new Vector3(Mathf.Cos(a)*radius,.005f,Mathf.Sin(a)*radius);
                Vector3 end=start+new Vector3(Mathf.Cos(a)*.018f,.035f+random()*(style==8?.025f:.07f),Mathf.Sin(a)*.018f);
                stems.Tube(start,(start+end)*.5f,end,.0018f,.0005f,4,4);
                int levels=style==8?4:6;
                for(int k=1;k<=levels;k++)
                {
                    Vector3 c=Vector3.Lerp(start,end,k/(float)(levels+1));
                    float width=(style==7?.035f*(1-k/(float)(levels+2)):.012f);
                    Vector3 side=new Vector3(Mathf.Cos(a+1),.25f,Mathf.Sin(a+1));
                    leaves.Blade(c,c+side*width,.0035f,Vector3.up,3);
                    leaves.Blade(c,c-side*width+Vector3.up*.004f,.0035f,Vector3.up,3);
                }
            }
        }
        else
        {
            int plants=style==9?19:style==10?7:3;
            for(int n=0;n<plants;n++)
            {
                float a=n*2.39996f, radius=Mathf.Sqrt(random())*.075f;
                Vector3 center=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                int petals=style==9?3:style==10?4:6;
                float length=style==9?.016f:style==10?.034f:.052f;
                for(int k=0;k<petals;k++)
                {
                    float b=k*Mathf.PI*2/petals+n;
                    Vector3 direction=new Vector3(Mathf.Cos(b),.1f,Mathf.Sin(b));
                    leaves.Blade(center,center+direction*length,length*.7f,Vector3.up,8);
                }
                for(int k=0;k<3;k++)
                    stems.Tube(center,center+new Vector3(.007f*k,-.025f,0),center+new Vector3(.009f*k,-(style==11?.14f:.045f),.008f),.0008f,.0002f,6,4);
            }
        }
        Color green=style==3?new Color(.65f,.30f,.19f):style>=6&&style<=8?new Color(.19f,.43f,.12f):new Color(.36f,.60f,.14f);
        Model("Leaves",item.foliage,leaves.Save(name+"-leaves"),Nature(Ids[style]+"-leaf",green));
        Model("Stems",item.foliage,stems.Save(name+"-stems"),Nature("PlantStem",new Color(.19f,.30f,.07f)));
    }

    private static void BuildWood(AquariumDecorItem item,int style)
    {
        var species=DriftwoodSpeciesBuilder.Classic[style];
        Model("Wood",item.transform,Store(DriftwoodSpeciesBuilder.Build(species,1,out item.plantMounts,out item.footprint),Ids[style+12]),SpeciesWood());
    }

    private static void BuildRock(AquariumDecorItem item,int style)
    {
        string id=Ids[style+18];
        Model("Stone",item.transform,Store(StoneCoralBuilder.Build(Piece(id),1,out _),id),SpeciesStone());
        item.plantMounts=TopMounts(item.gameObject);
    }

    // The highest upward-facing points of a rock, spaced so attached moss or plants never overlap.
    private static Vector3[] TopMounts(GameObject root)
    {
        var candidates=new List<Vector3>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.sharedMesh==null)continue;
            var vertices=filter.sharedMesh.vertices;var normals=filter.sharedMesh.normals;
            if(normals.Length!=vertices.Length)continue;
            for(int i=0;i<vertices.Length;i++)
            {
                var normal=root.transform.InverseTransformDirection(filter.transform.TransformDirection(normals[i])).normalized;
                if(normal.y>.6f)candidates.Add(root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i])));
            }
        }
        candidates.Sort((a,b)=>b.y.CompareTo(a.y));
        var chosen=new List<Vector3>();
        foreach(var c in candidates)
            if(chosen.Count<3&&chosen.TrueForAll(m=>new Vector2(m.x-c.x,m.z-c.z).magnitude>=.06f))chosen.Add(c);
        return chosen.ToArray();
    }

    // One pad mesh under the swaying pivot; vertex colours tint the shared white moss material.
    private static float BuildMoss(AquariumDecorItem item,MossSpeciesBuilder.Species species)
    {
        item.foliage=new GameObject("SwayingFoliage").transform;item.foliage.SetParent(item.transform,false);
        item.phase=species.id.Length*1.37f;
        var mesh=Store(MossSpeciesBuilder.Build(species,out float footprint),species.id+"-leaves");
        Model("Leaves",item.foliage,mesh,Nature("MossSpecies",Color.white));
        return footprint;
    }

    // Neutral bark shared by every driftwood species; each mesh's vertex colours give the wood its colour.
    private static Material SpeciesWood()
    {
        string texture=Art+"Textures/DriftwoodGrey-v3.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(texture);
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
        importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=1024;
        importer.SaveAndReimport();
        string path=Root+"/DriftwoodSpecies.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Lumaquarium/Aquarium Hardscape"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Tiling",2);material.SetFloat("_UVMode",1);material.SetFloat("_Roughness",.6f);
        EditorUtility.SetDirty(material);return material;
    }

    // RiverStone-v3 grain tinted back to neutral grey (its mean is a warm .47), so vertex colours read true.
    private static Material SpeciesStone()
    {
        string path=Root+"/StoneSpecies.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(AssetDatabase.LoadAssetAtPath<Material>(Art+"RiverStone-v3.mat"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",new Color(1.5f,1.56f,1.69f));material.SetFloat("_Tiling",4);material.SetFloat("_UVMode",0);material.SetFloat("_Roughness",.75f);
        EditorUtility.SetDirty(material);return material;
    }

    // Live and silicone corals: no grain, a little gloss; colour comes from vertex colours only.
    private static Material SmoothCoral()
    {
        string path=Root+"/CoralSmooth.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Lumaquarium/Aquarium Hardscape"));AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",null);material.SetColor("_BaseColor",Color.white);material.SetFloat("_UVMode",0);material.SetFloat("_Roughness",.35f);
        EditorUtility.SetDirty(material);return material;
    }

    private static Material Nature(string name,Color color)
    {
        string path=Root+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Lumaquarium/Aquarium Nature"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Detail",.18f);material.SetFloat("_Scale",95);EditorUtility.SetDirty(material);return material;
    }
    private static void Model(string name,Transform parent,Mesh mesh,Material material)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
    }

    private sealed partial class Geometry
    {
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<Vector2> uvs=new List<Vector2>();
        private readonly List<int> triangles=new List<int>();
        private readonly List<Color> colors=new List<Color>();
        // Vertex colour for what is added next; white keeps the material colour as is.
        public Color paint=Color.white;
        private void Vertex(Vector3 p,Vector2 uv){vertices.Add(p);uvs.Add(uv);colors.Add(paint);}
        public void Tube(Vector3 a,Vector3 b,Vector3 end,float r0,float r1,int rings,int sides)
        {
            int start=vertices.Count;
            for(int ring=0;ring<=rings;ring++)
            {
                float t=ring/(float)rings;
                Vector3 center=(1-t)*(1-t)*a+2*(1-t)*t*b+t*t*end;
                Vector3 tangent=(2*(1-t)*(b-a)+2*t*(end-b)).normalized;
                Vector3 right=Vector3.Cross(tangent,Mathf.Abs(tangent.z)<.9f?Vector3.forward:Vector3.up).normalized;
                Vector3 forward=Vector3.Cross(right,tangent).normalized;
                for(int side=0;side<=sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    float r=Mathf.Lerp(r0,r1,t)*(1+.09f*Mathf.Sin(angle*5+t*17));
                    Vertex(center+(right*Mathf.Cos(angle)+forward*Mathf.Sin(angle))*r,new Vector2(side/(float)sides,t*3));
                    if(ring<rings&&side<sides){int i=start+ring*(sides+1)+side;triangles.AddRange(new[]{i,i+sides+1,i+1,i+1,i+sides+1,i+sides+2});}
                }
            }
            int bottom=vertices.Count;Vertex(a,Vector2.zero);int top=vertices.Count;Vertex(end,Vector2.one);
            for(int side=0;side<sides;side++){triangles.AddRange(new[]{bottom,start+side+1,start+side});int i=start+rings*(sides+1)+side;triangles.AddRange(new[]{top,i,i+1});}
        }
        public void Blade(Vector3 a,Vector3 end,float width,Vector3 normal,int rows)
        {
            int start=vertices.Count;Vector3 direction=end-a;Vector3 side=Vector3.Cross(direction,normal).normalized;
            if(side.sqrMagnitude<.1f) side=Vector3.right;
            Vector3 cup=Vector3.Cross(side,direction).normalized;
            for(int row=0;row<=rows;row++)
            {
                float t=row/(float)rows;float w=Mathf.Sin(t*Mathf.PI)*width*.5f;
                Vector3 center=Vector3.Lerp(a,end,t)+cup*(Mathf.Sin(t*Mathf.PI)*width*.18f);
                Vertex(center-side*w,new Vector2(0,t));Vertex(center+side*w,new Vector2(1,t));
                if(row<rows){int i=start+row*2;triangles.AddRange(new[]{i,i+2,i+1,i+1,i+2,i+3,i+1,i+2,i,i+3,i+2,i+1});}
            }
        }
        public Mesh Save(string name)
        {
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return Store(mesh,name);
        }
    }

    private static Mesh Store(Mesh mesh,string name)
    {
        mesh.name=name;
        string path=Root+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        // Setters invalidate the native render buffers too. CopySerialized can leave old GPU
        // vertices alive while bounds already describe the rebuilt geometry, clipping portraits.
        old.Clear();old.vertices=mesh.vertices;old.normals=mesh.normals;old.uv=mesh.uv;
        old.colors=mesh.colors;old.triangles=mesh.triangles;old.RecalculateBounds();
        UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
    }

    private static Sprite Portrait(GameObject prefab,string id)
    {
        string image=Root+"/"+id+".png";
        BakePortrait(prefab,image);
        AssetDatabase.ImportAsset(image,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(image);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(image);
    }

    private static void BakePortrait(GameObject prefab,string path)
    {
        var clone=UnityEngine.Object.Instantiate(prefab);var cameraObject=new GameObject("DecorPortraitCamera");
        var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(512,384,24,RenderTextureFormat.ARGB32);Texture2D texture=null;
        try
        {
            clone.transform.position=new Vector3(1000,0,0);clone.transform.rotation=Quaternion.Euler(0,-25,0);
            foreach(var t in clone.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            foreach(var b in clone.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
            var bounds=new Bounds(clone.transform.position,Vector3.one*.01f);
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
            var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<31;camera.orthographic=true;camera.aspect=4f/3;
            camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x/camera.aspect)*.70f;
            camera.transform.position=bounds.center+new Vector3(0,.9f,-2);camera.transform.LookAt(bounds.center);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.targetTexture=target;camera.Render();
            RenderTexture.active=target;texture=new Texture2D(512,384,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,512,384),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(clone);}
    }
}
