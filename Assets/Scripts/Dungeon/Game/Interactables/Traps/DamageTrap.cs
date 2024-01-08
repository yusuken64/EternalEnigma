using System.Collections.Generic;

public class DamageTrap : Trap
{
	public int TrapDamage;

	internal override string GetInteractionText()
	{
		return "Damage Trap";
	}

	internal override List<GameAction> GetTrapSideEffects(Character character)
	{
		VisualObject.gameObject.SetActive(true);

		if (character is Player player)
		{
			var hit = UnityEngine.Random.value > 0.5f;
			return new List<GameAction>()
			{
				new TakeDamageAction(character, character, TrapDamage, true, hit)
			};
		}
		return new();
	}
}