using static ClassContentBuilder;

public static class ClassContentShared
{
	public const string Folder = "Shared";

	public static void Build()
	{
		ConfigurePassive(Upsert(Folder, ExplorationSkillNames.Mining), 1, Cost(1), "Can harvest ore points.", null);
		ConfigurePassive(Upsert(Folder, ExplorationSkillNames.Harvesting), 1, Cost(1), "Can harvest plant points.", null);
		ConfigurePassive(Upsert(Folder, ExplorationSkillNames.Foraging), 1, Cost(1), "Can harvest forage points.", null);
		ConfigurePassive(Upsert(Folder, "SP Up"), 5, Cost(1), "+2 SPMax.", new StatModification { SPMax = 2 });
		ConfigurePassive(Upsert(Folder, "Vigor"), 5, Cost(3), "+10 HPMax.", new StatModification { HPMax = 10 }).RankStep(5);
		UnityEditor.AssetDatabase.SaveAssets();
		UnityEngine.Debug.Log("Shared class skills written.");
	}
}
