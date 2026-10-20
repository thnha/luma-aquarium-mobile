using System.Linq;
using UnityEditor;
using UnityEngine;

// Creates (or refreshes) the starter tail types, patterns, variants and library under Data/Betta.
// Menu: Lumaquarium > Build Betta Variant Presets (safe to re-run; overwrites these assets' values).
// The same table is shown in docs/betta-variants.md.
public static class BettaVariantPresets
{
    const string Dir = "Assets/_Project/Data/Betta";
    public const string LibraryPath = Dir + "/BettaLibrary.asset";

    [MenuItem("Lumaquarium/Build Betta Variant Presets")]
    public static BettaVariantLibrary Build()
    {
        foreach (string sub in new[] { "", "/Tails", "/Patterns", "/Variants" }) EnsureFolder(Dir + sub);

        BettaTailType Tail(string id, string name, float caudal, float dorsal, float anal, float pelvic, float spread, float droop,
            float sag, float tipStiffness, float maxAngle, float weight)
        {
            var t = Load<BettaTailType>(Dir + "/Tails/" + id + ".asset");
            t.displayName = name;
            t.caudalLength = caudal; t.dorsalLength = dorsal; t.analLength = anal; t.pelvicLength = pelvic;
            t.caudalSpread = spread; t.caudalDroop = droop;
            t.finMeshes = HalfmoonFins; t.baseMap = null; t.skippedFinChains = BettaFinPhysics.CrownChainRoots;
            t.ruffle = new BettaFinRuffle.Settings();
            // Betta.fbx is a Halfmoon, so it is the baseline (scale 1, spread 0); other forms are relative to it.
            // Fin physics starts from the BettaFinPhysics defaults, which were tuned on that model.
            t.caudal = new BettaFinPhysics.FinSettings { rootStiffness = 0.1f, tipStiffness = tipStiffness, damping = 0.18f, maxAngle = maxAngle, ripple = 0.005f, speedStiffening = 1.3f, sag = sag };
            t.dorsalAnal = new BettaFinPhysics.FinSettings { rootStiffness = 0.14f, tipStiffness = tipStiffness * 1.7f, damping = 0.24f, maxAngle = maxAngle, ripple = 0.002f, speedStiffening = 2.5f, sag = sag * 0.5f };
            t.rarityWeight = weight;
            EditorUtility.SetDirty(t);
            return t;
        }
        var plakat = Tail("Plakat", "Plakat", 0.45f, 0.6f, 0.65f, 0.8f, -6f, 0f, 0.03f, 0.09f, 15f, 30f);
        var veiltail = Tail("Veiltail", "Veiltail", 1.15f, 1.1f, 1.1f, 1.1f, -12f, 14f, 0.3f, 0.03f, 25f, 25f);
        var delta = Tail("Delta", "Delta", 0.9f, 0.95f, 0.95f, 1f, -10f, 0f, 0.12f, 0.045f, 22f, 18f);
        var halfmoon = Tail("Halfmoon", "Halfmoon", 1f, 1f, 1f, 1f, 0f, 0f, 0.15f, 0.035f, 22f, 12f);
        var crowntail = Tail("Crowntail", "Crowntail", 1f, 1f, 1f, 1f, -4f, 0f, 0.12f, 0.04f, 22f, 10f);
        var doubletail = Tail("DoubleTail", "Double Tail", 0.95f, 1f, 1f, 1f, 4f, 0f, 0.15f, 0.035f, 22f, 5f);
        // A Halfmoon with so many ray branches that the edge folds: Halfmoon meshes, folded by BettaFinRuffle.
        var rosetail = Tail("Rosetail", "Rosetail", 1.05f, 1.05f, 1.05f, 1f, 6f, 0f, 0.18f, 0.035f, 22f, 4f);
        rosetail.ruffle = new BettaFinRuffle.Settings { depth = 0.04f, wavelength = 0.2f, start = 0.5f };
        // Own fin meshes in Betta.fbx, so no alpha-cut base map. The Double Tail dorsal mesh is already
        // the long one, so dorsalLength stays 1. Only the Crowntail meshes use the *_Crown_* spike bones.
        // The Double Tail slit hides the middle Tail_ ray: the lobes keep their own rays (Top/UpMid, LowMid/Low/Bot).
        crowntail.finMeshes = new[] { "Fin_Caudal_Crowntail", "Fin_Dorsal_Crowntail", "Fin_Anal_Crowntail" };
        doubletail.finMeshes = new[] { "Fin_Caudal_DoubleTail", "Fin_Dorsal_DoubleTail", "Fin_Anal_Halfmoon" };
        crowntail.skippedFinChains = new string[0];
        doubletail.skippedFinChains = BettaFinPhysics.CrownChainRoots.Append("Tail_01").ToArray();

        BettaPattern Pattern(string id, BettaPattern.Kind kind, float spot, float accentSpot, float weight,
            float band = 0.55f, float bandSoftness = 0.04f)
        {
            var p = Load<BettaPattern>(Dir + "/Patterns/" + id + ".asset");
            p.displayName = id; p.kind = kind;
            p.spotThreshold = spot; p.accentThreshold = accentSpot;
            p.butterflyBand = band; p.butterflyBandSoftness = bandSoftness;
            p.rarityWeight = weight;
            EditorUtility.SetDirty(p);
            return p;
        }
        var solid = Pattern("Solid", BettaPattern.Kind.Solid, 0.58f, 0.3f, 35f);
        var bicolor = Pattern("Bicolor", BettaPattern.Kind.Bicolor, 0.58f, 0.3f, 25f);
        var butterfly = Pattern("Butterfly", BettaPattern.Kind.Butterfly, 0.58f, 0.3f, 15f);
        var marble = Pattern("Marble", BettaPattern.Kind.Marble, 0.5f, 0.36f, 12f);
        var koi = Pattern("Koi", BettaPattern.Kind.Koi, 0.58f, 0.3f, 8f);
        var dragon = Pattern("Dragon", BettaPattern.Kind.Dragon, 0.58f, 0.3f, 5f);
        // Butterfly with an early, wide fade: body and fin roots in the primary, fins fade into the secondary.
        var pearl = Pattern("Pearl", BettaPattern.Kind.Butterfly, 0.58f, 0.3f, 4f, 0.3f, 0.22f);

        BettaVariant Variant(string name, BettaTailType tail, BettaPattern pattern, string primary, string secondary, string accent,
            float iridescence, float head, float fin, int seed)
        {
            var v = Load<BettaVariant>(Dir + "/Variants/" + name.Replace(" ", "") + ".asset");
            v.displayName = name; v.tail = tail; v.pattern = pattern;
            v.primary = Hex(primary); v.secondary = Hex(secondary); v.accent = Hex(accent);
            v.iridescence = iridescence; v.headScale = head; v.finLength = fin; v.seed = seed;
            EditorUtility.SetDirty(v);
            return v;
        }
        var variants = new[]
        {
            Variant("Royal Blue Halfmoon", halfmoon, solid, "#1F4FD1", "#3D7BFF", "#9FD3FF", 0.75f, 1f, 1f, 11),
            Variant("Red Plakat", plakat, solid, "#C8102E", "#E0303F", "#FF9AA2", 0.3f, 1.08f, 1f, 3),
            Variant("Mustard Gas Veiltail", veiltail, bicolor, "#1E3A8A", "#E8B32A", "#7FB8FF", 0.55f, 1f, 1.05f, 5),
            Variant("Koi Galaxy Plakat", plakat, koi, "#E8562A", "#F4EFE6", "#1A1A1A", 0.35f, 1.05f, 1f, 21),
            Variant("Black Orchid Crowntail", crowntail, butterfly, "#281E46", "#965AD2", "#8E6CFF", 0.9f, 1f, 1f, 8),
            Variant("White Opal Halfmoon", halfmoon, solid, "#EDE8F2", "#F7F3FA", "#BCD7FF", 0.85f, 0.96f, 1.1f, 2),
            Variant("Butterfly Delta", delta, butterfly, "#D7263D", "#F2EDE4", "#FFB3BC", 0.4f, 1f, 1f, 9),
            Variant("Copper Dragon Double Tail", doubletail, dragon, "#8A2A1A", "#B5562C", "#E9C9A0", 0.9f, 1.12f, 0.95f, 14),
            Variant("Blue Marble Halfmoon", halfmoon, marble, "#2B3A8F", "#EDE6DA", "#C2185B", 0.5f, 1f, 1f, 33),
            Variant("Aqua Pearl Rosetail", rosetail, pearl, "#F4F1EC", "#2EC4C6", "#BFF5F2", 0.9f, 0.98f, 1.12f, 7),
        };

        var library = Load<BettaVariantLibrary>(LibraryPath);
        library.variants = variants;
        library.tails = new[] { plakat, veiltail, delta, halfmoon, crowntail, doubletail, rosetail };
        library.patterns = new[] { solid, bicolor, butterfly, marble, koi, dragon, pearl };
        library.palette = new[] { "#C8102E", "#1F4FD1", "#E8562A", "#2A9D8F", "#6B3FA0", "#E8B32A", "#1B1530", "#D7263D", "#3D7BFF", "#8A2A1A", "#EDE8F2", "#0F766E" }
            .Select(Hex).ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log("[Lumaquarium] Built Betta variant presets in " + Dir);
        return library;
    }

    static readonly string[] HalfmoonFins = { "Fin_Caudal_Halfmoon", "Fin_Dorsal_Halfmoon", "Fin_Anal_Halfmoon" };

    static T Load<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
}
