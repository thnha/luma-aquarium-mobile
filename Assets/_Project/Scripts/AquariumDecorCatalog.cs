using System;
using UnityEngine;

public enum AquariumDecorCategory { Plants, Wood, Rocks }

[Serializable]
public sealed class AquariumDecorOffer
{
    public string id;
    public string displayName;
    public string description;
    public AquariumDecorCategory category;
    public int price;
    public AquariumDecorItem prefab;
    public Sprite portrait;
    public bool IsValid => !string.IsNullOrWhiteSpace(id) && price > 0 && prefab != null && portrait != null;
}

public sealed class AquariumDecorCatalog : ScriptableObject
{
    public AquariumDecorOffer[] offers = new AquariumDecorOffer[0];
    public AquariumDecorOffer Find(int kind) => Array.Find(offers, x => x.prefab != null && x.prefab.kind == kind);
}
