using System;
using System.IO;
using System.Linq;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class DecorShopScreenBuilder
{
    private const string UI = "Assets/_Project/UI/";
    private const string PrefabPath = UI + "Prefabs/DecorShop.prefab";

    [MenuItem("Lumaquarium/UI/Build Decoration Shop")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/MainAquarium.unity")throw new InvalidOperationException("Open MainAquarium first.");
        var aquarium=UnityEngine.Object.FindFirstObjectByType<MainAquariumScreen>();
        if(aquarium==null || aquarium.decoration==null)throw new InvalidOperationException("Aquarium HUD and decoration are required.");
        Directory.CreateDirectory("Assets/_Project/Scenes/Backups");
        string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        File.Copy(scene.path,"Assets/_Project/Scenes/Backups/Main-before-decor-shop-disk-"+stamp+".unity");
        EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/Backups/Main-before-decor-shop-working-"+stamp+".unity",true);
        Ui3D.LoadFonts();
        var catalog=AquariumDecorCatalogBuilder.Build(aquarium.decoration.prefabs.Take(3).ToArray());
        Undo.RecordObject(aquarium.decoration,"Add decoration catalog");
        ApplyCatalog(aquarium.decoration,catalog);
        // Keep the approved environment prefab ready for subsequent scene installations.
        string environment="Assets/_Project/Art/Aquarium3D/AquariumEnvironment.prefab";
        var environmentRoot=PrefabUtility.LoadPrefabContents(environment);
        try
        {
            ApplyCatalog(environmentRoot.GetComponentInChildren<AquariumDecoration>(true),catalog);
            PrefabUtility.SaveAsPrefabAsset(environmentRoot,environment);
        }
        finally{PrefabUtility.UnloadPrefabContents(environmentRoot);}
        var existingPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var existingShop=existingPrefab!=null?existingPrefab.GetComponent<DecorShopScreen>():null;
        if(existingShop==null || existingShop.layoutVersion<FishShopScreenBuilder.LayoutVersion || existingShop.cards.Length!=catalog.offers.Length)
        {
            var built=Create(catalog);PrefabUtility.SaveAsPrefabAsset(built.gameObject,PrefabPath);UnityEngine.Object.DestroyImmediate(built.gameObject);
        }
        var shop=UnityEngine.Object.FindFirstObjectByType<DecorShopScreen>(FindObjectsInactive.Include);
        if(shop==null)
        {
            shop=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene)).GetComponent<DecorShopScreen>();
            Undo.RegisterCreatedObjectUndo(shop.gameObject,"Add decoration shop");
        }
        Connect(aquarium);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log($"[Lumaquarium] {catalog.offers.Length} decoration offers installed with stock, shared wallet and wood planting.");
    }

    private static void ApplyCatalog(AquariumDecoration decoration,AquariumDecorCatalog catalog)
    {
        if(decoration==null)throw new InvalidOperationException("Environment has no placement component.");
        var prefabs=new AquariumDecorItem[catalog.offers.Max(x=>x.prefab.kind)+1];
        Array.Copy(decoration.prefabs,prefabs,3);
        foreach(var offer in catalog.offers)prefabs[offer.prefab.kind]=offer.prefab;
        decoration.prefabs=prefabs;decoration.catalog=catalog;
        SetText(decoration.dock.transform.Find("PlacementAction1")?.GetComponentInChildren<TMP_Text>(),"Cất");
        EditorUtility.SetDirty(decoration);
        if(PrefabUtility.IsPartOfPrefabInstance(decoration))PrefabUtility.RecordPrefabInstancePropertyModifications(decoration);
    }

    // Links the shared shop tabs and the Trang trí sheet. Entry buttons from the old HUD are removed.
    public static void Connect(MainAquariumScreen aquarium)
    {
        var shop=UnityEngine.Object.FindFirstObjectByType<DecorShopScreen>(FindObjectsInactive.Include);
        if(shop==null)return;
        Undo.RecordObject(shop,"Connect decoration shop");
        shop.aquarium=aquarium;shop.fishShop=UnityEngine.Object.FindFirstObjectByType<FishShopScreen>(FindObjectsInactive.Include);
        if(shop.fishShop!=null)
        {
            Undo.RecordObject(shop.fishShop,"Link shop tabs");
            shop.fishShop.decorShop=shop;
            Saved(shop.fishShop);
            var sheet=shop.fishShop.content.transform.Find("SafeArea/SelectionSheet");
            RemoveLegacy(sheet,"DecorShopLink");
        }
        var inventory=aquarium.decoratePanel.GetComponent<DecorInventoryPanel>();
        if(inventory!=null){Undo.RecordObject(inventory,"Link decoration shop");inventory.decorShop=shop;Saved(inventory);}
        RemoveLegacy(aquarium.header.transform,"DecorShopButton");
        RemoveLegacy(aquarium.decoratePanel.transform,"DecorShopEntry");
        var deleteLabel=aquarium.decoration!=null?aquarium.decoration.dock.transform.Find("PlacementAction1")?.GetComponentInChildren<TMP_Text>():null;
        SetText(deleteLabel,"Cất");
        Saved(shop);
    }

    private static void RemoveLegacy(Transform parent,string name)
    {
        var legacy=parent!=null?parent.Find(name):null;
        if(legacy!=null)Undo.DestroyObjectImmediate(legacy.gameObject);
    }

    private static void Saved(Component component)
    {
        EditorUtility.SetDirty(component);
        if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    private static void SetText(TMP_Text label,string text)
    {
        if(label==null)return;
        Undo.RecordObject(label,"Update decoration label");
        label.text=text;EditorUtility.SetDirty(label);
        if(PrefabUtility.IsPartOfPrefabInstance(label))PrefabUtility.RecordPrefabInstancePropertyModifications(label);
    }

    // Same frame as the fish shop; the Cá tab hands back to it.
    private static DecorShopScreen Create(AquariumDecorCatalog catalog)
    {
        var root=Ui3D.Canvas("DecorShopHUD",35);
        var shop=root.gameObject.AddComponent<DecorShopScreen>();shop.catalog=catalog;shop.layoutVersion=FishShopScreenBuilder.LayoutVersion;
        shop.normalSurface=Ui3D.Require("ui3d_tile");shop.selectedSurface=Ui3D.Require("ui3d_tile_selected");
        var frame=Ui3D.BuildShopFrame(root,1,"Cửa hàng");
        shop.content=frame.content.gameObject;shop.backButton=frame.back;shop.balanceLabel=frame.balance;
        shop.fishButton=frame.tabs[0];
        shop.categoryButtons=frame.tabs.Skip(1).ToArray();
        shop.categorySurfaces=frame.tabSurfaces.Skip(1).ToArray();
        shop.categoryLabels=frame.tabLabels.Skip(1).ToArray();
        shop.tabLabelOn=Ui3D.Ink;shop.tabLabelOff=Ui3D.Cream;

        shop.portrait=Ui3D.Image("Portrait",frame.hero,null,false);shop.portrait.sprite=catalog.offers[0].portrait;shop.portrait.preserveAspect=true;
        Ui3D.Edges(shop.portrait.rectTransform,Vector2.zero,Vector2.one,new Vector2(220,170),new Vector2(-220,-20));
        shop.title=Ui3D.Text("Name",frame.hero,"",62,Ui3D.Cream,true);Ui3D.At(shop.title.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,80),new Vector2(980,90));
        shop.description=Ui3D.Text("Description",frame.hero,"",38,Ui3D.Cream);Ui3D.At(shop.description.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,10),new Vector2(980,64));

        var sheet=frame.sheet;
        var heading=Ui3D.Text("Heading",sheet,"Chọn món trang trí",50,Ui3D.Ink,true,TextAlignmentOptions.MidlineLeft);Ui3D.TopStrip(heading.rectTransform,64,460,44,76);
        shop.stockButton=Ui3D.Button("StockOnly",sheet,"Chỉ món trong kho",Ui3D.Style.Secondary,32);
        Ui3D.At((RectTransform)shop.stockButton.transform,Vector2.one,Vector2.one,new Vector2(-54,-34),new Vector2(400,100));
        shop.stockButtonLabel=shop.stockButton.GetComponentInChildren<TMP_Text>();
        var scrollRoot=Ui3D.Rect("ScrollView",sheet);Ui3D.Edges(scrollRoot,Vector2.zero,Vector2.one,new Vector2(54,470),new Vector2(-54,-150));
        scrollRoot.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var grid=Ui3D.Rect("Content",scrollRoot);Ui3D.Edges(grid,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);grid.pivot=new Vector2(.5f,1);
        var layout=grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();layout.cellSize=new Vector2(474,330);layout.spacing=new Vector2(24,24);layout.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;
        grid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=scrollRoot;scroll.content=grid;
        int count=catalog.offers.Length;
        shop.optionButtons=new UnityEngine.UI.Button[count];shop.cards=new GameObject[count];shop.cardSurfaces=new UnityEngine.UI.Image[count];shop.stockLabels=new TMP_Text[count];
        for(int i=0;i<count;i++)
        {
            var offer=catalog.offers[i];var card=Ui3D.Image("Item_"+offer.id,grid,"ui3d_tile");card.raycastTarget=true;
            shop.cards[i]=card.gameObject;shop.cardSurfaces[i]=card;var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=card;button.transition=UnityEngine.UI.Selectable.Transition.None;shop.optionButtons[i]=button;
            var portrait=Ui3D.Image("Portrait",card.transform,null,false);portrait.sprite=offer.portrait;portrait.preserveAspect=true;Ui3D.Edges(portrait.rectTransform,new Vector2(0,.42f),Vector2.one,new Vector2(16,0),new Vector2(-16,-14));
            var name=Ui3D.Text("Name",card.transform,offer.displayName,38,Ui3D.Ink,true,TextAlignmentOptions.MidlineLeft);Ui3D.Edges(name.rectTransform,new Vector2(0,.22f),new Vector2(1,.42f),new Vector2(28,0),new Vector2(-24,0));
            shop.stockLabels[i]=Ui3D.Text("Stock",card.transform,"",30,Ui3D.Forest,true,TextAlignmentOptions.MidlineLeft);Ui3D.Edges(shop.stockLabels[i].rectTransform,Vector2.zero,new Vector2(1,.22f),new Vector2(28,18),new Vector2(-20,0));
        }
        shop.feedback=Ui3D.Text("Feedback",sheet,"",32,Ui3D.Ink);Ui3D.Edges(shop.feedback.rectTransform,Vector2.zero,new Vector2(1,0),new Vector2(54,390),new Vector2(-54,460));

        var footer=frame.footer;
        var caption=Ui3D.Text("PriceCaption",footer,"GIÁ / MÓN",30,Ui3D.Muted,true,TextAlignmentOptions.MidlineLeft);Ui3D.At(caption.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(54,-40),new Vector2(300,44));
        shop.priceLabel=Ui3D.Text("Price",footer,"",52,Ui3D.Ink,true,TextAlignmentOptions.MidlineLeft);Ui3D.At(shop.priceLabel.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(54,-88),new Vector2(300,72));
        shop.placeButton=Ui3D.Button("Place",footer,"Đặt vào hồ",Ui3D.Style.Secondary,42);Ui3D.At((RectTransform)shop.placeButton.transform,Vector2.one,Vector2.one,new Vector2(-376,-36),new Vector2(300,150));
        shop.buyButton=Ui3D.Button("BuyOne",footer,"Mua 1",Ui3D.Style.Primary,46);Ui3D.At((RectTransform)shop.buyButton.transform,Vector2.one,Vector2.one,new Vector2(-48,-36),new Vector2(312,150));

        shop.confirmation=frame.modal.gameObject;var dialog=frame.dialog;
        var modalTitle=Ui3D.Text("Title",dialog,"Thêm một món vào kho?",54,Ui3D.Ink,true);Ui3D.TopStrip(modalTitle.rectTransform,50,50,60,90);
        shop.confirmationText=Ui3D.Text("Summary",dialog,"",40,Ui3D.Ink);Ui3D.TopStrip(shop.confirmationText.rectTransform,50,50,170,240);
        shop.cancelButton=Ui3D.Button("Cancel",dialog,"Chọn lại",Ui3D.Style.Secondary);Ui3D.Edges((RectTransform)shop.cancelButton.transform,Vector2.zero,new Vector2(.42f,0),new Vector2(56,60),new Vector2(-8,210));
        shop.confirmButton=Ui3D.Button("Confirm",dialog,"Xác nhận mua",Ui3D.Style.Primary);Ui3D.Edges((RectTransform)shop.confirmButton.transform,new Vector2(.42f,0),new Vector2(1,0),new Vector2(8,60),new Vector2(-56,210));
        frame.content.gameObject.SetActive(false);return shop;
    }
}
