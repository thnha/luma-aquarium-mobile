using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class AquariumDecorCatalogBuilder
{
    private static readonly string[] RedesignedStones = { "river-pebbles", "dragon-stone", "lava-rock", "seiryu", "rock-cluster",
        "mini-landscape", "stone-elephant-skin", "stone-slab", "stone-cliff" };

    // Rebuilds only the redesigned stones in place: same prefab, mesh and portrait paths, so the catalog,
    // the shop cards and stones already placed in tanks pick up the new look without a full catalog build.
    [MenuItem("Lumaquarium/Art/Rebuild Redesigned Stones")]
    public static void RebuildRedesignedStones()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        string backup="Backups/Stones/"+SceneBackups.Stamp();Directory.CreateDirectory(backup);
        foreach(string id in RedesignedStones)
            foreach(string ext in new[]{".prefab",".asset",".png"})
                foreach(string file in new[]{Root+"/"+id+ext,Root+"/"+id+ext+".meta"})
                    if(File.Exists(file))File.Copy(file,backup+"/"+Path.GetFileName(file),true);
        var material=SpeciesStone();
        WithPortraitLight(()=>{ foreach(string id in RedesignedStones) RebuildStone(id,material); });
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Rebuilt "+RedesignedStones.Length+" redesigned stones. Backups: "+backup);
    }

    // Portraits take the open scene's light; a scene without one gets a studio light close to MainAquarium's.
    private static void WithPortraitLight(Action bake)
    {
        GameObject studio=null;var ambientMode=RenderSettings.ambientMode;var ambient=RenderSettings.ambientLight;
        if(UnityEngine.Object.FindFirstObjectByType<Light>()==null)
        {
            studio=new GameObject("StonePortraitLight");var light=studio.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.25f;light.color=new Color(1,.97f,.92f);light.cullingMask=1<<31;
            studio.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.52f,.55f);
        }
        try { bake(); }
        finally
        {
            if(studio!=null){UnityEngine.Object.DestroyImmediate(studio);RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;}
        }
    }

    private static void RebuildStone(string id,Material material)
    {
        string path=Root+"/"+id+".prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var item=root.GetComponent<AquariumDecorItem>();
            var model=root.transform.Find("Stone").GetComponent<MeshFilter>();
            model.sharedMesh=id=="stone-slab"?Fitted(Piece("stone-elephant-skin"),SlabSize,id)
                :id=="stone-cliff"?Fitted(Piece(id),CliffSize,id)
                :id=="stone-elephant-skin"?Store(StoneCoralBuilder.Build(Piece(id),1,out item.footprint),id)
                :Store(StoneCoralBuilder.Build(Piece(id),1,out _),id);
            model.GetComponent<MeshRenderer>().sharedMaterial=material;
            item.plantMounts=TopMounts(root);
            var bounds=new Bounds(Vector3.zero,Vector3.one*.02f);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(renderer.bounds);
            var collider=root.GetComponent<BoxCollider>();
            collider.center=bounds.center;collider.size=bounds.size+Vector3.one*.025f;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Portrait(AssetDatabase.LoadAssetAtPath<GameObject>(path),id);
    }
}
