using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Art/Fish/<Name>/<Name>.json: every setting of one pipeline fish in one file.
//   "build"            read by betta-splendens/pipeline/build_fish.py --config (mesh shape, fin thickness, ...)
//   "shop" / "model" / "look"   read here by PipelineFishImporter, written into <Name>.asset and the variant
// A key left out keeps the default below. Colours are [r, g, b] (0-1, as in the Inspector); ranges are [start, end].
[Serializable]
public class PipelineFishConfig
{
    public Shop shop = new Shop();
    public Model model = new Model();
    public Look look = new Look();

    [Serializable]
    public class Shop
    {
        public string displayName = "Plakat Betta";
        public bool inShop = true;
        public string shopId = "";
        public string description = "";
        public int price;
    }

    [Serializable]
    public class Model
    {
        public float size = 2f;
        public float bodySwimAmplitude = 3f;
        public float strokeSwimAmplitude = 8f;
    }

    [Serializable]
    public class Look
    {
        public string pattern = "Solid";   // Data/Betta/Patterns/<pattern>.asset
        public float[] primary = { 0.12f, 0.31f, 0.82f };
        public float[] secondary = { 0.95f, 0.95f, 0.95f };
        public float[] accent = { 0.09f, 0.09f, 0.09f };
        public float[] finEdge = { 1f, 0.82f, 0.2f };
        public float iridescence = 0.4f;
        public float textureDetail = 0.6f;
        public float silverSheen = 1f;
        public float gloss = 1f;
        public float blackGloss;
        public float whiteScales;
        public float pigmentDepth;
        public float finEdgeClear;
        public float finRelief = 1f;
        public float finTranslucency = 0.8f;
        public float scaleRelief = 1f;
        public float scaleEdge;
        public float scaleEdgeWidth = 0.5f;
        public float[] scaleEdgeDark = { 0.62f, 0.42f, 0.22f };
        public float[] scaleEdgeLight = { 0.5f, 0.5f, 0.55f };
        public float finBlack;
        public float finBrightness = 1f;
        public float vibrance = 1f;
        public float brightness = 1f;
        public float finRimAlpha = 1f;
        public float[] finRim = { 0.9f, 1f };
        public float[] caudalRim = { 0.62f, 0.8f };
        public float caudalStart = 0.55f;
        public int seed;
    }

    const string PatternDir = "Assets/_Project/Data/Betta/Patterns";

    public static string PathFor(string fishDir, string name) => fishDir + "/" + name + ".json";

    public static PipelineFishConfig Load(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonUtility.FromJson<PipelineFishConfig>(File.ReadAllText(path)); }
        catch (Exception e) { throw new InvalidOperationException("Bad fish config " + path + ": " + e.Message); }
    }

    // Every Art/Fish/<Name>/<Name>.json without a <Name>.asset gets one, so a new fish needs only its folder.
    public static void CreateMissingAssets(string fishRoot)
    {
        foreach (string dir in Directory.GetDirectories(fishRoot))
        {
            string name = Path.GetFileName(dir);
            string assetPath = fishRoot + "/" + name + "/" + name + ".asset";
            if (!File.Exists(PathFor(fishRoot + "/" + name, name)) || File.Exists(assetPath)) continue;
            var fish = ScriptableObject.CreateInstance<PipelineFish>();
            AssetDatabase.CreateAsset(fish, assetPath);
        }
    }

    public void ApplyTo(PipelineFish fish, string path)
    {
        fish.displayName = shop.displayName;
        fish.inShop = shop.inShop;
        fish.shopId = shop.shopId;
        fish.description = shop.description;
        fish.price = shop.price;
        fish.size = model.size;
        fish.bodySwimAmplitude = model.bodySwimAmplitude;
        fish.strokeSwimAmplitude = model.strokeSwimAmplitude;
        fish.pattern = Pattern(path);
        fish.primary = Colour(look.primary, "primary", path);
        fish.secondary = Colour(look.secondary, "secondary", path);
        fish.accent = Colour(look.accent, "accent", path);
        fish.finEdge = Colour(look.finEdge, "finEdge", path);
        fish.iridescence = look.iridescence;
        fish.seed = look.seed;
        EditorUtility.SetDirty(fish);
    }

    // The surface settings that live only on the variant.
    public void ApplyTo(BettaVariant variant, string path)
    {
        variant.textureDetail = look.textureDetail;
        variant.silverSheen = look.silverSheen;
        variant.gloss = look.gloss;
        variant.blackGloss = look.blackGloss;
        variant.whiteScales = look.whiteScales;
        variant.pigmentDepth = look.pigmentDepth;
        variant.finEdgeClear = look.finEdgeClear;
        variant.finRelief = look.finRelief;
        variant.finTranslucency = look.finTranslucency;
        variant.scaleRelief = look.scaleRelief;
        variant.scaleEdge = look.scaleEdge;
        variant.scaleEdgeWidth = look.scaleEdgeWidth;
        variant.scaleEdgeDark = Colour(look.scaleEdgeDark, "scaleEdgeDark", path);
        variant.scaleEdgeLight = Colour(look.scaleEdgeLight, "scaleEdgeLight", path);
        variant.finBlack = look.finBlack;
        variant.finBrightness = look.finBrightness;
        variant.vibrance = look.vibrance;
        variant.brightness = look.brightness;
        variant.finRimAlpha = look.finRimAlpha;
        variant.finRim = Range(look.finRim, "finRim", path);
        variant.caudalRim = Range(look.caudalRim, "caudalRim", path);
        variant.caudalStart = look.caudalStart;
    }

    BettaPattern Pattern(string path)
    {
        var pattern = AssetDatabase.LoadAssetAtPath<BettaPattern>(PatternDir + "/" + look.pattern + ".asset");
        if (!pattern) throw new InvalidOperationException(path + ": no pattern " + PatternDir + "/" + look.pattern + ".asset");
        return pattern;
    }

    static Color Colour(float[] v, string key, string path)
    {
        if (v == null || v.Length != 3) throw new InvalidOperationException(path + ": " + key + " needs [r, g, b]");
        return new Color(v[0], v[1], v[2]);
    }

    static Vector2 Range(float[] v, string key, string path)
    {
        if (v == null || v.Length != 2) throw new InvalidOperationException(path + ": " + key + " needs [start, end]");
        return new Vector2(v[0], v[1]);
    }
}
