using System.Linq;
using EternalEnigma.Core.World;
using TMPro;
using UnityEngine;

/// <summary>A freestanding painted sign, with lettering fixed to its bevelled face.</summary>
public sealed class WorldSign : MonoBehaviour
{
    // Row-major order in ArtSource/TownInteriors/BuildingSigns.svg.
    internal enum Emblem { Home, Bakery, Supplies, Items, Trainer, Inn, Dungeon, Gate, Statue, PublicBuilding, DistrictExit }
    private static readonly int AtlasTransform = Shader.PropertyToID("_MainTex_ST");
    internal TextMeshPro Caption { get; private set; }
    internal Emblem Symbol { get; private set; }

    internal static WorldSign AtBuilding(TownBuilding building, TownPlan plan, float size)
    {
        var definition = building.Definition;
        var symbol = definition.Id switch
        {
            "home" => Emblem.Home,
            "bakery" => Emblem.Bakery,
            "consumables" => Emblem.Supplies,
            _ => definition.DialogId switch
            {
                "shop" => Emblem.Items,
                "inn" => Emblem.Inn,
                "trainer" => Emblem.Trainer,
                "entrance" => Emblem.Dungeon,
                "statue" => Emblem.Statue,
                _ => Emblem.PublicBuilding
            }
        };
        return Create(building.transform, BesideDoor(plan, building.TilemapPosition.ToGridPoint()), size, symbol, building.Name);
    }

    internal static void AtDistrictBuilding(Transform district, TownPlan plan, TownInterior room, float size)
    {
        var (symbol, name) = (room.Spec.Kind, room.Spec.Theme) switch
        {
            (TownInteriorKind.Inn, _) => (Emblem.Inn, "Inn"),
            (TownInteriorKind.Trainer, _) => (Emblem.Trainer, "Training Hall"),
            (_, TownShopTheme.Bakery) => (Emblem.Bakery, "Bakery"),
            (_, TownShopTheme.Consumables) => (Emblem.Supplies, "Supplies"),
            (_, TownShopTheme.Equipment) => (Emblem.Items, "Equipment"),
            _ => (Emblem.Items, "Shop")
        };
        var facade = new Vector3(room.Door.X, room.Door.Y + 1, 0);
        Create(district, facade + BesideDoor(plan, room.Door), size, symbol, name);
    }

    private static Vector3 BesideDoor(TownPlan plan, GridPoint door)
    {
        var footprint = plan.Footprints.FirstOrDefault(f => f.Door.Equals(door));
        int Frontage(int side)
        {
            int span = 0;
            while (span < 2 && footprint != null &&
                footprint.Cells.Contains(new GridPoint(door.X + (span + 1) * side, door.Y + 1))) span++;
            return span;
        }

        // Favor the longer stretch of front wall. Leave a full tile of clearance at the door.
        int side = Frontage(-1) > Frontage(1) ? -1 : 1;
        return new Vector3(.5f + side * 1.46f, .04f, 0);
    }

    internal static WorldSign Create(Transform parent, Vector3 mountInCells, float size, Emblem symbol, string name)
    {
        var model = Instantiate(GameContent.Load<GameObject>("Towns/BuildingSign"), parent, false);
        model.name = "Building sign";
        model.transform.localPosition = mountInCells * size;
        model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        model.transform.localScale = Vector3.one * size;
        var sign = model.AddComponent<WorldSign>();
        sign.Symbol = symbol;
        var paint = model.GetComponentInChildren<MeshRenderer>();
        paint.sharedMaterial = GameContent.Load<Material>("Towns/BuildingSignPaint");
        var properties = new MaterialPropertyBlock();
        const int columns = 4, rows = 3;
        int tile = (int)symbol;
        properties.SetVector(AtlasTransform, new Vector4(1f / columns, 1f / rows,
            (tile % columns) / (float)columns, (rows - 1 - tile / columns) / (float)rows));
        paint.SetPropertyBlock(properties);

        sign.Caption = sign.PaintLine(name);
        return sign;
    }

    // Distances are measured along the board's surface in model units.
    private TextMeshPro PaintLine(string text)
    {
        var label = new GameObject("Painted name").AddComponent<TextMeshPro>();
        label.transform.SetParent(transform, false);
        var tilt = Quaternion.Euler(47, 0, 0);
        label.transform.localRotation = tilt;
        label.transform.localPosition = new Vector3(0, 1.055f, -.5f) +
            tilt * new Vector3(0, -.29f, -.012f);
        label.rectTransform.sizeDelta = new Vector2(1.49f, .22f);
        GameTypography.Apply(label, GameTextRole.Heading);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1.25f;
        label.fontSizeMax = 3f;
        label.fontSize = 3f;
        label.color = new Color(1f, .94f, .77f);
        label.text = text;
        return label;
    }
}
