using UnityEditor;
using UnityEngine;

namespace EternalEnigma.Editor.Classes
{
    public static class TuneEnemyResistances
    {
        public static readonly (string Prefab, int Fire, int Ice, int Lightning, bool Boss)[] Table = new[]
        {
            ("Enemy_Bat", 0, 0, -1, false),
            ("Enemy_BattleBee", -1, 0, 0, false),
            ("Enemy_Beholder", 0, 0, 1, false),
            ("Enemy_BishopKnight", 0, 0, -1, false),
            ("Enemy_BlackKnight", 0, 0, -1, false),
            ("Enemy_Cactus", -1, 1, 0, false),
            ("Enemy_ChestMonster", -1, 0, 0, false),
            ("Enemy_CrabMonster", 0, 1, -1, false),
            ("Enemy_Cyclops", 0, -1, 0, false),
            ("Enemy_DemonKing", 1, 1, 1, true),
            ("Enemy_Dragon", 2, -1, 0, true),
            ("Enemy_EvilMage", 1, 0, 0, false),
            ("Enemy_Fishman", 1, 1, -1, false),
            ("Enemy_FylingDemon", 1, -1, 0, false),
            ("Enemy_Golem", 1, 0, 2, false),
            ("Enemy_LargeSlime", 0, -1, 0, false),
            ("Enemy_LizardWarrior", 0, -1, 0, false),
            ("Enemy_MonsterPlant", -1, 0, 1, false),
            ("Enemy_MushroomAngry", -1, 0, 0, false),
            ("Enemy_MushroomSmile", -1, 0, 0, false),
            ("Enemy_NagaWizard", 0, 1, -1, false),
            ("Enemy_Orc", 0, 0, 0, false),
            ("Enemy_RatAssassin", 0, 0, 0, false),
            ("Enemy_Salamander", 2, -1, 0, false),
            ("Enemy_Skeleton", -1, 1, 0, false),
            ("Enemy_Slime", 0, -1, 0, false),
            ("Enemy_Slime_Big", 0, -1, 0, false),
            ("Enemy_Specter", -1, 1, 1, false),
            ("Enemy_Spider", -1, 0, 0, false),
            ("Enemy_StingRay", 1, 1, -1, false),
            ("Enemy_TurtleShell", 1, 1, -1, false),
            ("Enemy_Werewolf", -1, 0, 0, false),
            ("Enemy_WormMonster", 0, -1, 1, false),
        };

        public static void Build()
        {
            int count = 0;
            foreach (var row in Table)
            {
                string path = $"Assets/Prefabs/Dungeon/Enemies/{row.Prefab}.prefab";

                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"Prefab not found: {path}");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var enemy = root.GetComponent<Enemy>();
                    if (enemy == null)
                    {
                        Debug.LogError($"Enemy component not found on {path}");
                        continue;
                    }

                    enemy.StartingStats ??= new StartingStats();
                    enemy.StartingStats.FireResistance = row.Fire;
                    enemy.StartingStats.IceResistance = row.Ice;
                    enemy.StartingStats.LightningResistance = row.Lightning;
                    enemy.IsBoss = row.Boss;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    count++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"Tuned {count} enemy resistances and boss flags.");
        }
    }
}
