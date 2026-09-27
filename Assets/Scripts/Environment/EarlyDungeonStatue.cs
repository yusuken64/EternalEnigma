using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>A harmless Black Knight-shaped statue foreshadows the later Stone Hulk equivalent.</summary>
public static class EarlyDungeonStatue
{
    public static DungeonProp Place(TileWorldDungeon dungeon, DungeonTheme theme)
    {
        var prefab = Resources.Load<GameObject>("DungeonProps/StoneHulkStatue");
        if (prefab == null) return null;
        var start = dungeon.GetStartPosition();
        var stairs = dungeon.Floor.Stairs.ToCell();
        var candidates = new List<Vector3Int>();
        for(int y=1;y<dungeon.dungeonHeight-1;y++) for(int x=1;x<dungeon.dungeonWidth-1;x++)
        {
            var cell = new Vector3Int(x,y);
            if (!dungeon.IsWalkable(cell) || TileWorldDungeon.ChevDistance(start,cell)<4 || TileWorldDungeon.ChevDistance(stairs,cell)<3 ||
                dungeon.GetInteractable(cell)!=null || Game.Instance.AllCharacters.Any(c=>c!=null && c.OverlapsWith(Character.ToBounds(cell)))) continue;
            // Back the statue against the far wall so foreground walls cannot hide it.
            if (!dungeon.IsFloorCell(cell+Vector3Int.up) &&
                new[] { Vector3Int.down,Vector3Int.left,Vector3Int.right }.All(d=>dungeon.IsFloorCell(cell+d))) candidates.Add(cell);
        }
        foreach(var cell in candidates.OrderBy(c=>TileWorldDungeon.ChevDistance(c,start)).ThenBy(c=>DungeonPresentation.Hash(dungeon.Floor.Seed,c.x,c.y)))
        {
            // Include diagonal corner rules; every remaining floor destination must stay reachable.
            bool Walkable(Vector3Int p) => p!=cell && dungeon.IsWalkable(p);
            var seen=new HashSet<Vector3Int> { start }; var pending=new Queue<Vector3Int>();pending.Enqueue(start);
            while(pending.Count>0)
            {
                var from=pending.Dequeue();
                foreach(var facing in GridMovement.GetValidDirections(from,Walkable))
                {
                    var next=from+GridMovement.GetFacingOffset(facing);
                    if(seen.Add(next)) pending.Enqueue(next);
                }
            }
            int expected=0;
            for(int y=0;y<dungeon.dungeonHeight;y++) for(int x=0;x<dungeon.dungeonWidth;x++) if(Walkable(new Vector3Int(x,y))) expected++;
            if(seen.Count!=expected) continue;
            var prop=DungeonProp.Create(dungeon,new DungeonScenery(cell.ToGridPoint(),DungeonSceneryKind.Destructible,40,0,SceneryReward.None),theme,prefab);
            prop.name="Stone Hulk statue";
            return prop;
        }
        return null;
    }
}
