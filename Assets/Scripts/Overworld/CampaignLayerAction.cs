using System;
using TWC;
using TWC.Actions;
using TWC.OdinSerializer;

/// <summary>Feeds an already validated core mask into TWC's normal map-finalization pipeline.</summary>
[Serializable]
public sealed class CampaignLayerAction : TWCBlueprintAction, ITWCAction
{
    [OdinSerialize] private bool[,] cells;
    public CampaignLayerAction(bool[,] cells) { this.cells = (bool[,])cells.Clone(); }
    public override bool ShowFoldout => false;
    public float GetGUIHeight() => 0f;
    public ITWCAction Clone() => new CampaignLayerAction(cells);
    public bool[,] Execute(bool[,] map, TileWorldCreator creator)
    {
        if (cells.GetLength(0) != map.GetLength(0) || cells.GetLength(1) != map.GetLength(1))
            throw new InvalidOperationException("Campaign layer dimensions do not match the TWC asset.");
        return (bool[,])cells.Clone();
    }
}
