using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Separate inspection scene: never edits the player's layout or wallet.
public static class CoralCollectionPreview
{
    public const string ScenePath="Assets/_Project/Scenes/CoralCollectionPreview.unity";
    public const string Output="docs/previews/CoralCollection";
    [MenuItem("Lumaquarium/Art/Open Coral Collection Preview")]
    public static void Open()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/MainAquarium.unity")throw new InvalidOperationException("Open MainAquarium first.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.SaveScene(scene,ScenePath,true);
        scene=EditorSceneManager.OpenScene(ScenePath);
        var d=UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
        d.storageKey="Lumaquarium.CoralCollection.Preview.v1";d.inventoryStorageKey="Lumaquarium.CoralCollection.Preview.Inventory.v1";d.persistLayout=false;
        var lighting=d.GetComponent<AquariumLighting>();lighting.persist=false;lighting.followClock=false;
        // Preserve the familiar plants/rocks, move the old driftwood to the side of the coral garden.
        foreach(var item in d.itemsRoot.GetComponentsInChildren<AquariumDecorItem>())
        {
            if(item.kind==2){item.transform.position=new Vector3(-.57f,d.floorY,.65f);item.transform.localScale=Vector3.one*1.3f;}
            else if(item.kind==0)item.transform.position+=Vector3.forward*.7f;
        }
        var positions=new[]{new Vector3(-.43f,0,.33f),new Vector3(-.46f,0,.83f),new Vector3(-.45f,0,-.1f),new Vector3(.43f,0,.58f),new Vector3(.5f,0,.12f),new Vector3(-.48f,0,-.62f),new Vector3(-.25f,0,-.87f),new Vector3(.27f,0,-.87f),new Vector3(.35f,0,-.19f),new Vector3(.53f,0,-.65f)};
        for(int i=0;i<CoralCollectionBuilder.All.Length;i++)
        {
            var species=CoralCollectionBuilder.All[i];var offer=d.catalog.offers.Single(o=>o.id==species.id);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(offer.prefab.gameObject,scene);root.transform.SetParent(d.itemsRoot,false);
            var p=positions[i];p.y=d.SandAt(p);root.transform.position=p;
            root.transform.localScale=Vector3.one*(i==1?1.35f:i==0||i==3?1.8f:1.45f);
            root.transform.rotation=Quaternion.Euler(0,i==1?5:i*17%50-25,0);
        }
        EditorUtility.SetDirty(d);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }

    [MenuItem("Lumaquarium/Art/Capture Coral Model Sheet")]
    public static void CaptureSheet()
    {
        Directory.CreateDirectory(Output);
        const int width=512,height=420;
        var sheet=new Texture2D(width*5,height*2,TextureFormat.RGB24,false);
        var background=new Color(.035f,.12f,.15f);
        var pixels=Enumerable.Repeat(background,width*height*10).ToArray();sheet.SetPixels(pixels);
        try
        {
            for(int i=0;i<CoralCollectionBuilder.All.Length;i++)
            {
                string path=AquariumDecorCatalogBuilder.Root+"/"+CoralCollectionBuilder.All[i].id+".png";
                var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    image.LoadImage(File.ReadAllBytes(path));var source=image.GetPixels();
                    for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)
                    {
                        var p=source[y*image.width+x];sheet.SetPixel((i%5)*width+x,(1-i/5)*height+y+18,Color.Lerp(background,p,p.a));
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(image);}
            }
            sheet.Apply();File.WriteAllBytes(Output+"/coral-models.png",sheet.EncodeToPNG());
        }
        finally{UnityEngine.Object.DestroyImmediate(sheet);}
    }
}
