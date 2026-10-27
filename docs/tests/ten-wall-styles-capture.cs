if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Run in MainAquarium Play Mode");
var ids = new[] { "wall_basalt", "wall_limestone", "wall_sandstone", "wall_amazon_roots", "wall_frosted_aqua",
    "wall_matte_black", "wall_dragon_stone", "wall_pearl_marble", "wall_turquoise_mosaic", "wall_sunken_relic" };
var decoration = UnityEngine.Object.FindFirstObjectByType<AquariumDecoration>();
var tank = UnityEngine.Object.FindFirstObjectByType<AquariumTank>();
if (decoration == null || tank == null) throw new System.Exception("MainAquarium must be loaded and initialized");
var camera = UnityEngine.Camera.main;
var previousWall = new Lumaquarium.UI.FishShopInventory(decoration.inventoryStorageKey).ActiveWall;
string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName, "docs/previews/TenWallStyles");
System.IO.Directory.CreateDirectory(directory);
var target = UnityEngine.RenderTexture.GetTemporary(540, 960, 24);
var previousTarget = camera.targetTexture;
var previousActive = UnityEngine.RenderTexture.active;
var image = new UnityEngine.Texture2D(540, 960, UnityEngine.TextureFormat.RGB24, false);
try
{
    foreach (string id in ids)
    {
        tank.ApplyWall(id);
        camera.targetTexture = target;
        camera.Render();
        UnityEngine.RenderTexture.active = target;
        image.ReadPixels(new UnityEngine.Rect(0, 0, 540, 960), 0, 0);
        image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, id + ".png"), image.EncodeToPNG());
    }
    return new { captured = ids.Length, directory, frame = UnityEngine.Time.frameCount, previousWallRestored = previousWall };
}
finally
{
    camera.targetTexture = previousTarget;
    UnityEngine.RenderTexture.active = previousActive;
    UnityEngine.RenderTexture.ReleaseTemporary(target);
    UnityEngine.Object.DestroyImmediate(image);
    tank.ApplyWall(previousWall);
}
