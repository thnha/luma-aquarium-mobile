using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class AquariumDecorCatalogBuilder
{
    private const string StarterWoodPath="Assets/_Project/Art/Aquarium3D/BranchingDriftwoodNatural.prefab";

    // The redesigned species (and their Vừa/Lớn sizes); the six classic pieces and the starter are rebuilt too.
    private static readonly string[] RedesignedWoodSpecies = { "wood-mopani", "wood-san-da", "wood-linh-sam" };

    // Rebuilds only the redesigned driftwood in place: same prefab, mesh and portrait paths, so the catalog,
    // the shop cards and wood already placed in tanks pick up the new look without a full catalog build.
    [MenuItem("Lumaquarium/Art/Rebuild Redesigned Driftwood")]
    public static void RebuildRedesignedDriftwood()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        string backup="Backups/Driftwood/"+SceneBackups.Stamp();Directory.CreateDirectory(backup);
        var material=SpeciesWood();int count=0;
        WithPortraitLight(()=>
        {
            for(int style=0;style<DriftwoodSpeciesBuilder.Classic.Length;style++)
            {
                int s=style;
                RebuildWood(Ids[s+12],backup,item=>BuildWood(item,s));count++;
            }
            foreach(string id in RedesignedWoodSpecies)
            {
                var species=Array.Find(DriftwoodSpeciesBuilder.All,x=>x.id==id);
                RebuildWood(id,backup,item=>
                {
                    Model("Wood",item.transform,Store(DriftwoodSpeciesBuilder.Build(species,1,out item.plantMounts,out item.footprint),id),material);
                });count++;
                foreach(var size in Sizes)
                {
                    string sized=id+"-"+size.id;
                    RebuildWood(sized,backup,item=>
                    {
                        var raw=DriftwoodSpeciesBuilder.Build(species,1,out var mounts,out float footprint,5);
                        float scale=Fit(raw,size.scale);
                        Model("Wood",item.transform,Scaled(raw,Vector3.one*scale,sized),material);
                        item.plantMounts=Array.ConvertAll(mounts,m=>m*scale);
                        item.footprint=Mathf.Min(.42f,footprint*(1+(scale-1)*.5f));
                    });count++;
                }
            }
            RebuildWood(OldGrowth.id,backup,item=>
            {
                var raw=DriftwoodSpeciesBuilder.Build(OldGrowth,1,out var mounts,out item.footprint,6);
                float fit=Fit(raw,1);
                Model("Wood",item.transform,Scaled(raw,Vector3.one*fit,OldGrowth.id),material);
                item.plantMounts=Array.ConvertAll(mounts,m=>m*fit);
            });count++;
            Backup(StarterWoodPath,backup);
            var root=PrefabUtility.LoadPrefabContents(StarterWoodPath);
            try{ApplyStarterWood(root);PrefabUtility.SaveAsPrefabAsset(root,StarterWoodPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Backup(Root+"/starter-wood.png",backup);
            Portrait(AssetDatabase.LoadAssetAtPath<GameObject>(StarterWoodPath),"starter-wood");count++;
        });
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Rebuilt "+count+" redesigned driftwood pieces. Backups: "+backup);
    }

    // The starter keeps its prefab (placed tanks reference it) but takes a species mesh under its own name.
    private static void ApplyStarterWood(GameObject root)
    {
        var item=root.GetComponent<AquariumDecorItem>();
        var filter=root.GetComponentInChildren<MeshFilter>();
        filter.sharedMesh=Store(DriftwoodSpeciesBuilder.Build(DriftwoodSpeciesBuilder.Starter,1,out item.plantMounts,out item.footprint),"starter-wood");
        filter.GetComponent<MeshRenderer>().sharedMaterial=SpeciesWood();
        FitCollider(root);
    }

    private static void RebuildWood(string id,string backup,Action<AquariumDecorItem> build)
    {
        string path=Root+"/"+id+".prefab";
        foreach(string ext in new[]{".prefab",".asset",".png"})Backup(Root+"/"+id+ext,backup);
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var old=root.transform.Find("Wood");
            if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            build(root.GetComponent<AquariumDecorItem>());
            FitCollider(root);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Portrait(AssetDatabase.LoadAssetAtPath<GameObject>(path),id);
    }

    private static void FitCollider(GameObject root)
    {
        var bounds=new Bounds(Vector3.zero,Vector3.one*.02f);
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(renderer.bounds);
        var collider=root.GetComponent<BoxCollider>();
        collider.center=bounds.center;collider.size=bounds.size+Vector3.one*.025f;
    }

    private static void Backup(string file,string backup)
    {
        foreach(string f in new[]{file,file+".meta"})
            if(File.Exists(f))File.Copy(f,backup+"/"+Path.GetFileName(f),true);
    }
}
