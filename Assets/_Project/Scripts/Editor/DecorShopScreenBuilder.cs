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
    private static readonly Color Ink = new Color32(45,66,45,255), Cream = new Color32(255,249,227,255);
    private static TMP_FontAsset font;

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
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UI+"Fonts/Nunito-SemiBold SDF.asset");
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
        if(existingPrefab==null || existingPrefab.GetComponent<DecorShopScreen>().cards.Length!=catalog.offers.Length)
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
        Debug.Log("[Lumaquarium] 24 decoration offers installed with stock, shared wallet and wood planting.");
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

    public static void Connect(MainAquariumScreen aquarium)
    {
        var shop=UnityEngine.Object.FindFirstObjectByType<DecorShopScreen>(FindObjectsInactive.Include);
        if(shop==null)return;
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UI+"Fonts/Nunito-SemiBold SDF.asset");
        shop.aquarium=aquarium;shop.fishShop=UnityEngine.Object.FindFirstObjectByType<FishShopScreen>(FindObjectsInactive.Include);
        WireEntry(aquarium.header.transform,"DecorShopButton","Cây · Lũa · Đá",new Vector2(1,1),new Vector2(0,-220),new Vector2(248,80),shop.Open);
        var panel=aquarium.decoratePanel.transform;
        WireEntry(panel,"DecorShopEntry","Cửa hàng & kho đồ",new Vector2(.5f,0),new Vector2(0,115),new Vector2(700,76),shop.Open);
        var hint=panel.Find("Availability")?.GetComponent<TMP_Text>();
        SetText(hint,"Mua từng món trong cửa hàng.\nĐồ đã cất sẽ quay lại kho.");
        var deleteLabel=aquarium.decoration.dock.transform.Find("PlacementAction1")?.GetComponentInChildren<TMP_Text>();
        SetText(deleteLabel,"Cất");
        if(shop.fishShop!=null)
        {
            var sheet=shop.fishShop.content.transform.Find("SafeArea/SelectionSheet");
            if(sheet!=null)
            {
                var count=sheet.Find("FishCount");if(count!=null)count.gameObject.SetActive(false);
                WireEntry(sheet,"DecorShopLink","Cây · Lũa · Đá",Vector2.one,new Vector2(-55,-48),new Vector2(385,70),shop.Open);
            }
        }
        EditorUtility.SetDirty(shop);if(PrefabUtility.IsPartOfPrefabInstance(shop))PrefabUtility.RecordPrefabInstancePropertyModifications(shop);
    }

    private static void WireEntry(Transform parent,string name,string title,Vector2 anchor,Vector2 offset,Vector2 size,UnityAction action)
    {
        var existing=parent.Find(name);
        var button=existing!=null?existing.GetComponent<UnityEngine.UI.Button>():Button(name,parent,title);
        At((RectTransform)button.transform,anchor,offset,size);
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)
            if(button.onClick.GetPersistentTarget(i) is DecorShopScreen || button.onClick.GetPersistentTarget(i)==null)UnityEventTools.RemovePersistentListener(button.onClick,i);
        UnityEventTools.AddPersistentListener(button.onClick,action);EditorUtility.SetDirty(button);
    }

    private static void SetText(TMP_Text label,string text)
    {
        if(label==null)return;
        Undo.RecordObject(label,"Update decoration label");
        label.text=text;EditorUtility.SetDirty(label);
        if(PrefabUtility.IsPartOfPrefabInstance(label))PrefabUtility.RecordPrefabInstancePropertyModifications(label);
    }

    private static DecorShopScreen Create(AquariumDecorCatalog catalog)
    {
        var root=Rect("DecorShopHUD",null);var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=35;
        var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=0;
        root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var shop=root.gameObject.AddComponent<DecorShopScreen>();shop.catalog=catalog;
        shop.normalSurface=Sprite("Surfaces/inventory_slot");shop.selectedSurface=Sprite("Surfaces/inventory_slot_selected");
        var content=Picture("ShopScreen",root,null,new Color32(20,65,66,255));Stretch(content.rectTransform);content.raycastTarget=true;shop.content=content.gameObject;
        var safe=Rect("SafeArea",content.transform);Stretch(safe);safe.gameObject.AddComponent<SafeArea>();
        shop.backButton=Button("Back",safe,"‹ Về hồ");At((RectTransform)shop.backButton.transform,new Vector2(0,1),new Vector2(40,-36),new Vector2(245,90));
        var heading=Label("Heading",safe,"Vườn thủy sinh",58,Cream);At(heading.rectTransform,new Vector2(.5f,1),new Vector2(30,-38),new Vector2(490,88));
        shop.balanceLabel=Label("Balance",safe,"1.250 xu",40,Cream);At(shop.balanceLabel.rectTransform,Vector2.one,new Vector2(-38,-38),new Vector2(250,88));
        var subtitle=Label("Subtitle",safe,"CÂY XANH · GỖ MỘC · ĐÁ TỰ NHIÊN",29,new Color32(190,218,181,255));At(subtitle.rectTransform,new Vector2(.5f,1),new Vector2(0,-151),new Vector2(980,55));
        var hero=Picture("SelectedItem",safe,Sprite("Surfaces/panel_care"),Cream);At(hero.rectTransform,new Vector2(.5f,1),new Vector2(0,-235),new Vector2(990,410));
        shop.portrait=Picture("Portrait",hero.transform,catalog.offers[0].portrait,Color.white);shop.portrait.preserveAspect=true;
        Edges(shop.portrait.rectTransform,Vector2.zero,new Vector2(.44f,1),new Vector2(14,24),new Vector2(-4,-20));
        shop.title=Label("Name",hero.transform,"",49,Ink);Edges(shop.title.rectTransform,new Vector2(.44f,.65f),Vector2.one,new Vector2(12,0),new Vector2(-22,-20));
        shop.description=Label("Description",hero.transform,"",31,Ink);Edges(shop.description.rectTransform,new Vector2(.44f,.19f),new Vector2(1,.65f),new Vector2(12,0),new Vector2(-22,-4));
        shop.priceLabel=Label("Price",hero.transform,"",31,Ink);Edges(shop.priceLabel.rectTransform,new Vector2(.44f,0),new Vector2(1,.19f),new Vector2(12,10),new Vector2(-22,0));
        shop.categoryButtons=new UnityEngine.UI.Button[3];string[] categories={"Cây thủy sinh","Lũa","Đá · Non bộ"};
        for(int i=0;i<3;i++)
        {shop.categoryButtons[i]=Button("Category"+i,safe,categories[i]);At((RectTransform)shop.categoryButtons[i].transform,new Vector2(.5f,1),new Vector2((i-1)*336,-680),new Vector2(318,96));}
        shop.stockButton=Button("StockOnly",safe,"Xem kho của bạn");At((RectTransform)shop.stockButton.transform,new Vector2(0,1),new Vector2(44,-796),new Vector2(470,74));shop.stockButtonLabel=shop.stockButton.GetComponentInChildren<TMP_Text>();
        shop.fishButton=Button("FishShop",safe,"Sang tiệm cá →");At((RectTransform)shop.fishButton.transform,Vector2.one,new Vector2(-44,-796),new Vector2(470,74));
        var scrollRoot=Rect("ScrollView",safe);Edges(scrollRoot,Vector2.zero,Vector2.one,new Vector2(44,340),new Vector2(-44,-892));
        var scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var viewport=Picture("Viewport",scrollRoot,null,Color.white);Stretch(viewport.rectTransform);viewport.raycastTarget=true;
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var grid=Rect("Content",viewport.transform);Edges(grid,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero);grid.pivot=new Vector2(.5f,1);
        var layout=grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();layout.cellSize=new Vector2(484,330);layout.spacing=new Vector2(24,24);layout.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;
        grid.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport.rectTransform;scroll.content=grid;
        int count=catalog.offers.Length;
        shop.optionButtons=new UnityEngine.UI.Button[count];shop.cards=new GameObject[count];shop.cardSurfaces=new UnityEngine.UI.Image[count];shop.stockLabels=new TMP_Text[count];
        for(int i=0;i<count;i++)
        {
            var offer=catalog.offers[i];var card=Picture("Item_"+offer.id,grid,shop.normalSurface,Color.white);card.raycastTarget=true;
            shop.cards[i]=card.gameObject;shop.cardSurfaces[i]=card;var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=card;shop.optionButtons[i]=button;
            var portrait=Picture("Portrait",card.transform,offer.portrait,Color.white);portrait.preserveAspect=true;Edges(portrait.rectTransform,new Vector2(0,.42f),Vector2.one,new Vector2(16,0),new Vector2(-16,-8));
            var name=Label("Name",card.transform,offer.displayName,37,Ink);At(name.rectTransform,new Vector2(.5f,0),new Vector2(0,91),new Vector2(450,57));
            shop.stockLabels[i]=Label("Stock",card.transform,"",28,Ink);At(shop.stockLabels[i].rectTransform,new Vector2(.5f,0),new Vector2(0,16),new Vector2(450,75));
        }
        var footer=Picture("Footer",safe,null,new Color32(244,237,211,255));Edges(footer.rectTransform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,315));footer.raycastTarget=true;
        shop.buyButton=Button("BuyOne",footer.transform,"Mua 1 món",true);At((RectTransform)shop.buyButton.transform,new Vector2(0,1),new Vector2(44,-24),new Vector2(480,106));
        shop.placeButton=Button("Place",footer.transform,"Đặt vào hồ");At((RectTransform)shop.placeButton.transform,Vector2.one,new Vector2(-44,-24),new Vector2(480,106));
        shop.feedback=Label("Feedback",footer.transform,"",30,Ink);Edges(shop.feedback.rectTransform,Vector2.zero,Vector2.one,new Vector2(45,15),new Vector2(-45,-145));
        var modal=Picture("PurchaseConfirmation",content.transform,null,new Color(0,.08f,.07f,.85f));Stretch(modal.rectTransform);modal.raycastTarget=true;shop.confirmation=modal.gameObject;
        var dialog=Picture("Dialog",modal.transform,Sprite("Surfaces/panel_care"),Cream);At(dialog.rectTransform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(960,660));
        var modalTitle=Label("Title",dialog.transform,"Thêm một món vào kho?",49,Ink);At(modalTitle.rectTransform,new Vector2(.5f,1),new Vector2(0,-52),new Vector2(860,95));
        shop.confirmationText=Label("Summary",dialog.transform,"",40,Ink);At(shop.confirmationText.rectTransform,new Vector2(.5f,.5f),new Vector2(0,10),new Vector2(840,260));
        shop.cancelButton=Button("Cancel",dialog.transform,"Chọn lại");At((RectTransform)shop.cancelButton.transform,Vector2.zero,new Vector2(48,52),new Vector2(405,115));
        shop.confirmButton=Button("Confirm",dialog.transform,"Xác nhận mua",true);At((RectTransform)shop.confirmButton.transform,new Vector2(1,0),new Vector2(-48,52),new Vector2(405,115));
        modal.gameObject.SetActive(false);content.gameObject.SetActive(false);return shop;
    }

    private static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(UI+name+".png");
    private static RectTransform Rect(string name,Transform parent)
    {var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;if(parent!=null)r.SetParent(parent,false);return r;}
    private static void Stretch(RectTransform r)=>Edges(r,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
    private static void Edges(RectTransform r,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper)
    {r.anchorMin=min;r.anchorMax=max;r.offsetMin=lower;r.offsetMax=upper;}
    private static void At(RectTransform r,Vector2 anchor,Vector2 position,Vector2 size)
    {r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;}
    private static UnityEngine.UI.Image Picture(string name,Transform parent,Sprite sprite,Color color)
    {var image=Rect(name,parent).gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;if(sprite!=null)image.type=UnityEngine.UI.Image.Type.Sliced;return image;}
    private static TMP_Text Label(string name,Transform parent,string text,float size,Color color)
    {var label=Rect(name,parent).gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=size*.8f;label.fontSizeMax=size;return label;}
    private static UnityEngine.UI.Button Button(string name,Transform parent,string text,bool primary=false)
    {var surface=Picture(name,parent,Sprite("Surfaces/"+(primary?"button_selected_wide":"button_normal_wide")),Color.white);surface.raycastTarget=true;var button=surface.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=surface;var label=Label("Label",surface.transform,text,35,Ink);Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(12,6);label.rectTransform.offsetMax=new Vector2(-12,-6);return button;}
}
