using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Twelve plants approved on 2026-10-27, offered after the climbing carpets.
// Colour lives in vertex colours, so each species keeps one white leaf material.
public static partial class AquariumDecorCatalogBuilder
{
    public const string NewPlantsPreview="docs/previews/NewPlants";

    private sealed class NewPlant
    {
        public string id, name, description; public int price, kind, points; public AquariumDecorCategory category;
        public Action<AquariumDecorItem> build;
    }

    // Kinds 110+ follow the climbing carpets (100–103), so no released kind moves.
    private static NewPlant[] NewPlants => new[]
    {
        new NewPlant { id="ludwigia-red", name="Rau mương đỏ Super Red", price=150, kind=110, points=1, category=AquariumDecorCategory.Plants,
            description="Bụi thân đứng, lá tròn chuyển từ xanh sang đỏ rượu ở ngọn · Trồng hậu cảnh · 1 điểm", build=BuildLudwigia },
        new NewPlant { id="water-wisteria", name="Thuỷ cúc", price=110, kind=111, points=1, category=AquariumDecorCategory.Plants,
            description="Lá xẻ thuỳ như ren, xanh non · Trồng hậu cảnh · 1 điểm", build=BuildWisteria },
        new NewPlant { id="anubias-hammock", name="Ráy lá võng", price=160, kind=112, points=1, category=AquariumDecorCategory.Plants,
            description="Một lá to nằm ngang gần mặt nước cho betta nghỉ · Hậu, trung cảnh · 1 điểm", build=BuildHammock },
        new NewPlant { id="tiger-lotus-red", name="Súng hổ đỏ", price=260, kind=113, points=2, category=AquariumDecorCategory.Plants,
            description="Bụi lá đỏ tía có vân, thêm lá súng vươn lên gần mặt nước · Điểm nhấn trung cảnh · 2 điểm", build=BuildTigerLotus },
        new NewPlant { id="madagascar-lace", name="Lưới Madagascar", price=320, kind=114, points=2, category=AquariumDecorCategory.Plants,
            description="Lá dài như tấm lưới đan, nhìn xuyên qua được · Cây hiếm cho trung cảnh · 2 điểm", build=BuildLace },
        new NewPlant { id="crypt-wendtii", name="Ráy Crypt nâu", price=100, kind=115, points=1, category=AquariumDecorCategory.Plants,
            description="Bụi lá thuôn mép gợn sóng màu đồng nâu · Trung cảnh · 1 điểm", build=BuildCrypt },
        new NewPlant { id="alternanthera-mini", name="Huyết tâm lan mini", price=130, kind=116, points=1, category=AquariumDecorCategory.Plants,
            description="Bụi rậm lá nhỏ hồng đỏ · Thêm màu ấm giữa hồ · 1 điểm", build=BuildAlternanthera },
        new NewPlant { id="bucephalandra", name="Ráy Buce xanh ánh kim", price=140, kind=117, points=1, category=AquariumDecorCategory.Plants,
            description="Lá nhỏ xanh thẫm ánh xanh dương, lá non đỏ nâu · Trồng trên nền hoặc kéo lên lũa, đá để gắn", build=BuildBuce },
        new NewPlant { id="downoi", name="Downoi lá xoăn", price=90, kind=118, points=1, category=AquariumDecorCategory.Plants,
            description="Các cụm hoa thị nhỏ lá xoăn xanh tươi · Trồng tiền cảnh", build=BuildDownoi },
        new NewPlant { id="red-root-floater", name="Bèo rễ đỏ", price=70, kind=119, points=1, category=AquariumDecorCategory.Floating,
            description="Lá tròn nhỏ đỏ, rễ đỏ buông xuống nước · Kéo để sắp xếp từng cụm", build=BuildRedRootFloater },
        new NewPlant { id="water-lettuce", name="Bèo cái mini", price=80, kind=120, points=1, category=AquariumDecorCategory.Floating,
            description="Hoa thị lá nhung có gân, rễ tua dài, tạo bóng râm cho betta · Kéo để sắp xếp từng cụm", build=BuildWaterLettuce },
        new NewPlant { id="mosaic-plant", name="Bèo khảm đỏ", price=190, kind=121, points=1, category=AquariumDecorCategory.Plants,
            description="Thân dài từ đáy, nở hoa thị lá hình thoi như tranh khảm gần mặt nước · 1 điểm", build=BuildMosaic },
    };

    // Rebuilt plants keep a price already edited in the catalog, like the coral collection.
    private static void AddNewPlants(List<AquariumDecorOffer> offers)
    {
        foreach(var plant in NewPlants)
        {
            if(offers.Any(o=>o.id!=plant.id && o.prefab.kind==plant.kind))
                throw new InvalidOperationException("Plant kind conflicts with a released item: "+plant.kind);
            var previous=offers.Find(o=>o.id==plant.id);var built=new List<AquariumDecorOffer>();
            AddPiece(built,plant.id,plant.name,previous!=null?previous.price:plant.price,plant.category,plant.description,plant.kind,plant.points,plant.build);
            if(previous!=null)offers[offers.IndexOf(previous)]=built[0];else offers.Add(built[0]);
        }
    }

    [MenuItem("Lumaquarium/Art/Install Approved New Plants")]
    public static void InstallNewPlants()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/MainAquarium.unity")throw new InvalidOperationException("Open MainAquarium first.");
        var catalog=AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(Root+"/AquariumDecorCatalog.asset");
        if(catalog==null)throw new InvalidOperationException("Build the decoration catalog first.");
        string backup="Backups/NewPlants/"+SceneBackups.Stamp();Directory.CreateDirectory(backup);
        EditorSceneManager.SaveScene(scene,backup+"/MainAquarium.unity",true);
        foreach(var path in new[]{Root,"Assets/_Project/UI/Prefabs","Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab"})
        {
            if(Directory.Exists(path))
                foreach(string file in Directory.GetFiles(path))File.Copy(file,backup+"/"+Path.GetFileName(file),true);
            else if(File.Exists(path))File.Copy(path,backup+"/"+Path.GetFileName(path),true);
        }
        var offers=catalog.offers.ToList();AddNewPlants(offers);catalog.offers=offers.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        ServerCatalogWriter.Write();
        DecorShopScreenBuilder.RefreshCatalogViews(catalog);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Installed twelve approved plants. Backups: "+backup);
    }

    [MenuItem("Lumaquarium/Art/Build New Plants Preview")]
    public static void BuildNewPlantsPreview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        // Portraits take the open scene's lighting; a scene without lights gets a temporary studio light.
        GameObject studio=null;
        if(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length==0)
        {
            studio=new GameObject("PortraitStudioLight");var light=studio.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.2f;studio.transform.rotation=Quaternion.Euler(50,-30,0);
        }
        var offers=new List<AquariumDecorOffer>();
        try
        {
            foreach(var plant in NewPlants)
                AddPiece(offers,plant.id,plant.name,plant.price,plant.category,plant.description,plant.kind,plant.points,plant.build);
        }
        finally{if(studio!=null)UnityEngine.Object.DestroyImmediate(studio);}
        AssetDatabase.SaveAssets();
        WriteNewPlantsSheet();
    }

    // All portraits on one dark-water sheet, four across, in the order of NewPlants.
    private static void WriteNewPlantsSheet()
    {
        Directory.CreateDirectory(NewPlantsPreview);
        const int width=512,height=384,columns=4;var plants=NewPlants;int rows=(plants.Length+columns-1)/columns;
        var background=new Color(.06f,.18f,.2f);
        var sheet=new Texture2D(width*columns,height*rows,TextureFormat.RGB24,false);
        var fill=new Color[width*columns*height*rows];for(int i=0;i<fill.Length;i++)fill[i]=background;sheet.SetPixels(fill);
        for(int i=0;i<plants.Length;i++)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                image.LoadImage(File.ReadAllBytes(Root+"/"+plants[i].id+".png"));
                if(image.width!=width||image.height!=height)continue;
                var source=image.GetPixels();int x0=i%columns*width,y0=(rows-1-i/columns)*height;
                var block=sheet.GetPixels(x0,y0,width,height);
                for(int p=0;p<block.Length;p++)block[p]=Color.Lerp(block[p],source[p],source[p].a);
                sheet.SetPixels(x0,y0,width,height,block);
            }
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
        sheet.Apply();File.WriteAllBytes(NewPlantsPreview+"/new-plants-sheet.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
    }

    // sin(pi*t) dips just below zero at t=1, which turns Pow and Sqrt into NaN.
    private static float Sin01(float t)=>Mathf.Max(0,Mathf.Sin(Mathf.PI*t));
    private static readonly Func<float,float> Lance=t=>Mathf.Pow(Sin01(t),.85f)*(1-t*.25f);
    private static readonly Func<float,float> Oval=t=>Mathf.Sqrt(Sin01(t));
    private static readonly Func<float,float> Oblong=t=>Mathf.Pow(Sin01(t),.45f);
    // Wide lobed base narrowing to the tip, like a lily leaf seen from the side.
    private static readonly Func<float,float> Arrow=t=>Mathf.Sin(Mathf.PI*Mathf.Lerp(.3f,1,Mathf.Pow(t,.8f)));
    private static readonly Func<float,float> Spoon=t=>Mathf.Sin(Mathf.PI*Mathf.Pow(t,1.8f));
    private static readonly Func<float,float> Diamond=t=>1-Mathf.Abs(2*t-1);
    private static readonly Func<float,float> Strap=t=>Mathf.Min(1,Mathf.Sin(Mathf.PI*t)*3);

    private static Vector3 Radial(float angle)=>new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
    private static Vector3 Bez(Vector3 a,Vector3 b,Vector3 c,float t)=>(1-t)*(1-t)*a+2*(1-t)*t*b+t*t*c;
    private static Color Rgb(float r,float g,float b)=>new Color(r,g,b);

    private sealed partial class Geometry
    {
        private void Quad(int i){triangles.AddRange(new[]{i,i+2,i+1,i+1,i+2,i+3,i+1,i+2,i,i+3,i+2,i+1});}

        // A leaf whose half-width follows the profile: its tip droops, its margin ripples, its colour runs base to tip.
        public void Leaf(Vector3 a,Vector3 end,float width,Vector3 normal,int rows,Func<float,float> profile,Color from,Color to,float droop=0,float wave=0)
        {
            int start=vertices.Count;Vector3 direction=end-a;float length=direction.magnitude;
            Vector3 side=Vector3.Cross(direction,normal).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;
            Vector3 face=Vector3.Cross(side,direction).normalized;
            for(int row=0;row<=rows;row++)
            {
                float t=row/(float)rows,w=profile(t)*width*.5f,ripple=wave*w*Mathf.Sin(t*rows*1.9f);
                paint=Color.Lerp(from,to,t);
                Vector3 center=Vector3.Lerp(a,end,t)+Vector3.down*(droop*length*t*t);
                Vertex(center-side*w-face*(w*.3f-ripple),new Vector2(0,t));
                Vertex(center+side*w-face*(w*.3f+ripple),new Vector2(1,t));
                if(row<rows)Quad(start+row*2);
            }
            paint=Color.white;
        }

        // A thin strip along a path, lying in the plane facing the given normal.
        public void Ribbon(IList<Vector3> path,float width,Vector3 face,Color color)
        {
            int start=vertices.Count;paint=color;
            for(int i=0;i<path.Count;i++)
            {
                Vector3 tangent=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(i-1,0)]).normalized;
                Vector3 side=Vector3.Cross(tangent,face).normalized*(width*.5f);
                Vertex(path[i]-side,new Vector2(0,i));Vertex(path[i]+side,new Vector2(1,i));
                if(i<path.Count-1)Quad(start+i*2);
            }
            paint=Color.white;
        }

        // A lattice leaf: lengthwise veins crossed by short ones, with open windows between them.
        public void LatticeLeaf(Vector3 a,Vector3 end,float width,Vector3 normal,Func<float,float> profile,Color color,float droop)
        {
            Vector3 direction=end-a;float length=direction.magnitude;
            Vector3 side=Vector3.Cross(direction,normal).normalized;Vector3 face=Vector3.Cross(side,direction).normalized;
            Func<float,float,Vector3> at=(t,k)=>Vector3.Lerp(a,end,t)+Vector3.down*(droop*length*t*t)
                +side*(profile(t)*width*.5f*k)+face*(Mathf.Sin(t*9)*.006f);
            const int steps=18;
            foreach(float k in new[]{-1f,-.6f,-.2f,.2f,.6f,1f})
            {
                var path=new List<Vector3>();for(int i=0;i<=steps;i++)path.Add(at(i/(float)steps,k));
                Ribbon(path,Mathf.Abs(k)>.9f?.004f:.0028f,face,Color.Lerp(color,Color.white,.08f));
            }
            for(int j=1;j<22;j++)
            {
                float t=j/22f;var path=new List<Vector3>();for(int i=0;i<=4;i++)path.Add(at(t,-1+i*.5f));
                Ribbon(path,.0022f,face,color);
            }
        }

        // A flat round leaf with a notch, like a lily pad, facing up.
        public void Pad(Vector3 center,float radius,float notchAngle,Color color)
        {
            int start=vertices.Count;paint=color;const int segments=24;
            Vertex(center,new Vector2(.5f,.5f));
            for(int i=0;i<=segments;i++)
            {
                float a=notchAngle+.25f+i*(Mathf.PI*2-.5f)/segments;
                Vertex(center+Radial(a)*radius*(1+.03f*Mathf.Sin(a*7)),new Vector2(i/(float)segments,1));
                if(i>0)triangles.AddRange(new[]{start,start+i+1,start+i,start,start+i,start+i+1});
            }
            paint=Color.white;
        }
    }

    // Leaves and stems on a swaying pivot; both take their colour from vertex colours.
    private static void Foliage(AquariumDecorItem item,string id,Geometry leaves,Geometry stems,float detail=.18f)
    {
        item.foliage=new GameObject("SwayingFoliage").transform;item.foliage.SetParent(item.transform,false);
        item.phase=id.Length*1.37f;
        var leaf=Nature(id+"-leaf",Color.white);leaf.SetFloat("_Detail",detail);
        Model("Leaves",item.foliage,leaves.Save(id+"-leaves"),leaf);
        Model("Stems",item.foliage,stems.Save(id+"-stems"),Nature("PlantStemPainted",Color.white));
    }

    // Upright stems in a clump; node(position, fraction up the stem, node index, stem index) adds the leaves.
    private static void Stems(Geometry stems,System.Random random,int count,float radius,float minHeight,float maxHeight,
        float spacing,float thickness,Color color,Action<Vector3,float,int,int> node)
    {
        for(int n=0;n<count;n++)
        {
            float r=(float)random.NextDouble(),h=Mathf.Lerp(minHeight,maxHeight,(float)random.NextDouble());
            Vector3 root=Radial(n*2.39996f)*Mathf.Sqrt((n+.5f)/count)*radius;
            Vector3 lean=(root.normalized*.6f+Radial(r*6.28f)*.4f)*h*.12f;
            Vector3 mid=root+lean*.3f+Vector3.up*h*.5f,end=root+lean+Vector3.up*h;
            stems.paint=color;stems.Tube(root,mid,end,thickness,thickness*.6f,Mathf.Max(6,Mathf.RoundToInt(h*30)),5);stems.paint=Color.white;
            int nodes=Mathf.Max(2,Mathf.RoundToInt(h/spacing));
            for(int k=1;k<=nodes;k++){float f=k/(nodes+.5f);node(Bez(root,mid,end,f),f,k,n);}
        }
    }

    private static void BuildLudwigia(AquariumDecorItem item)
    {
        item.footprint=.16f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1101);
        Color green=Rgb(.36f,.52f,.20f),red=Rgb(.62f,.10f,.16f);
        Stems(stems,random,9,.06f,.7f,1.1f,.05f,.0045f,Rgb(.42f,.16f,.12f),(p,f,k,n)=>
        {
            Color c=Color.Lerp(green,red,Mathf.Pow(f,1.1f));float len=Mathf.Lerp(.085f,.05f,f);
            for(int s=0;s<2;s++)
            {
                Vector3 dir=(Radial(n*1.3f+k*Mathf.PI*.5f+s*Mathf.PI)+Vector3.up*.55f).normalized;
                leaves.Leaf(p,p+dir*len,len*.75f,Vector3.up,7,Oval,c*.8f,c,.15f);
            }
        });
        Foliage(item,"ludwigia-red",leaves,stems);
    }

    private static void BuildWisteria(AquariumDecorItem item)
    {
        item.footprint=.17f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1102);
        Color light=Rgb(.48f,.74f,.26f),deep=Rgb(.34f,.58f,.18f);
        Stems(stems,random,6,.06f,.6f,.92f,.075f,.005f,Rgb(.30f,.48f,.16f),(p,f,k,n)=>
        {
            float len=Mathf.Lerp(.15f,.09f,f);
            for(int s=0;s<2;s++)
            {
                Vector3 dir=(Radial(n*1.7f+k*Mathf.PI*.5f+s*Mathf.PI)+Vector3.up*.35f).normalized;
                leaves.Leaf(p,p+dir*len,.005f,Vector3.up,4,Strap,deep,deep);
                Vector3 across=Vector3.Cross(dir,Vector3.up).normalized;
                for(int j=1;j<=5;j++)
                {
                    Vector3 q=p+dir*len*(j/6f);float l=len*.42f*(1-j*.1f);
                    foreach(float side in new[]{-1f,1f})
                    {
                        Vector3 d=(across*side+dir*.6f+Vector3.up*.1f).normalized;
                        leaves.Leaf(q,q+d*l,l*.5f,Vector3.up,4,Lance,deep,light);
                    }
                }
            }
        });
        Foliage(item,"water-wisteria",leaves,stems);
    }

    private static void BuildHammock(AquariumDecorItem item)
    {
        item.footprint=.2f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1103);
        Color dark=Rgb(.11f,.28f,.12f),tip=Rgb(.20f,.42f,.18f),stem=Rgb(.20f,.34f,.14f);
        stems.paint=Rgb(.30f,.28f,.16f);stems.Tube(new Vector3(-.07f,.012f,0),new Vector3(0,.02f,.01f),new Vector3(.07f,.012f,0),.012f,.009f,8,7);
        for(int n=0;n<7;n++)
        {
            float a=n*2.39996f,r=(float)random.NextDouble();Vector3 start=new Vector3(-.06f+n*.02f,.015f,0);
            Vector3 petiole=start+Radial(a)*.03f+Vector3.up*(.05f+r*.06f);
            stems.paint=stem;stems.Tube(start,(start+petiole)*.5f,petiole,.003f,.0025f,4,5);
            Vector3 dir=(Radial(a)*.7f+Vector3.up).normalized;
            leaves.Leaf(petiole,petiole+dir*(.09f+r*.04f),.06f,Radial(a),10,Oval,dark,tip,.2f);
        }
        // The hammock: one long petiole holding a broad leaf flat near the surface.
        Vector3 top=new Vector3(.08f,.88f,.02f);
        stems.paint=stem;stems.Tube(new Vector3(0,.015f,0),new Vector3(.06f,.45f,0),top,.0055f,.004f,16,6);
        leaves.Leaf(top-Radial(.3f)*.03f,top+Radial(.3f)*.24f,.15f,Vector3.up,14,Oval,dark,tip,-.04f);
        Foliage(item,"anubias-hammock",leaves,stems);
    }

    private static void BuildTigerLotus(AquariumDecorItem item)
    {
        item.footprint=.24f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1104);
        Color deep=Rgb(.40f,.06f,.12f),bright=Rgb(.74f,.20f,.20f),stem=Rgb(.45f,.14f,.16f);
        for(int n=0;n<11;n++)
        {
            float a=n*2.39996f,r=(float)random.NextDouble(),lean=.3f+.6f*(n%4)/3f;
            Vector3 petiole=Radial(a)*(.03f+lean*.05f)+Vector3.up*(.03f+r*.05f);
            stems.paint=stem;stems.Tube(Vector3.zero,petiole*.5f+Vector3.up*.015f,petiole,.004f,.003f,5,5);
            Vector3 dir=(Radial(a)*(.5f+lean*.5f)+Vector3.up*(.8f-lean*.5f)).normalized;
            leaves.Leaf(petiole,petiole+dir*(.2f+r*.08f),.17f,Radial(a),14,Arrow,deep,bright,.1f);
        }
        // Two leaves climb to lie near the surface as round pads.
        foreach(float a in new[]{.8f,3.6f})
        {
            Vector3 end=Radial(a)*.28f+Vector3.up*(a<2?1.72f:1.6f);
            stems.paint=stem;stems.Tube(Vector3.zero,Radial(a)*.12f+Vector3.up*.9f,end,.004f,.0035f,24,5);
            leaves.Pad(end,.13f,a,Rgb(.62f,.14f,.18f));
        }
        Foliage(item,"tiger-lotus-red",leaves,stems,.45f);
    }

    private static void BuildLace(AquariumDecorItem item)
    {
        item.footprint=.26f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1105);
        stems.paint=Rgb(.40f,.30f,.18f);stems.Tube(Vector3.zero,Vector3.up*.018f,Vector3.up*.032f,.022f,.012f,6,8);
        Color olive=Rgb(.40f,.48f,.20f);
        for(int n=0;n<9;n++)
        {
            float a=n*2.39996f,r=(float)random.NextDouble(),lean=.25f+.55f*(n%3)/2f;
            Vector3 petiole=Radial(a)*(.03f+lean*.05f)+Vector3.up*(.06f+r*.07f);
            stems.paint=Rgb(.34f,.40f,.16f);stems.Tube(Vector3.up*.02f,petiole*.5f+Vector3.up*.02f,petiole,.0035f,.0028f,6,5);
            Vector3 dir=(Radial(a)*lean+Vector3.up*(1-lean*.45f)).normalized;
            leaves.LatticeLeaf(petiole,petiole+dir*(.28f+r*.14f),.08f,Radial(a),Oblong,Color.Lerp(olive,Rgb(.52f,.42f,.20f),r),.25f*lean);
        }
        Foliage(item,"madagascar-lace",leaves,stems);
    }

    private static void BuildCrypt(AquariumDecorItem item)
    {
        item.footprint=.14f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1106);
        for(int n=0;n<18;n++)
        {
            float a=n*2.39996f,r=(float)random.NextDouble(),lean=.2f+.55f*(n%5)/4f;
            Vector3 petiole=Radial(a)*.012f+Vector3.up*(.02f+r*.02f);
            stems.paint=Rgb(.30f,.22f,.12f);stems.Tube(Vector3.zero,petiole*.5f,petiole,.003f,.0025f,4,5);
            Vector3 dir=(Radial(a)*lean+Vector3.up*(1-lean*.4f)).normalized;
            leaves.Leaf(petiole,petiole+dir*(.13f+r*.08f),.05f,Radial(a),20,Lance,Rgb(.28f,.22f,.10f),Rgb(.50f,.36f,.17f),.3f*lean,.12f);
        }
        Foliage(item,"crypt-wendtii",leaves,stems,.3f);
    }

    private static void BuildAlternanthera(AquariumDecorItem item)
    {
        item.footprint=.13f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1107);
        Color pink=Rgb(.74f,.20f,.32f),wine=Rgb(.48f,.10f,.26f);
        Stems(stems,random,18,.07f,.1f,.18f,.024f,.0025f,Rgb(.42f,.12f,.20f),(p,f,k,n)=>
        {
            for(int s=0;s<2;s++)
            {
                Vector3 dir=(Radial(n*2.1f+k*Mathf.PI*.5f+s*Mathf.PI)+Vector3.up*.7f).normalized;
                leaves.Leaf(p,p+dir*.048f,.02f,Vector3.up,6,Lance,wine,pink,.15f);
            }
        });
        Foliage(item,"alternanthera-mini",leaves,stems);
    }

    private static void BuildBuce(AquariumDecorItem item)
    {
        item.footprint=.1f;item.attachable=true;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1108);
        stems.paint=Rgb(.16f,.15f,.10f);stems.Tube(new Vector3(-.04f,.006f,0),new Vector3(0,.01f,.005f),new Vector3(.04f,.006f,0),.004f,.0035f,8,6);
        for(int n=0;n<18;n++)
        {
            float a=n*2.39996f,r=(float)random.NextDouble();bool young=n%4==1;
            Vector3 start=new Vector3(-.035f+n*.004f,.012f,0),petiole=start+Radial(a)*.008f+Vector3.up*.012f;
            stems.paint=young?Rgb(.36f,.16f,.12f):Rgb(.12f,.20f,.18f);stems.Tube(start,(start+petiole)*.5f,petiole,.0018f,.0015f,3,4);
            Vector3 dir=(Radial(a)*.55f+Vector3.up).normalized;
            leaves.Leaf(petiole,petiole+dir*(.035f+r*.022f),.026f,Radial(a),8,Lance,
                young?Rgb(.32f,.11f,.08f):Rgb(.04f,.10f,.09f),young?Rgb(.48f,.18f,.12f):Rgb(.07f,.18f,.22f),.15f);
        }
        Foliage(item,"bucephalandra",leaves,stems);
    }

    private static void BuildDownoi(AquariumDecorItem item)
    {
        item.footprint=.12f;var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(1109);
        for(int p=0;p<7;p++)
        {
            Vector3 center=Radial(p*2.39996f)*Mathf.Sqrt((p+.5f)/7)*.08f;
            for(int n=0;n<11;n++)
            {
                float a=n*2.39996f+p,r=(float)random.NextDouble();
                Vector3 dir=(Radial(a)*.8f+Vector3.up*.6f).normalized;
                leaves.Leaf(center,center+dir*(.03f+r*.016f),.014f,Radial(a),14,Lance,Rgb(.38f,.62f,.20f),Rgb(.62f,.86f,.36f),.3f,.35f);
            }
        }
        stems.paint=Rgb(.30f,.45f,.16f);stems.Tube(Vector3.zero,Vector3.up*.004f,Vector3.up*.008f,.004f,.003f,2,4);
        Foliage(item,"downoi",leaves,stems);
    }

    private static void BuildRedRootFloater(AquariumDecorItem item)
    {
        item.footprint=.12f;item.floats=true;var leaves=new Geometry();var roots=new Geometry();var random=new System.Random(1110);
        for(int n=0;n<24;n++)
        {
            Vector3 center=Radial(n*2.39996f)*Mathf.Sqrt((n+.5f)/24)*.09f;
            for(int k=0;k<4;k++)
            {
                float b=k*1.57f+n;bool red=random.NextDouble()<.55;
                Color c=red?Rgb(.62f,.14f,.10f):Rgb(.38f,.50f,.18f);
                leaves.Leaf(center,center+Radial(b)*.014f,.012f,Vector3.up,4,Oval,c*.85f,c);
            }
            for(int k=0;k<3;k++)
            {
                float depth=.03f+(float)random.NextDouble()*.035f;Vector3 drift=Radial(n+k*2.1f)*.006f;
                roots.paint=Rgb(.56f,.14f,.10f);roots.Tube(center,center+drift+Vector3.down*depth*.5f,center+drift*1.5f+Vector3.down*depth,.0008f,.0003f,5,3);
            }
        }
        Foliage(item,"red-root-floater",leaves,roots);
    }

    private static void BuildWaterLettuce(AquariumDecorItem item)
    {
        item.footprint=.14f;item.floats=true;var leaves=new Geometry();var roots=new Geometry();var random=new System.Random(1111);
        for(int p=0;p<6;p++)
        {
            Vector3 center=Radial(p*2.39996f)*Mathf.Sqrt((p+.5f)/6)*.075f;
            for(int n=0;n<8;n++)
            {
                float a=n*.785f+p,r=(float)random.NextDouble();
                Vector3 dir=(Radial(a)+Vector3.up*(.35f+r*.2f)).normalized;
                leaves.Leaf(center,center+dir*(.038f+r*.012f),.036f,Radial(a),8,Spoon,Rgb(.50f,.66f,.36f),Rgb(.70f,.84f,.52f));
            }
            for(int k=0;k<9;k++)
            {
                float depth=.07f+(float)random.NextDouble()*.07f;Vector3 drift=Radial(k*.7f+p)*.025f;
                roots.paint=Rgb(.46f,.44f,.34f);roots.Tube(center,center+drift*.5f+Vector3.down*depth*.5f,center+drift+Vector3.down*depth,.0012f,.0005f,6,3);
            }
        }
        Foliage(item,"water-lettuce",leaves,roots,.3f);
    }

    private static void BuildMosaic(AquariumDecorItem item)
    {
        item.footprint=.16f;var leaves=new Geometry();var stems=new Geometry();
        Vector3 top=Vector3.up*1.74f;
        stems.paint=Rgb(.50f,.14f,.14f);stems.Tube(Vector3.zero,new Vector3(.08f,.9f,.04f),top,.005f,.0035f,30,5);
        const int count=34;
        for(int n=0;n<count;n++)
        {
            float a=n*2.39996f,f=n/(float)count,radius=.03f+.17f*Mathf.Sqrt(f);
            Color c=Color.Lerp(Rgb(.32f,.52f,.16f),Rgb(.74f,.16f,.12f),Mathf.Sqrt(f));
            Vector3 leaf=top+Radial(a)*radius+Vector3.down*.01f*f;
            leaves.Leaf(top,leaf,.003f,Vector3.up,3,Strap,Rgb(.45f,.16f,.14f),Rgb(.45f,.16f,.14f));
            leaves.Leaf(leaf,leaf+Radial(a)*.065f,.055f,Vector3.up,4,Diamond,c*.85f,c);
        }
        Foliage(item,"mosaic-plant",leaves,stems);
    }
}
