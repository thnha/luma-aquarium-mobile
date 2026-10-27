using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class FrontEndAssetImporter : AssetPostprocessor
{
    internal const string Root = "Assets/_Project/UI";

    [Serializable] private sealed class Manifest { public Entry[] sprites; }
    [Serializable] private sealed class Entry { public string name; public int[] borderLTRB; }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root + "/", StringComparison.Ordinal)
            || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        // Icons show at about 260 px on a 1080-wide canvas; the 1254 px action sources need no more than 512.
        importer.maxTextureSize = assetPath.StartsWith(Root + "/Icons/", StringComparison.Ordinal) ? 512 : 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = BorderFor(Path.GetFileNameWithoutExtension(assetPath));
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    internal static Vector4 BorderFor(string spriteName)
    {
        string file = Root + "/sprite-manifest.json";
        if (!File.Exists(file)) return Vector4.zero;
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
        if (manifest?.sprites == null) return Vector4.zero;
        foreach (var entry in manifest.sprites)
        {
            if (entry.name != spriteName || entry.borderLTRB == null || entry.borderLTRB.Length != 4) continue;
            int[] b = entry.borderLTRB;
            // The manifest is L,T,R,B; Unity expects L,B,R,T.
            return new Vector4(b[0], b[3], b[2], b[1]);
        }
        return Vector4.zero;
    }
}
