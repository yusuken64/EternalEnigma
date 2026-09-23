using System.Linq;
using UnityEngine;

// Moves hero classes between live TownAlly objects and save data.
// Recruits: the prefab is authoritative. Protagonist: the saved ids are authoritative.
public static class HeroClassBinding
{
	public static void Capture(TownAlly ally, TownAllyData data)
	{
		if (ally == null || data == null) return;
		data.PrimaryClassId = ally.PrimaryClass != null ? ally.PrimaryClass.Id : "";
		data.SecondaryClassId = ally.SecondaryClass != null ? ally.SecondaryClass.Id : "";
	}

	// Convenience for building new save entries from a prefab.
	public static TownAllyData FromPrefab(TownAlly prefab, TownAllyData data)
	{
		Capture(prefab, data);
		return data;
	}

	public static bool IsProtagonist(GameSaveData save, TownAllyData data)
	{
		if (save == null || data == null) return false;
		if (!string.IsNullOrEmpty(save.ProtagonistId)) return data.AllyId == save.ProtagonistId;
		return ReferenceEquals(save.TownSaveData?.RecruitedAlliesData?.FirstOrDefault(), data);
	}

	// Call right after instantiating a TownAlly from saved data.
	public static void Apply(TownAlly instance, TownAllyData data, GameSaveData save)
	{
		if (instance == null || data == null) return;
		if (IsProtagonist(save, data))
		{
			if (string.IsNullOrEmpty(data.PrimaryClassId)) { Capture(instance, data); return; }
			var catalog = ClassCatalog.Load();
			var primary = catalog != null ? catalog.Get(data.PrimaryClassId) : null;
			if (primary == null)
			{
				Debug.LogWarning($"Protagonist class '{data.PrimaryClassId}' is not in the class catalog; keeping the prefab class.");
				Capture(instance, data);
				return;
			}
			var secondary = catalog.Get(data.SecondaryClassId);
			instance.PrimaryClass = primary;
			instance.SecondaryClass = secondary != null && secondary.Id != primary.Id ? secondary : null;
			Capture(instance, data);
			return;
		}
		string prefabPrimary = instance.PrimaryClass != null ? instance.PrimaryClass.Id : "";
		string prefabSecondary = instance.SecondaryClass != null ? instance.SecondaryClass.Id : "";
		if ((!string.IsNullOrEmpty(data.PrimaryClassId) && data.PrimaryClassId != prefabPrimary) ||
			(!string.IsNullOrEmpty(data.SecondaryClassId) && data.SecondaryClassId != prefabSecondary))
			Debug.LogWarning($"Saved class for '{data.AllyId}' ({data.PrimaryClassId}/{data.SecondaryClassId}) differs from its prefab; using the prefab.");
		Capture(instance, data);
	}
}
