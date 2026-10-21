using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Aquarium3DPreviewBuilder
{
    public const string PreviewPath = "Assets/_Project/Scenes/Aquarium3DPreview.unity";
    private const string AssetRoot = "Assets/_Project/Art/Aquarium3D";
    private static Material sand, stone, leaf, wood;
    private static TMP_FontAsset font;

    static Aquarium3DPreviewBuilder()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("AquariumPreview.RestoreStart", false)) return;
            string path = SessionState.GetString("AquariumPreview.PreviousStartScene", "");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetBool("AquariumPreview.RestoreStart", false);
        };
    }

    [MenuItem("Lumaquarium/Preview/Play 3D Preview")]
    public static void PlayPreview()
    {
        if (!File.Exists(PreviewPath)) throw new InvalidOperationException("Build the preview first.");
        if (EditorApplication.isPlaying)
        {
            EditorSceneManager.LoadSceneInPlayMode(PreviewPath, new LoadSceneParameters(LoadSceneMode.Single));
            return;
        }
        if (SceneManager.GetActiveScene().path != PreviewPath)
        {
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before opening the preview.");
            EditorSceneManager.OpenScene(PreviewPath);
        }
        SessionState.SetString("AquariumPreview.PreviousStartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool("AquariumPreview.RestoreStart", true);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Lumaquarium/Preview/Build 3D Decoration and Water")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var source = SceneManager.GetActiveScene();
        if (source.path != "Assets/_Project/Scenes/MainAquarium.unity")
            throw new InvalidOperationException("Open MainAquarium to create an independent preview copy.");
        if (File.Exists(PreviewPath))
            File.Copy(PreviewPath, $"Assets/_Project/Scenes/Backups/Aquarium3DPreview-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.unity");
        // Save a copy, including any current edits, without changing the original scene on disk.
        EditorSceneManager.SaveScene(source, PreviewPath, true);
        EditorSceneManager.OpenScene(PreviewPath);
        Directory.CreateDirectory(AssetRoot);
        AssetDatabase.Refresh();
        sand = Nature("Sand", new Color(.73f,.62f,.4f), .26f, 160);
        stone = Nature("RiverStone", new Color(.31f,.35f,.31f), .5f, 55);
        leaf = Nature("Leaf", new Color(.3f,.52f,.075f), .14f, 90);
        wood = Nature("Driftwood", new Color(.28f,.16f,.075f), .42f, 85);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/UI/Fonts/Nunito-SemiBold SDF.asset");
        var root = new GameObject("Aquarium3DPreview").transform;
        var screen = UnityEngine.Object.FindFirstObjectByType<Lumaquarium.UI.MainAquariumScreen>();
        var camera = Camera.main;
        // The approved image remains a distant atmosphere; all new foreground pieces are meshes.
        var floor = MeshObject("SandBed3D", root, SandMesh(), sand);
        floor.transform.position = new Vector3(0,.28f,-.6f);

        var placement = root.gameObject.AddComponent<AquariumDecoration>();
        placement.viewCamera = camera;
        placement.orbit = screen.orbit;
        placement.screen = screen;
        screen.decoration = placement;
        placement.itemsRoot = new GameObject("PlacedDecorations").transform;
        placement.itemsRoot.SetParent(root, false);
        placement.prefabs = new[] { PlantPrefab(), RockPrefab(), WoodPrefab() };
        // Keep the central swimming lane free; footprint checks use the same space at runtime.
        Seed(placement, 0, -.48f, .27f, 20);
        Seed(placement, 0, .48f, .32f, -25);
        Seed(placement, 1, -.48f, -.25f, 45);
        Seed(placement, 1, .47f, -.24f, -10);
        Seed(placement, 2, 0, .25f, 65);
        BuildRing(placement, root);
        var water = root.gameObject.AddComponent<AquariumWaterEffects>();
        water.viewCamera = camera;
        water.decoration = placement;
        water.fish = screen.fish.transform;
        placement.water = water;
        var waterMesh = MeshObject("AnimatedWaterSurface", root, GridMesh(3.6f, 4, 60), Material("Water", "Lumaquarium/Aquarium Water"));
        waterMesh.transform.position = new Vector3(0, water.surfaceY, .6f);
        water.surface = waterMesh.GetComponent<Renderer>();
        water.bubbles = BuildBubbles(root, Material("Bubbles", "Lumaquarium/Aquarium Bubble"));
        BuildControls(placement, screen);
        if (File.Exists("Assets/_Project/Art/Aquarium3D/Textures/AnubiasLeaf-v2.png"))
            AquariumNaturalVisualsBuilder.ApplyToScene(false);
        if (File.Exists("Assets/_Project/Art/Aquarium3D/Textures/Hardscape-v3.generation.json"))
            AquariumHardscapeBuilder.ApplyToScene(false);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] 3D preview ready. MainAquarium and Build Settings preserved.");
    }

    private static void Seed(AquariumDecoration placement, int kind, float x, float z, float yaw)
    {
        var item = (AquariumDecorItem)PrefabUtility.InstantiatePrefab(placement.prefabs[kind], placement.itemsRoot);
        item.transform.SetPositionAndRotation(new Vector3(x,placement.floorY,z), Quaternion.Euler(0,yaw,0));
    }

    private static AquariumDecorItem PlantPrefab()
    {
        var root = new GameObject("AquaticPlant");
        var item = root.AddComponent<AquariumDecorItem>();
        item.kind = 0; item.footprint = .14f;
        var foliage = new GameObject("SwayingFoliage").transform;
        foliage.SetParent(root.transform, false);
        item.foliage = foliage;
        item.phase = 1.5f;
        for (int stem = 0; stem < 5; stem++)
        {
            float angle = stem * 2.4f;
            Vector3 bottom = new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle)) * .07f;
            float height = .58f + stem * .1f;
            Vector3 top = bottom + new Vector3(Mathf.Sin(angle) * .13f,height,Mathf.Cos(angle) * .1f);
            Branch("Stem" + stem, foliage, bottom, top, .009f, .002f, leaf);
            for (int n = 0; n < 7; n++)
            {
                float t = .18f + n * .105f;
                float yaw = angle * Mathf.Rad2Deg + n * 137.5f;
                var blade = MeshObject($"Leaf{stem}-{n}", foliage, LeafMesh(), leaf);
                blade.transform.localPosition = Vector3.Lerp(bottom,top,t);
                blade.transform.localRotation = Quaternion.Euler(-22 - n * 4, yaw, -16);
                blade.transform.localScale = new Vector3(.09f,.11f,.25f) * (1 - n * .055f);
            }
        }
        AddPickCollider(root, new Vector3(0,.5f,0), new Vector3(.3f,1.05f,.3f));
        return SavePrefab(root);
    }

    private static AquariumDecorItem RockPrefab()
    {
        var root = new GameObject("RiverRock");
        var item = root.AddComponent<AquariumDecorItem>();
        item.kind = 1; item.footprint = .19f;
        var rock = MeshObject("WeatheredStone", root.transform, RockMesh(), stone);
        rock.transform.localPosition = new Vector3(0,.12f,0);
        rock.transform.localScale = new Vector3(.34f,.28f,.3f);
        AddPickCollider(root,new Vector3(0,.12f,0),new Vector3(.36f,.3f,.32f));
        return SavePrefab(root);
    }

    private static AquariumDecorItem WoodPrefab()
    {
        var root = new GameObject("BranchingDriftwood");
        var item = root.AddComponent<AquariumDecorItem>();
        item.kind = 2; item.footprint = .24f;
        Vector3 a = new Vector3(-.14f,.03f,0), b = new Vector3(.03f,.18f,0), c = new Vector3(.17f,.48f,.025f);
        Branch("TrunkBase",root.transform,a,b,.075f,.048f,wood);
        Branch("TrunkTip",root.transform,b,c,.048f,.008f,wood);
        Branch("LeftBranch",root.transform,b,new Vector3(-.16f,.44f,.06f),.03f,.003f,wood);
        Branch("RightBranch",root.transform,Vector3.Lerp(b,c,.45f),new Vector3(.22f,.35f,-.07f),.023f,.002f,wood);
        Branch("Root",root.transform,b,new Vector3(.19f,.01f,.1f),.04f,.006f,wood);
        AddPickCollider(root,new Vector3(0,.23f,0),new Vector3(.46f,.49f,.3f));
        return SavePrefab(root);
    }

    private static void AddPickCollider(GameObject root, Vector3 center, Vector3 size)
    {
        root.layer = 8;
        var box = root.AddComponent<BoxCollider>();
        box.center = center; box.size = size; box.isTrigger = true;
    }

    private static AquariumDecorItem SavePrefab(GameObject root)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{AssetRoot}/{root.name}.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<AquariumDecorItem>();
    }

    private static void Branch(string name, Transform parent, Vector3 a, Vector3 b, float r0, float r1, Material material)
    {
        const int rings = 8, sides = 10;
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var colors = new List<Color>();
        Quaternion direction = Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        for (int ring = 0; ring <= rings; ring++)
            for (int side = 0; side <= sides; side++)
            {
                float t = ring / (float)rings, angle = side * Mathf.PI * 2 / sides;
                float radius = Mathf.Lerp(r0,r1,t) * (1 + Mathf.Sin(side * 5.4f + ring * 1.5f) * .12f);
                var center = Vector3.Lerp(a,b,t) + new Vector3(Mathf.Sin(t * Mathf.PI) * .025f,0,0);
                vertices.Add(center + direction * new Vector3(Mathf.Cos(angle) * radius,0,Mathf.Sin(angle) * radius));
                colors.Add(Color.white * (.75f + .25f * Mathf.Pow(Mathf.Sin(side * 1.7f),2)));
                if (ring < rings && side < sides)
                {
                    int i = ring * (sides+1) + side;
                    triangles.AddRange(new[] {i,i+sides+1,i+1,i+1,i+sides+1,i+sides+2});
                }
            }
        MeshObject(name,parent,MakeMesh(name,vertices,triangles,colors),material);
    }

    private static Mesh LeafMesh()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var colors = new List<Color>();
        for (int row = 0; row <= 10; row++)
        {
            float t = row / 10f, width = Mathf.Sin(t * Mathf.PI) * .65f;
            for (int col = -1; col <= 1; col++)
            {
                vertices.Add(new Vector3(col*width,Mathf.Sin(t*Mathf.PI)*.3f-Mathf.Abs(col)*.12f,t));
                colors.Add(Color.white * (col == 0 ? 1.12f : .74f));
            }
            if (row < 10)
                for (int col = 0; col < 2; col++)
                {
                    int i = row*3+col;
                    triangles.AddRange(new[] {i,i+3,i+1,i+1,i+3,i+4});
                }
        }
        return MakeMesh("LeafBlade",vertices,triangles,colors);
    }

    private static Mesh RockMesh()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var colors = new List<Color>();
        const int rows = 14, columns = 22;
        for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                float phi = row * Mathf.PI/rows, theta = column * 2*Mathf.PI/columns;
                Vector3 p = new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta));
                float n = Mathf.PerlinNoise(p.x*3+12,p.y*4+p.z*2+8);
                vertices.Add(p * (.48f + n*.14f));
                colors.Add(Color.white * (.75f + n*.5f));
                if(row<rows && column<columns)
                {
                    int i=row*(columns+1)+column;
                    triangles.AddRange(new[] {i,i+1,i+columns+1,i+1,i+columns+2,i+columns+1});
                }
            }
        return MakeMesh("RiverRock",vertices,triangles,colors);
    }

    private static Mesh SandMesh()
    {
        Mesh mesh = GridMesh(2.7f,4.4f,44);
        var vertices = mesh.vertices;
        for (int i=0;i<vertices.Length;i++)
            vertices[i].y = .025f * Mathf.PerlinNoise(vertices[i].x*6+10,vertices[i].z*6+20);
        mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh.name="SandBed";
        return mesh;
    }

    private static Mesh GridMesh(float width,float depth,int count)
    {
        var vertices=new List<Vector3>(); var triangles=new List<int>(); var colors=new List<Color>();
        for(int z=0;z<=count;z++)
            for(int x=0;x<=count;x++)
            {
                vertices.Add(new Vector3((x/(float)count-.5f)*width,0,(z/(float)count-.5f)*depth));
                colors.Add(Color.white);
                if(x<count && z<count)
                {
                    int i=z*(count+1)+x;
                    triangles.AddRange(new[] {i,i+count+1,i+1,i+1,i+count+1,i+count+2});
                }
            }
        return MakeMesh("Grid",vertices,triangles,colors);
    }

    private static Mesh MakeMesh(string name,List<Vector3> vertices,List<int> triangles,List<Color> colors)
    {
        var mesh=new Mesh {name=name};
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.SetColors(colors);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material material)
    {
        // Persist generated geometry; scene/prefab references must survive a fresh Editor session.
        if (!AssetDatabase.Contains(mesh))
        {
            string path=AssetDatabase.GenerateUniqueAssetPath($"{AssetRoot}/{name}.asset");
            AssetDatabase.CreateAsset(mesh,path);
        }
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial=material;
        return go;
    }

    private static Material Nature(string name,Color color,float detail,float scale)
    {
        var material=Material(name,"Lumaquarium/Aquarium Nature");
        material.SetColor("_BaseColor",color); material.SetFloat("_Detail",detail); material.SetFloat("_Scale",scale);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Material(string name,string shader)
    {
        string path=$"{AssetRoot}/{name}.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null) return material;
        var found=Shader.Find(shader);
        if(found==null) throw new InvalidOperationException("Missing shader: "+shader);
        material=new Material(found) {name=name};
        AssetDatabase.CreateAsset(material,path);
        return material;
    }

    private static void BuildRing(AquariumDecoration placement,Transform root)
    {
        var ring=new GameObject("PlacementRing").AddComponent<LineRenderer>();
        ring.transform.SetParent(root,false); ring.useWorldSpace=false;
        ring.positionCount=49; ring.widthMultiplier=.009f;
        ring.sharedMaterial=Material("SelectionRing","Sprites/Default");
        placement.selectionRing=ring;
        ring.gameObject.SetActive(false);
    }

    private static ParticleSystem BuildBubbles(Transform root,Material material)
    {
        var bubbles=new GameObject("RisingBubbles").AddComponent<ParticleSystem>();
        bubbles.transform.SetParent(root,false); bubbles.transform.localPosition=new Vector3(.64f,.35f,.16f);
        bubbles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=bubbles.main; main.startLifetime=7; main.startSpeed=.29f;
        main.startSize=new ParticleSystem.MinMaxCurve(.008f,.024f); main.maxParticles=70;
        main.startColor=new Color(.8f,.95f,1,.7f); main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=bubbles.emission; emission.rateOverTime=7;
        var shape=bubbles.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(.15f,.01f,.14f);
        var velocity=bubbles.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.World;
        velocity.x=new ParticleSystem.MinMaxCurve(-.012f,.012f); velocity.z=new ParticleSystem.MinMaxCurve(-.008f,.008f);
        // Particle systems emit along local Z by default; use upward velocity instead.
        main.startSpeed=0; velocity.y=new ParticleSystem.MinMaxCurve(.27f,.32f);
        var color=bubbles.colorOverLifetime; color.enabled=true;
        var gradient=new Gradient();
        gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.12f),new GradientAlphaKey(.65f,.85f),new GradientAlphaKey(0,1)});
        color.color=gradient;
        bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
        bubbles.Play();
        return bubbles;
    }

    private static void BuildControls(AquariumDecoration placement,Lumaquarium.UI.MainAquariumScreen screen)
    {
        // The redesigned HUD builds its own inventory sheet and placement dock.
        if(screen.decoratePanel.GetComponent<Lumaquarium.UI.DecorInventoryPanel>()!=null) return;
        var panel=screen.decoratePanel.transform;
        for(int i=0;i<3;i++)
        {
            var slot=panel.Find("Inventory"+i);
            var button=slot.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic=slot.GetComponent<UnityEngine.UI.Image>();
            ((UnityEngine.UI.Image)button.targetGraphic).raycastTarget=true;
            UnityAction action=i==0 ? placement.BeginPlant : i==1 ? placement.BeginRock : placement.BeginWood;
            UnityEventTools.AddPersistentListener(button.onClick,action);
        }
        var availability=panel.Find("Availability").GetComponent<TMP_Text>();
        availability.text="Chọn món → kéo trên cát → xác nhận Đặt.\nBố cục được lưu riêng cho bản preview.";
        availability.fontSize=27;
        availability.rectTransform.anchoredPosition=new Vector2(0,-355);
        Button("EditExisting",panel,"Sửa đồ trong hồ",new Vector2(0,30),new Vector2(700,76),placement.EditExisting);

        var dock=Picture("PlacementDock",screen.toolbar.transform.parent,"panel_decorate");
        At(dock.rectTransform,new Vector2(.5f,0),new Vector2(0,85),new Vector2(960,270));
        placement.dock=dock.gameObject;
        var hint=Label("Instruction",dock.transform,"Kéo trên cát • Xoay • Đặt",29);
        At(hint.rectTransform,new Vector2(.5f,1),new Vector2(0,-62),new Vector2(850,86));
        placement.instruction=hint;
        string[] labels={"Xoay 30°","Xóa","Hủy","Đặt"};
        UnityAction[] actions={placement.Rotate,placement.Delete,placement.Cancel,placement.Confirm};
        for(int i=0;i<labels.Length;i++)
            Button("PlacementAction"+i,dock.transform,labels[i],new Vector2((i-1.5f)*218,48),new Vector2(202,93),actions[i]);
        dock.gameObject.SetActive(false);
    }

    private static UnityEngine.UI.Button Button(string name,Transform parent,string value,Vector2 position,Vector2 size,UnityAction action)
    {
        var image=Picture(name,parent,"button_primary");
        At(image.rectTransform,new Vector2(.5f,0),position,size);
        var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();
        var label=Label("Label",image.transform,value,30);
        label.color=new Color32(255,248,228,255);
        label.rectTransform.anchorMin=Vector2.zero; label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(5,5); label.rectTransform.offsetMax=new Vector2(-5,-5);
        UnityEventTools.AddPersistentListener(button.onClick,action);
        return button;
    }

    private static UnityEngine.UI.Image Picture(string name,Transform parent,string sprite)
    {
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
        var image=go.AddComponent<UnityEngine.UI.Image>();
        image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/UI/Surfaces/{sprite}.png");
        image.type=UnityEngine.UI.Image.Type.Sliced; image.raycastTarget=true;
        return image;
    }

    private static TMP_Text Label(string name,Transform parent,string value,int size)
    {
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
        var label=go.AddComponent<TextMeshProUGUI>(); label.text=value; label.font=font;
        label.fontSize=size; label.alignment=TextAlignmentOptions.Center;
        label.color=new Color32(52,60,45,255); label.raycastTarget=false;
        return label;
    }

    private static void At(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=anchor; rect.pivot=anchor;
        rect.anchoredPosition=position; rect.sizeDelta=size;
    }
}
