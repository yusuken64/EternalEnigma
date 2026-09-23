using UnityEngine;

/// <summary>Stationary NPC standing inside a carved shop interior; opens the shop's dialog when the player faces and interacts with it.</summary>
public class ShopVendor : TownCharacter
{
    public TownBuildingDefinition Building;

    /// <summary>Placeholder visual used when a building has no authored VendorPrefab.</summary>
    public static ShopVendor CreateDefault(Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "ShopVendor";
        go.transform.SetParent(parent);
        go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
        var vendor = go.AddComponent<ShopVendor>();
        vendor.VisualParent = go;
        return vendor;
    }
}
