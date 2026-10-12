using System.Linq;
using EternalEnigma.Core.Generation;
using UnityEngine;

internal static class FinaleEnemyStats
{
    internal static Enemy Prefab(string family, bool boss)
    {
        if (Game.Instance?.Finale == null) return null;
        return Resources.Load<Enemy>("Finale/" + (boss ? "Boss_" : "Enemy_") + FinaleRoster.PrefabStem(family)) ??
            (boss ? LargeBossBehavior.Prefab(family) : Game.Instance.EnemyManager.EnemyPrefabs.FirstOrDefault(e => e != null && e.name == "Enemy_" + FinaleRoster.PrefabStem(family)));
    }

    internal static void Apply(Enemy enemy, int difficulty, bool boss)
    {
        difficulty = Mathf.Clamp(difficulty, 1, 27); // The existing endgame spawn curve caps at floor 27.
        var candidates = Game.Instance.EnemyManager.SpawnDefinitions.Where(d => d.FloorMin <= difficulty && d.FloorMax >= difficulty)
            .Select(d => d.EnemyCharacterPrefab).Where(e => e != null && !e.IsBoss && e.name != "Enemy_MetalSlime").Distinct().ToArray();
        if (candidates.Length == 0) throw new System.InvalidOperationException("No endgame stat curve is available.");
        int Median(System.Func<Enemy, int> select) => candidates.Select(select).OrderBy(n => n).ElementAt(candidates.Length / 2);
        var stats = enemy.StartingStats;
        stats.HPMax = Median(e => e.StartingStats.HPMax) * (boss ? 5 : 1);
        stats.Strength = Mathf.CeilToInt(Median(e => e.StartingStats.Strength) * (boss ? 1.25f : 1f));
        stats.MagicPower = Mathf.Max(stats.MagicPower, stats.Strength);
        stats.Defense = Median(e => e.StartingStats.Defense);
        stats.EXPOnKill = Median(e => e.StartingStats.EXPOnKill) * (boss ? 5 : 1);
        stats.ActionsPerTurnMax = 1; stats.AttacksPerTurnMax = 1;
        enemy.FootPrint = FootPrint.Size1x1;
        enemy.IsBoss = boss;
        if (enemy.GetComponent<EnemyBehavior>() is { } behavior)
        { behavior.PackSize = 1; behavior.WaitUntilAttacked = false; behavior.Mimic = false; behavior.AlwaysFlees = false; }
        enemy.InvalidateCachedStats();
    }
}
