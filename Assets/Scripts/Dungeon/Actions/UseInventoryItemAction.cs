using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class UseInventoryItemAction : GameAction
{
	private Inventory inventory;
	private InventoryItem item;
	private Character user;
	private Character target;
	private InventoryItem inventoryTarget;
	private Vector3Int direction;
	private MissileTargeting.Hit missileHit;
	private UsableItemDefinition Definition => item?.ItemDefinition as UsableItemDefinition;

	public UseInventoryItemAction()
	{

	}
	public UseInventoryItemAction(Inventory inventory, Character character, InventoryItem item)
	{
		this.inventory = inventory;
		this.item = item;
		user = character;
		target = character;
	}
	internal UseInventoryItemAction WithTarget(Character selected) { target = selected; return this; }
	internal UseInventoryItemAction WithItem(InventoryItem selected) { inventoryTarget = selected; return this; }
	internal UseInventoryItemAction WithDirection(Vector3Int selected) { direction = selected; return this; }

	internal bool CanBegin(Character character)
	{
		if (character == null || character != user || item?.ItemDefinition == null || inventory == null ||
			(!inventory.InventoryItems.Contains(item) && !character.Equipment.IsEquipped(item)) ||
			(item.HasStacks && item.StackIsEmpty()) || character.Vitals.HP <= 0 ||
			character.StatusEffects.Any(s => !s.IsExpired() && (s.PreventsMenu() || s.Interupts(this)))) return false;
		var definition = Definition;
		if (definition == null) return item.ItemDefinition.ItemEffectDefinition != null;
		if (!definition.TargetingRules.IsConfigured) return false;
		if (definition.Targeting == SkillTargeting.InventoryItem) return definition.GetInventoryTargets(character).Any();
		return definition.ItemEffectDefinition != null && (definition.Targeting == SkillTargeting.Missile ||
			(definition.TargetingRules.RequiresSelection ? definition.TargetingRules.GetCharacters(character) :
			definition.TargetingRules.GetAffected(character, character)).Any());
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();
		var definition = Definition;
		List<GameAction> effects;
		if (definition?.Targeting == SkillTargeting.InventoryItem)
			effects = definition.InventoryEffects.Cast<InventorySkillEffect>().Select(e => e.Bind(character, inventoryTarget)).ToList();
		else
		{
			var recipients = definition == null ? new List<Character> { character } : definition.TargetingRules.GetAffected(character, target);
			if (definition?.Targeting == SkillTargeting.Missile)
			{
				missileHit = MissileTargeting.Trace(character, direction, definition.MissileRange);
				recipients = definition.TargetingRules.GetMissileAffected(character, missileHit);
			}
			effects = recipients.SelectMany(recipient => item.GetGameActions(character, recipient, inventory, item)).ToList();
		}
		bool consume = !AutoplayRunner.InfiniteResourcesFor(character) &&
			!(UnityEngine.Random.value < ClassPassives.ConsumableSaveChance(character));
		if (consume && item.HasStacks)
		{
			item.Decrement();
		}

		if (consume && item.ShouldRemoveAfterUse())
		{
			inventory.Remove(item);
		}
		return effects;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		if (Definition?.Targeting == SkillTargeting.Missile)
			yield return MissileTargeting.Animate(character, missileHit.Cell, Definition.MissileProjectilePrefab);
		//TODO get sound from item
		AudioManager.Instance.SoundEffects.UseItem.PlayAsSound();
		Game.Instance.DoFloatingText(item.ItemName, Color.white, character.transform.position);
		yield return new WaitForSecondsRealtime(1f);
	}

	internal override bool IsValid(Character character)
	{
		if (!CanBegin(character)) return false;
		var definition = Definition;
		if (definition == null) return true;
		return definition.Targeting switch
		{
			SkillTargeting.InventoryItem => definition.GetInventoryTargets(character).Contains(inventoryTarget),
			SkillTargeting.Missile => MissileTargeting.IsDirection(direction),
			_ => definition.TargetingRules.GetAffected(character, target).Any()
		};
	}

	internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
	{
		foreach (var cell in base.AnimationCells(actor)) yield return cell;
		if (Definition?.Targeting == SkillTargeting.Missile)
			foreach (var cell in AnimationPath(actor, missileHit.Cell)) yield return cell;
	}
}
