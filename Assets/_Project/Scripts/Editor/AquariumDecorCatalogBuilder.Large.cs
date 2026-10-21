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
        depth=2, len=new[]{1.45f,.5f,.22f}, r0=.1f, rDec=new[]{.45f,.5f}, kids=new[]{5,3}, start=new[]{.35f,.3f},
        spread=new[]{new Vector2(30,55),new Vector2(25,50)}, up=new[]{.02f,.05f,.06f}, wander=new[]{.12f,.2f,.24f},
        curl=1.6f, ridgeK=9, ridge=.12f, sharp=2.5f, twist=4, lump=.16f, lumpF=5, tip=.22f,
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
                Model("Stone",item.transform,Fitted(Piece("stone-elephant-skin"),new Vector3(.62f,.16f,.48f),"stone-slab"),speciesStone);
                item.footprint=.27f;item.supportsPlants=true;item.plantMounts=TopMounts(item.gameObject);
            });
        AddPiece(offers,"vallisneria-tall","Hẹ nước Vallisneria cao",180,AquariumDecorCategory.Plants,
            "Cây hậu cảnh lá dài gần chạm mặt nước · Trồng sát kính sau · 1 điểm",kind++,1,item =>
            { item.footprint=.16f;BuildPlant(item,2,3.6f,"vallisneria-tall"); });
        AddPiece(offers,"rotala-tall","Hồng liễu Rotala cao",200,AquariumDecorCategory.Plants,
            "Bụi thân đứng cao, ngọn đỏ cam · Trồng hậu cảnh · 1 điểm",kind++,1,item =>
            { item.footprint=.16f;BuildPlant(item,3,3f,"rotala-tall"); });
    }

    // The wanted scale, reduced so the piece stays under the surface and inside the tank.
    private static float Fit(Mesh mesh,float scale)
    {
        var size=mesh.bounds.size;
        return Mathf.Min(scale,MaxHeight/size.y,MaxSpread/Mathf.Max(size.x,size.z));
    }

    private static int Price(float price) => Mathf.RoundToInt(price/10)*10;

    private static StoneCoralBuilder.Piece Piece(string id) =>
        Array.Find(StoneCoralBuilder.All,x=>x.id==id) ?? throw new InvalidOperationException("Missing stone "+id);

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
