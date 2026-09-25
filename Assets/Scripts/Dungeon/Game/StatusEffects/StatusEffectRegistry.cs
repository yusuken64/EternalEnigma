using System.Linq;

public static class StatusEffectRegistry
{
	public static T Get<T>() where T : StatusEffect =>
		Game.Instance?.StatusEffectPrefabs?.OfType<T>().FirstOrDefault();

	public static StatusEffect GetByName(string effectName) =>
		Game.Instance?.StatusEffectPrefabs?.FirstOrDefault(x => x != null && x.GetEffectName() == effectName);
}
