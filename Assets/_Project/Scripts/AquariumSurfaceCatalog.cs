using System;
using UnityEngine;

// Tank wall and floor styles sold in the shop (Thành hồ, Cát nền). Element 0 of each list is free and has no
// texture: it keeps the tank's own look. AquariumTank applies the style in use (FishShopInventory).
[Serializable]
public sealed class AquariumWallStyle
{
    public string id, displayName, description;
    public int price;
    public Texture2D albedo;
    public Color tint = Color.white;
    public float tiling = 1.5f; // tiles per metre (Aquarium Hardscape _Tiling)
    public Sprite portrait;
}

[Serializable]
public sealed class AquariumSubstrateStyle
{
    public string id, displayName, description;
    public int price;
    public Texture2D albedo;
    public Color tint = Color.white;
    public float tiling = 5;
    public bool pebbles = true; // show the loose pebble bed on top
    public Color pebbleTint = Color.white;
    public Sprite portrait;
}

public sealed class AquariumSurfaceCatalog : ScriptableObject
{
    public AquariumWallStyle[] walls = new AquariumWallStyle[0];
    public AquariumSubstrateStyle[] substrates = new AquariumSubstrateStyle[0];

    public AquariumWallStyle Wall(string id) => Array.Find(walls, x => x.id == id) ?? (walls.Length > 0 ? walls[0] : null);
    public AquariumSubstrateStyle Substrate(string id) => Array.Find(substrates, x => x.id == id) ?? (substrates.Length > 0 ? substrates[0] : null);
}
