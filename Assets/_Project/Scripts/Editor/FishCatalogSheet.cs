using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Lumaquarium.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Cá tab of the price sheet (DecorCatalogSheet). A fish's name, description and price live in the "shop"
// block of Art/Fish/<Name>/<Name>.json and are copied to its PipelineFish, its BettaVariant, both shop prefabs
// and the MainAquarium scene; Save writes all of them so no rebuild is needed.
public static class FishCatalogSheet
{
    public const string TabName = "Cá";
    private const string PortraitDir = Ui3D.Root + "/Portraits/Shop";
    private const string ScenePath = "Assets/_Project/Scenes/MainAquarium.unity";
    private const string ShopPrefab = Ui3D.Root + "/Prefabs/Shop.prefab";
    private const string FishShopPrefab = Ui3D.Root + "/Prefabs/FishShop.prefab";

    private static PipelineFish[] ShopFish() => PipelineFishImporter.All().Where(f => f.inShop).ToArray();

    public static List<DecorCatalogSheet.SheetItem> Items() => ShopFish().Select(f => new DecorCatalogSheet.SheetItem
        { id = f.shopId, name = f.displayName, description = f.description, price = f.price, freeAllowed = true }).ToList();

    public static DecorCatalogSheet.SheetTab Tab()
    {
        var tab = new DecorCatalogSheet.SheetTab { name = TabName, viewHeaders = new[] { "kiểu", "cỡ" } };
        foreach (var fish in ShopFish())
            tab.rows.Add(new DecorCatalogSheet.SheetRow
            {
                item = new DecorCatalogSheet.SheetItem { id = fish.shopId, name = fish.displayName, description = fish.description, price = fish.price },
                image = DecorCatalogSheet.ImageUrl(PortraitDir + "/" + fish.shopId + ".png", "fish"),
                view = new[] { fish.pattern != null ? fish.pattern.name : "", fish.size.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            });
        return tab;
    }

    // Writes the changed fish everywhere their text is kept. Returns problems to show in the Pull report.
    public static List<string> Save(IEnumerable<DecorCatalogSheet.SheetItem> items)
    {
        var errors = new List<string>();
        var changed = items.Where(i => i.changed).ToList();
        if (changed.Count == 0) return errors;
        var oldNames = new Dictionary<string, string>();
        foreach (var item in changed)
        {
            var fish = ShopFish().First(f => f.shopId == item.id);
            oldNames[item.id] = fish.displayName;
            string dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(fish)).Replace('\\', '/');
            string json = PipelineFishConfig.PathFor(dir, fish.name);
            if (File.Exists(json)) File.WriteAllText(json, SetShopFields(File.ReadAllText(json), item), new UTF8Encoding(false));
            else errors.Add($"Cá {item.id}: không thấy {json}, lần import cá sau sẽ trả về giá trị cũ.");
            Undo.RecordObject(fish, "Apply fish sheet");
            fish.displayName = item.name; fish.description = item.description; fish.price = item.price;
            EditorUtility.SetDirty(fish);
            var variant = AssetDatabase.LoadAssetAtPath<BettaVariant>(PipelineFishImporter.VariantPath(fish));
            if (variant != null) { Undo.RecordObject(variant, "Apply fish sheet"); variant.displayName = item.name; EditorUtility.SetDirty(variant); }
        }
        AssetDatabase.SaveAssets();
        EditPrefab(ShopPrefab, root => Apply(root, changed, oldNames));
        EditPrefab(FishShopPrefab, root => Apply(root, changed, oldNames));
        errors.AddRange(SaveScene(changed, oldNames));
        return errors;
    }

    // Replaces only the three values inside "shop": { ... }, keeping the rest of the hand-written file as it is.
    public static string SetShopFields(string json, DecorCatalogSheet.SheetItem item)
    {
        var block = Regex.Match(json, "\"shop\"\\s*:\\s*\\{[^{}]*\\}");
        if (!block.Success) return json;
        string text = block.Value;
        text = Regex.Replace(text, "(\"displayName\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"", m => m.Groups[1].Value + Quote(item.name));
        text = Regex.Replace(text, "(\"description\"\\s*:\\s*)\"(?:[^\"\\\\]|\\\\.)*\"", m => m.Groups[1].Value + Quote(item.description));
        text = Regex.Replace(text, "(\"price\"\\s*:\\s*)-?\\d+", m => m.Groups[1].Value + item.price);
        return json.Substring(0, block.Index) + text + json.Substring(block.Index + block.Length);
    }

    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        foreach (char c in value ?? "")
            text.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ => c < ' ' ? $"\\u{(int)c:x4}" : c.ToString() });
        return text.Append('"').ToString();
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // The shop screens below root: offers' description and price; the fish shop's cards and the HUD's fish name.
    private static void Apply(GameObject root, List<DecorCatalogSheet.SheetItem> changed, Dictionary<string, string> oldNames)
    {
        foreach (var shop in root.GetComponentsInChildren<ShopScreen>(true))
        {
            Undo.RecordObject(shop, "Apply fish sheet");
            Copy(shop.fishOffers, changed);
            Saved(shop);
        }
        foreach (var shop in root.GetComponentsInChildren<FishShopScreen>(true))
        {
            Undo.RecordObject(shop, "Apply fish sheet");
            Copy(shop.offers, changed);
            for (int i = 0; shop.offers != null && i < shop.offers.Length; i++)
            {
                var item = changed.FirstOrDefault(c => c.id == shop.offers[i].id);
                var label = item != null && shop.optionButtons != null && i < shop.optionButtons.Length && shop.optionButtons[i] != null
                    ? shop.optionButtons[i].transform.Find("Name")?.GetComponent<TMP_Text>() : null;
                if (label != null) { Undo.RecordObject(label, "Apply fish sheet"); label.text = item.name; Saved(label); }
            }
            Saved(shop);
        }
        // The header shows the tank's default fish until the saved one is applied at runtime.
        foreach (var screen in root.GetComponentsInChildren<MainAquariumScreen>(true))
        {
            var label = screen.fishNameLabel;
            var item = label != null ? changed.FirstOrDefault(c => oldNames[c.id] == label.text) : null;
            if (item != null) { Undo.RecordObject(label, "Apply fish sheet"); label.text = item.name; Saved(label); }
        }
    }

    private static void Copy(FishShopOffer[] offers, List<DecorCatalogSheet.SheetItem> changed)
    {
        if (offers == null) return;
        foreach (var offer in offers)
        {
            var item = offer != null ? changed.FirstOrDefault(c => c.id == offer.id) : null;
            if (item == null) continue;
            offer.description = item.description; offer.price = item.price;
        }
    }

    private static void Saved(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Opens MainAquarium on its own if it is closed; never saves a scene that holds someone's unsaved work.
    private static List<string> SaveScene(List<DecorCatalogSheet.SheetItem> changed, Dictionary<string, string> oldNames)
    {
        var errors = new List<string>();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            errors.Add("Đang Play: chưa cập nhật cá trong scene MainAquarium. Thoát Play rồi Pull lại.");
            return errors;
        }
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (!opened && scene.isDirty)
        {
            errors.Add("Scene MainAquarium có thay đổi chưa lưu nên chưa cập nhật cá trong scene. Lưu scene rồi Pull lại.");
            return errors;
        }
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects()) Apply(root, changed, oldNames);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        return errors;
    }
}
