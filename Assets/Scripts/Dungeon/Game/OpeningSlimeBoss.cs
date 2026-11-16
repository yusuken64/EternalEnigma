using System.Linq;
using UnityEngine;

/// <summary>The opening boss tests the existing preparation and combat lessons.</summary>
internal static class OpeningSlimeBoss
{
    internal static void Configure(Enemy enemy, EnemyManager catalog)
    {
        enemy.IsBoss = true;
        enemy.DisplayName = "Big Slime";
        enemy.StartingStats.HPMax = 60;
        enemy.StartingStats.Strength = 4;
        enemy.StartingStats.Defense = 1;
        enemy.StartingStats.EXPOnKill = 20;
        var behavior = enemy.GetComponent<LargeBossBehavior>() ?? enemy.gameObject.AddComponent<LargeBossBehavior>();
        behavior.Family = "Slime";
        behavior.WarningTurns = 2;
        behavior.SplashRadiusOverride = 1;
        behavior.CooldownTurns = 3;
        behavior.RecoveryTurns = 1;
        var split = enemy.GetComponent<BigSlimeDieResponseBehavior>() ?? enemy.gameObject.AddComponent<BigSlimeDieResponseBehavior>();
        split.EnemyToSpawnPrefab = catalog.EnemyPrefabs.First(e => e != null && e.name == "Enemy_Slime");
        split.SlimesToSpawn = 2;
        enemy.InvalidateCachedStats();
    }
}
