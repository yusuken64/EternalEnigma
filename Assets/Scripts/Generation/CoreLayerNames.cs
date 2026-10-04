using System.Linq;
using EternalEnigma.Core.World;

public static class CoreLayerNames
{
    public static readonly string[] Dungeon = DungeonLayers.All.ToArray();
    public static readonly string[] Town = TownLayers.All.Concat(TownLayers.Detail).Concat(TownLayers.InteriorLayers).ToArray();
}
