using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AquariumDecorCatalogBuilder
{
    private static Material CollectionMaterial()
    {
        string path=Root+"/CoralCollection.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("Lumaquarium/Aquarium Coral");
        if(shader==null)throw new InvalidOperationException("Coral shader is missing.");
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.shader=shader;material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Roughness",.62f);material.SetFloat("_Detail",.3f);material.SetFloat("_Transmission",.18f);
        EditorUtility.SetDirty(material);return material;
    }

    private static void AddCoralCollection(List<AquariumDecorOffer> offers)
    {
        var material=CollectionMaterial();
        foreach(var species in CoralCollectionBuilder.All)
        {
            var previous=offers.Find(o=>o.id==species.id);
            if(offers.Any(o=>o.id!=species.id && o.prefab.kind==species.kind))
                throw new InvalidOperationException("Coral kind conflicts with a released item: "+species.kind);
            var root=new GameObject(species.id);
            try
            {
                root.layer=8;
                var item=root.AddComponent<AquariumDecorItem>();item.kind=species.kind;
                var mesh=Store(CoralCollectionBuilder.Build(species,1,out item.footprint),species.id);
                Model("Coral",root.transform,mesh,material);
                foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=8;
                var box=root.AddComponent<BoxCollider>();box.isTrigger=true;box.center=mesh.bounds.center;box.size=mesh.bounds.size+Vector3.one*.01f;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+species.id+".prefab").GetComponent<AquariumDecorItem>();
                var offer=new AquariumDecorOffer{id=species.id,displayName=species.name,description=species.description,
                    price=previous!=null?previous.price:species.price,category=AquariumDecorCategory.Coral,prefab=prefab,portrait=Portrait(prefab.gameObject,species.id)};
                if(previous!=null)offers[offers.IndexOf(previous)]=offer;else offers.Add(offer);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }

    [MenuItem("Lumaquarium/Art/Install Approved Coral Collection")]
    public static void InstallCoralCollection()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/MainAquarium.unity")throw new InvalidOperationException("Open MainAquarium first.");
        var catalog=AssetDatabase.LoadAssetAtPath<AquariumDecorCatalog>(Root+"/AquariumDecorCatalog.asset");
        if(catalog==null)throw new InvalidOperationException("Build the decoration catalog first.");
        string backup="Backups/Corals/"+SceneBackups.Stamp();Directory.CreateDirectory(backup);
        // Keep focused backups outside Assets; never prune other user backups.
        EditorSceneManager.SaveScene(scene,backup+"/MainAquarium.unity",true);
        foreach(var path in new[]{Root,"Assets/_Project/UI/Prefabs","Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab"})
        {
            if(Directory.Exists(path))
                foreach(string file in Directory.GetFiles(path))File.Copy(file,backup+"/"+Path.GetFileName(file),true);
            else if(File.Exists(path))File.Copy(path,backup+"/"+Path.GetFileName(path),true);
        }
        var offers=catalog.offers.ToList();AddCoralCollection(offers);catalog.offers=offers.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        ServerCatalogWriter.Write();
        DecorShopScreenBuilder.RefreshCatalogViews(catalog);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Installed ten approved coral models. Backups: "+backup);
    }
}
