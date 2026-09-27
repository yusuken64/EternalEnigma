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
		if (!CanTrigger(character)) return new();
		VisualObject.gameObject.SetActive(true);

		if (UnityEngine.Random.value >= 0.5f)
			return new() { new TrapFeedbackAction(character, "Evaded", $"{GameMessages.Name(character)} evaded the Damage Trap; no damage was taken.") };
		return new List<GameAction>()
			{
				new TrapFeedbackAction(character, "Damage Trap", $"{GameMessages.Name(character)} triggered the Damage Trap."),
				new TakeDamageAction(character, character, TrapDamage) { Environmental = true }
			};
	}
}
