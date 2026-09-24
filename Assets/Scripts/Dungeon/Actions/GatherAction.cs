using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class GatherAction : GameAction
{
	private readonly GatheringPoint point;
	private readonly Character gatherer;
	private string message;
	private Color color = Color.white;

	public GatherAction() { }
	public GatherAction(GatheringPoint point, Character gatherer) { this.point = point; this.gatherer = gatherer; }

	internal static int Yield(int rank) => rank <= 0 ? 0 : 1 + (rank >= 3 ? 1 : 0) + (rank >= 5 ? 1 : 0);

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (point == null) return new();
		string skill = ExplorationSkillNames.ForKind(point.Kind);
		int rank = Mathf.Max(ExplorationPassives.PartyBestRank(skill), ExplorationPassives.PartyBestRank(ExplorationSkillNames.Survey));
		if (rank <= 0) { message = "Needs " + skill; return new(); }

		int amount = Yield(rank);
		var rng = new System.Random(point.Roll);
		int scavenger = ExplorationPassives.PartyBestRank(ExplorationSkillNames.Scavenger);
		if (scavenger > 0 && rng.NextDouble() < 0.20 + 0.05 * (scavenger - 1)) amount *= 2;

		var material = MaterialCatalog.ForKind(point.Kind);
		int added = MaterialCatalog.AddToInventory(Game.Instance.PlayerController.Inventory, material, amount);
		if (added <= 0) { message = "Bag is full"; return new(); }

		Game.Instance.CurrentDungeon.RemoveInteractable(point);
		message = $"+{added} {material.ItemName}";
		color = Color.green;
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || string.IsNullOrEmpty(message)) yield break;
		var who = gatherer != null ? gatherer : character;
		if (who != null) Game.Instance.DoFloatingText(message, color, who.transform.position);
		yield return null;
	}

	internal override bool IsValid(Character character) => point != null;
}
