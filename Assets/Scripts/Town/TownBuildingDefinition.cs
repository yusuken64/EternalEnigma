using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JuicyChickenGames.Menu;

[CreateAssetMenu(menuName = "Game/Town/Building")]
public class TownBuildingDefinition : ScriptableObject
{
    public string Id;
    public string DisplayName;
    public TownBuilding Prefab;
    [Tooltip("Matches a TownMenu dialog binding. New interactions can add their own dialog type and binding.")]
    public string DialogId;
    [Tooltip("Optional self-contained view. When supplied, the caller can add this building without editing the Town scene.")]
    public Dialog DialogPrefab;
    public List<TownShopOffer> ShopCatalog = new();

    public void Validate()
    {
        if (ShopCatalog.Any(o => o == null || o.Item == null || o.Price < 0 || o.Quantity < 1 || o.StackCount < 1))
            throw new InvalidOperationException($"Building '{Id}' has an invalid shop offer.");
        if (ShopCatalog.Select(o => o.Item).Distinct().Count() != ShopCatalog.Count)
            throw new InvalidOperationException($"Building '{Id}' has duplicate shop items.");
    }
}

[Serializable]
public class TownShopOffer
{
    public ItemDefinition Item;
    [Min(0)] public int Price = 100;
    [Min(1)] public int Quantity = 1;
    [Min(1)] public int StackCount = 1;
}
