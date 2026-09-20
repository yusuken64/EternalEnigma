using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DemoDungeonLoadout : ScriptableObject
{
    public List<Skill> Skills = new();
    public List<ItemDefinition> Items = new();
    public Enemy PracticeTargetPrefab;
    public static DemoDungeonLoadout Load() => Resources.Load<DemoDungeonLoadout>("DemoDungeon/Loadout");

    internal void Apply(Game game)
    {
        var inventory = game.PlayerController.Inventory;
        inventory.Clear();
        inventory.MaxItems = Mathf.Max(inventory.MaxItems, Items.Count + 10);
        foreach (var item in Items) inventory.Add(item.AsInventoryItem(item.StackMax > 0 ? 20 : null));
        foreach (var ally in game.Allies)
        {
            ally.Skills.AddRange(Skills.Select(Instantiate));
            ally.AllyStrategy = AllyStrategy.HoldPosition;
            ally.BaseStats.HPMax = 100;
            ally.BaseStats.SPMax = 100;
            ally.InvalidateCachedStats();
            ally.Vitals.HP = 60;
            ally.Vitals.SP = ally.FinalStats.SPMax;
            ally.SyncDisplayedStats();
        }
    }

    internal IEnumerator PreparePracticeRoom(Game game)
    {
        var dungeon = game.CurrentDungeon;
        var center = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
            Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
            .Where(p => Enumerable.Range(-2, 5).All(x => Enumerable.Range(-2, 5)
                .All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))))
            .OrderBy(p => TileWorldDungeon.ChevDistance(p, game.Allies[0].TilemapPosition)).First();
        var allyOffsets = new[] { Vector3Int.zero, Vector3Int.left, Vector3Int.down };
        for (int i = 0; i < game.Allies.Count; i++) game.Allies[i].SetPosition(center + allyOffsets[i % allyOffsets.Length]);
        foreach (var offset in new[] { new Vector3Int(2, 0), new Vector3Int(2, 1), new Vector3Int(0, 2) })
        {
            var enemy = Instantiate(PracticeTargetPrefab, game.transform);
            enemy.CharacterName = "Practice Target";
            enemy.InitialzeVitalsFromStats();
            enemy.BaseStats.HPMax = 500;
            enemy.BaseStats.DropRate = 0;
            enemy.InvalidateCachedStats();
            enemy.Vitals.HP = enemy.FinalStats.HPMax;
            enemy.SyncDisplayedStats();
            enemy.SetPosition(center + offset);
            game.Enemies.Add(enemy);
            yield return null; // Enemy.Start installs policies; clear them after initialization.
            enemy.Policies.Clear();
        }
        game.Allies[0].CurrentFacing = Facing.Right;
    }
}
