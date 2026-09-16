using NUnit.Framework;
using UnityEngine;

public class DungeonSightTests
{
    private static bool[,] Open(int size)
    {
        var map = new bool[size, size];
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++) map[x, y] = true;
        return map;
    }

    [Test]
    public void WallsAreVisibleButBlockTilesBehindThem()
    {
        var map = Open(7);
        for (int y = 0; y < 7; y++) map[3, y] = false;
        var tiles = DungeonSight.VisibleTiles(map, new Vector3Int(1, 3), 8);
        Assert.That(tiles.Contains(new Vector3Int(3, 3)), Is.True);
        Assert.That(tiles.Contains(new Vector3Int(4, 3)), Is.False);
        Assert.That(tiles.Contains(new Vector3Int(5, 5)), Is.False);
        map[3, 3] = true;
        Assert.That(DungeonSight.HasLineOfSight(map, new Vector3Int(1, 3), new Vector3Int(5, 3)), Is.True);
    }

    [Test]
    public void CorridorSightHasSymmetricEdgesAndCannotCutCorners()
    {
        var map = Open(7);
        var center = new Vector3Int(3, 3);
        Assert.That(DungeonSight.VisibleTiles(map, center, 1).Count, Is.EqualTo(9));
        Assert.That(DungeonSight.VisibleTiles(map, center, 1).Contains(new Vector3Int(5, 3)), Is.False);
        map[4, 3] = false;
        Assert.That(DungeonSight.HasLineOfSight(map, center, new Vector3Int(4, 4)), Is.False);
        Assert.That(DungeonSight.HasLineOfSight(map, new Vector3Int(4, 4), center), Is.False);
        Assert.That(Character.Contains2D(new BoundsInt(2, 2, 0, 3, 3, 1), new Vector3Int(5, 3)), Is.False);
    }

    [Test]
    public void RoomClassificationAndMapEdgesAreExplicit()
    {
        var corridor = new bool[7, 7];
        for (int x = 0; x < 7; x++) corridor[x, 3] = true;
        Assert.That(DungeonSight.IsRoom(corridor, new Vector3Int(3, 3)), Is.False);
        corridor[3, 4] = corridor[4, 4] = true;
        Assert.That(DungeonSight.IsRoom(corridor, new Vector3Int(3, 3)), Is.True);
        Assert.That(DungeonSight.VisibleTiles(Open(7), Vector3Int.zero, 8).Count, Is.EqualTo(49));
        Assert.That(DungeonSight.VisibleTiles(corridor, new Vector3Int(-1, 0), 8), Is.Empty);
    }

    [Test]
    public void UnseenPresentationActionsDoNotWaitOrInstantiateEffects()
    {
        // Null scene dependencies ensure skipped effects do not access audio, prefabs, or targets.
        GameAction[] actions = { new RangedAttackAction(), new ThrowItemAction(),
            new LevelUpAction(), new UseInventoryItemAction(), new SkillAction(), new ExplosionAction() };
        for (int i = 0; i < actions.Length; i++)
            Assert.That(actions[i].ExecuteRoutine(null, true).MoveNext(), Is.False, actions[i].GetType().Name);
    }
}
