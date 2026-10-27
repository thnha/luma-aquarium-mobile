using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Pieces tall enough to fill the water column (sand 0.31, surface 2.42): Vừa and Lớn sizes of every
// named driftwood and stone, an old-growth trunk that reaches toward the surface, a standing boulder,
// a flat slab to stack on, and background plants. Each costs points against the tank's capacity.
public static partial class AquariumDecorCatalogBuilder
{
    private sealed class Size { public string id, name; public float scale, price; public int points; }
    private static readonly Size[] Sizes =
    {
        new Size { id="m", name="Vừa", scale=1.8f, price=2, points=2 },
        new Size { id="l", name="Lớn", scale=3, price=3.5f, points=3 },
    };
    // Tallest a piece may stand, leaving room under the surface for the fish to breathe.
    private const float MaxHeight = 1.95f;
    // Widest a piece may spread, so a large one still fits the 1.48-wide tank with room beside it.
    private const float MaxSpread = 1.2f;

    private static readonly DriftwoodSpeciesBuilder.Species OldGrowth = new DriftwoodSpeciesBuilder.Species
    {
        id="wood-co-thu", name="Lũa cổ thụ đứng", price=900, shape=DriftwoodSpeciesBuilder.Shape.Upright,
        description="Thân to vươn cao gần chạm mặt nước, ngả được bằng nút Nghiêng · 6 vị trí gắn rêu, ráy · 3 điểm",
        depth=3, len=new[]{1.45f,.5f,.22f,.09f}, r0=.1f, rDec=new[]{.45f,.5f,.55f}, kids=new[]{5,3,2}, start=new[]{.35f,.3f,.3f},
        spread=new[]{new Vector2(30,55),new Vector2(25,50),new Vector2(25,55)}, up=new[]{.02f,.05f,.06f,.07f}, wander=new[]{.12f,.2f,.24f,.28f},
        curl=1.6f, ridgeK=9, ridge=.12f, sharp=2.5f, twist=4, lump=.16f, lumpF=5, tip=.22f, roots=6, rootLen=1.2f,
        colA=new Color(.36f,.24f,.16f), colB=new Color(.14f,.09f,.06f), tone=DriftwoodSpeciesBuilder.Tone.Grain
    };

    private static void AddLarge(List<AquariumDecorOffer> offers,int kind,Material speciesWood,Material speciesStone)
    {
        foreach (var species in DriftwoodSpeciesBuilder.All)
            foreach (var size in Sizes)
            {
                string id=species.id+"-"+size.id;
                AddPiece(offers,id,species.name+" · "+size.name,Price(species.price*size.price),AquariumDecorCategory.Wood,
                    $"Cỡ {size.name.ToLowerInvariant()} của {species.name} · Gắn cây hoặc rêu lên thân, ngả được bằng nút Nghiêng · {size.points} điểm",
                    kind++,size.points,item =>
                    {
                        item.supportsPlants=true;item.tiltable=true;
                        var raw=DriftwoodSpeciesBuilder.Build(species,1,out var mounts,out float footprint,5);
                        float scale=Fit(raw,size.scale);
                        var mesh=Scaled(raw,Vector3.one*scale,id);
                        item.plantMounts=Array.ConvertAll(mounts,m=>m*scale);
                        item.footprint=Mathf.Min(.42f,footprint*(1+(scale-1)*.5f));
                        Model("Wood",item.transform,mesh,speciesWood);
                    });
            }
        foreach (var piece in StoneCoralBuilder.All)
        {
            if (piece.IsCoral) continue;
            foreach (var size in Sizes)
            {
                string id=piece.id+"-"+size.id;
                AddPiece(offers,id,piece.name+" · "+size.name,Price(piece.price*size.price),AquariumDecorCategory.Rocks,
                    $"Cỡ {size.name.ToLowerInvariant()} của {piece.name} · Chồng đá, gác lũa hoặc gắn rêu lên mặt đá · {size.points} điểm",
                    kind++,size.points,item =>
                    {
                        var raw=StoneCoralBuilder.Build(piece,1,out float footprint);
                        float scale=Fit(raw,size.scale);
                        var mesh=Scaled(raw,Vector3.one*scale,id);
                        item.footprint=Mathf.Min(.45f,footprint*scale);
                        Model("Stone",item.transform,mesh,speciesStone);
                        item.supportsPlants=true;item.plantMounts=TopMounts(item.gameObject);
                    });
            }
        }
        AddPiece(offers,OldGrowth.id,OldGrowth.name,OldGrowth.price,AquariumDecorCategory.Wood,OldGrowth.description,kind++,3,item =>
        {
            item.supportsPlants=true;item.tiltable=true;
            var raw=DriftwoodSpeciesBuilder.Build(OldGrowth,1,out var mounts,out item.footprint,6);
            float fit=Fit(raw,1);
            var mesh=Scaled(raw,Vector3.one*fit,OldGrowth.id);
            item.plantMounts=Array.ConvertAll(mounts,m=>m*fit);
            Model("Wood",item.transform,mesh,speciesWood);
        });
        AddPiece(offers,"stone-iwagumi","Đá tảng chủ",700,AquariumDecorCategory.Rocks,
            "Tảng đá đứng cao làm điểm nhấn chính kiểu Iwagumi · Gắn rêu ở chân và đỉnh · 3 điểm",kind++,3,item =>
            {
                Model("Stone",item.transform,Fitted(Piece("stone-co-thach"),new Vector3(.46f,1f,.38f),"stone-iwagumi"),speciesStone);
                item.footprint=.22f;item.supportsPlants=true;item.plantMounts=TopMounts(item.gameObject);
            });
        AddPiece(offers,"stone-slab","Đá phiến bậc",300,AquariumDecorCategory.Rocks,
            "Mặt trên phẳng để chồng thêm đá hoặc gác lũa, dựng núi đá nhiều tầng · 2 điểm",kind++,2,item =>
            {
                Model("Stone",item.transform,Fitted(Piece("stone-elephant-skin"),SlabSize,"stone-slab"),speciesStone);
                item.footprint=.27f;item.supportsPlants=true;item.plantMounts=TopMounts(item.gameObject);
            });
        AddPiece(offers,"vallisneria-tall","Hẹ nước Vallisneria cao",180,AquariumDecorCategory.Plants,
            "Cây hậu cảnh lá dài gần chạm mặt nước · Trồng sát kính sau · 1 điểm",kind++,1,item =>
            { item.footprint=.16f;BuildPlant(item,2,3.6f,"vallisneria-tall"); });
        AddPiece(offers,"rotala-tall","Hồng liễu Rotala cao",200,AquariumDecorCategory.Plants,
            "Bụi thân đứng cao, ngọn đỏ cam · Trồng hậu cảnh · 1 điểm",kind++,1,item =>
            { item.footprint=.16f;BuildPlant(item,3,3f,"rotala-tall"); });
        AddPiece(offers,"amazon-sword","Kiếm Amazon",220,AquariumDecorCategory.Plants,
            "Bụi lá to hình lưỡi kiếm, làm điểm nhấn trung cảnh · Tỉa hoặc cho mọc cao · 2 điểm",kind++,2,item =>
            { item.footprint=.22f;BuildSword(item); });
        AddPiece(offers,"stone-cliff","Đá vách núi",800,AquariumDecorCategory.Rocks,
            "Khối đá dài nhiều tầng dựng sát kính sau, che phần hồ phía sau và tạo chiều sâu · 3 điểm",kind++,3,item =>
            {
                Model("Stone",item.transform,Fitted(Piece("stone-cliff"),CliffSize,"stone-cliff"),speciesStone);
                item.footprint=.3f;item.supportsPlants=true;item.plantMounts=TopMounts(item.gameObject);
            });
        string[] carpetIds={"carpet-hairgrass","carpet-monte-carlo"};
        string[] carpetNames={"Thảm Ngưu mao chiên","Thảm Trân châu Nhật"};
        int[] carpetPrices={60,70};
        for(int i=0;i<carpetIds.Length;i++)
        {
            int species=i+1;string id=carpetIds[i];
            AddPiece(offers,id,carpetNames[i],carpetPrices[i],AquariumDecorCategory.Moss,
                $"Gói {AquariumCarpet.PackCells} ô thảm tiền cảnh · Chọn rồi kéo trên cát để trải, Tẩy để xoá · Không tính điểm",kind++,0,
                item => BuildCarpet(item,species,id));
        }
    }

    // Climbing carpets grow on the sand, on stone and wood and on the back glass. They came after the coral
    // collection, on kinds 100+, so no released kind or offer moves.
    private sealed class Carpet { public string id, name, description; public int species, kind, price; }
    private static readonly Carpet[] Climbing =
    {
        new Carpet { id="carpet-hc-cuba", name="Thảm Trân châu Cuba", species=3, kind=100, price=80,
            description="Lá li ti xanh non phủ kín như nhung" },
        new Carpet { id="carpet-java-moss", name="Thảm rêu Java", species=4, kind=101, price=70,
            description="Rêu lông vũ xanh đậm bò sát mặt lũa, đá" },
        new Carpet { id="carpet-riccia", name="Thảm rêu Riccia", species=5, kind=102, price=75,
            description="Tản rêu chẻ nhánh xanh chanh, bám đá thành đệm" },
        new Carpet { id="carpet-marimo", name="Thảm tảo Marimo", species=6, kind=103, price=90,
            description="Tảo cầu xanh thẫm mọc thành đệm tròn mịn" },
    };

    private static void AddClimbingCarpets(List<AquariumDecorOffer> offers)
    {
        foreach (var carpet in Climbing)
        {
            if(offers.Exists(o=>o.prefab.kind==carpet.kind))
                throw new InvalidOperationException("Carpet kind conflicts with a released item: "+carpet.kind);
            AddPiece(offers,carpet.id,carpet.name,carpet.price,AquariumDecorCategory.Moss,
                $"{carpet.description} · Gói {AquariumCarpet.PackCells} ô · Kéo trên cát, lũa, đá hoặc kính sau để trải, Tẩy để xoá · Không tính điểm",
                carpet.kind,0,item => BuildCarpet(item,carpet.species,carpet.id));
        }
    }

    // Carpets are painted with the brush; the prefab is a patch for the shop portrait.
    private static void BuildCarpet(AquariumDecorItem item,int species,string id)
    {
        item.carpet=species;item.footprint=.1f;
        var vertices=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
        var random=new System.Random(species);
        for(int x=-2;x<=2;x++)for(int z=-1;z<=1;z++)
            AquariumCarpet.AddTuft(vertices,colors,triangles,species,new Vector3(x*.035f,0,z*.035f),random);
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        Model("Leaves",item.transform,Store(mesh,id),Nature("Carpet",Color.white));
    }

    // A rosette of broad blades: inner leaves stand upright, outer ones arch outward.
    private static void BuildSword(AquariumDecorItem item)
    {
        item.foliage=new GameObject("SwayingFoliage").transform;item.foliage.SetParent(item.transform,false);
        item.phase=2.1f;
        var leaves=new Geometry();var stems=new Geometry();var random=new System.Random(907);
        for(int n=0;n<16;n++)
        {
            float a=n*2.39996f,r=(float)random.NextDouble(),lean=.25f+.45f*(n%4)/3f;
            var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Vector3 petiole=radial*(.02f+lean*.06f)+Vector3.up*(.08f+r*.06f);
            stems.Tube(Vector3.zero,petiole*.5f+Vector3.up*.02f,petiole,.004f,.003f,5,5);
            Vector3 tip=petiole+(radial*lean+Vector3.up*(1-lean*.5f)).normalized*(.32f+r*.26f);
            leaves.Blade(petiole,tip,.07f+r*.03f,radial,12);
        }
        Model("Leaves",item.foliage,leaves.Save("amazon-sword-leaves"),Nature("amazon-sword-leaf",new Color(.33f,.62f,.16f)));
        Model("Stems",item.foliage,stems.Save("amazon-sword-stems"),Nature("PlantStem",new Color(.19f,.30f,.07f)));
    }

    // The wanted scale, reduced so the piece stays under the surface and inside the tank.
    private static float Fit(Mesh mesh,float scale)
    {
        var size=mesh.bounds.size;
        return Mathf.Min(scale,MaxHeight/size.y,MaxSpread/Mathf.Max(size.x,size.z));
    }

    private static int Price(float price) => Mathf.RoundToInt(price/10)*10;

    private static readonly Vector3 SlabSize=new Vector3(.62f,.16f,.48f),CliffSize=new Vector3(.95f,1.3f,.34f);

    private static StoneCoralBuilder.Piece Piece(string id) =>
        Array.Find(StoneCoralBuilder.All,x=>x.id==id) ?? Array.Find(StoneCoralBuilder.Redesign,x=>x.id==id)
        ?? throw new InvalidOperationException("Missing stone "+id);

    // Same steps as the other shop items: build, add a trigger box for picking, save, bake a portrait.
    private static void AddPiece(List<AquariumDecorOffer> offers,string id,string name,int price,AquariumDecorCategory category,
        string description,int kind,int points,Action<AquariumDecorItem> build)
    {
        var root=new GameObject(id);
        try
        {
            root.layer=8;
            var item=root.AddComponent<AquariumDecorItem>();
            item.kind=kind;item.points=points;
            build(item);
            var bounds=new Bounds(Vector3.zero,Vector3.one*.02f);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(renderer.bounds);
            var collider=root.AddComponent<BoxCollider>();collider.isTrigger=true;
            collider.center=bounds.center;collider.size=bounds.size+Vector3.one*.025f;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+id+".prefab").GetComponent<AquariumDecorItem>();
            offers.Add(new AquariumDecorOffer { id=id, displayName=name, price=price, prefab=prefab, category=category,
                description=description, portrait=Portrait(prefab.gameObject,id) });
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }

    // Stretches a stone to the given size.
    private static Mesh Fitted(StoneCoralBuilder.Piece piece,Vector3 size,string name)
    {
        var raw=StoneCoralBuilder.Build(piece,1,out _);
        var b=raw.bounds;
        return Scaled(raw,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z),name);
    }

    // A copy scaled about the pivot, so it sits in the sand exactly as the source does, saved under its own name.
    private static Mesh Scaled(Mesh source,Vector3 scale,string name)
    {
        var vertices=source.vertices;
        for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i],scale);
        var mesh=UnityEngine.Object.Instantiate(source);
        mesh.vertices=vertices;
        // A uniform scale keeps the builder's seam-matched normals; stretching needs fresh ones.
        if(!Mathf.Approximately(scale.x,scale.y)||!Mathf.Approximately(scale.y,scale.z))mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        UnityEngine.Object.DestroyImmediate(source);
        return Store(mesh,name);
    }
}
