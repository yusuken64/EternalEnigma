using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Catalog assets describe offers; resolving a town never edits those shared assets.
public static class TownShopCatalog
{
    public static int Tier(string townId) => Common.Instance?.CampaignContext?.Campaign.Locations
        .FirstOrDefault(l => l.Id == townId && l.Kind == EternalEnigma.Core.Progression.LocationKind.Town)?.Tier ?? 0;

    public static List<TownShopOffer> Resolve(TownBuildingDefinition building, int tier) =>
        building.ShopCatalog.Where(o => o.Item != null && o.MinimumTier <= tier).ToList();

    public static int SellPrice(InventoryItem item)
    {
        if (item?.ItemDefinition == null || item.ItemDefinition.IsProgressionItem || item.StackStock <= 0) return 0;
        int remaining = item.StackStock ?? 1;
        if (item.ItemDefinition is MaterialItemDefinition material)
            return (int)Math.Min(int.MaxValue, (long)material.SellValue * remaining);
        decimal fallback = item.ItemDefinition is EquipmentItemDefinition equipment
            ? Math.Max(100, 50 * (equipment.StatModification.Strength + equipment.StatModification.Defense)) : 100;
        var prices = Resources.LoadAll<TownBuildingDefinition>("Towns/Buildings")
            .SelectMany(b => b.ShopCatalog).Where(o => o.Item == item.ItemDefinition && o.StackCount > 0)
            .Select(o => (decimal)o.Price / o.StackCount);
        decimal unitPrice = prices.DefaultIfEmpty(fallback).Min();
        return (int)Math.Min(int.MaxValue, decimal.Floor(unitPrice * remaining / 4));
    }
}
