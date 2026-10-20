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

	internal bool CanBegin(Character character) => CanBegin(character, out _);

	internal bool CanBegin(Character character, out string reason)
	{
		reason = null;
		if (character == null || character != user) { reason = "Select the hero who will use this item."; return false; }
		if (item?.ItemDefinition == null || inventory == null ||
			(!inventory.InventoryItems.Contains(item) && !character.Equipment.IsEquipped(item)))
		{ reason = "This item is no longer in your inventory."; return false; }
		if (item.HasStacks && item.StackIsEmpty()) { reason = "This item has no uses remaining."; return false; }
		if (character.Vitals.HP <= 0) { reason = "A downed hero cannot use items or change equipment."; return false; }
		var status = character.StatusEffects.FirstOrDefault(s => !s.IsExpired() && (s.PreventsMenu() || s.Interupts(this)));
		if (status != null) { reason = $"Cannot use this item while affected by {GameplayText.Name(status.GetEffectName())}."; return false; }
		if (item is EquipableInventoryItem equipment)
		{
			if (character.Equipment.IsEquipped(equipment) || character.Equipment.CanEquip(equipment)) return true;
			reason = character is Ally hero ? HeroClass.EquipmentRestriction(hero.PrimaryClass, hero.SecondaryClass, equipment) : null;
			reason ??= "This hero cannot equip that item.";
			return false;
		}
		var definition = Definition;
		if (definition == null)
		{
			if (item.ItemDefinition.ItemEffectDefinition != null) return true;
			reason = "This item cannot be used directly."; return false;
		}
		if (!definition.TargetingRules.IsConfigured) { reason = "This item has no usable target rules."; return false; }
		if (definition.Targeting == SkillTargeting.InventoryItem)
		{
			if (definition.GetInventoryTargets(character).Any()) return true;
			reason = "No valid inventory items can be targeted by this item."; return false;
		}
		if (definition.ItemEffectDefinition == null) { reason = "This item cannot be used directly."; return false; }
		if (definition.Targeting == SkillTargeting.Missile ||
			(definition.TargetingRules.RequiresSelection ? definition.TargetingRules.GetCharacters(character) :
			definition.TargetingRules.GetAffected(character, character)).Any()) return true;
		reason = "No valid characters can be targeted by this item."; return false;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!IsValid(character)) return new();
		if (item is EquipableInventoryItem equipment)
			return new() { character.Equipment.IsEquipped(equipment)
				? new UnEquipAction(character, equipment) : new EquipAction(character, equipment) };
        QueueSound(SoundCue.UseItem);
        GameMessages.ForCharacter(character, $"{GameMessages.Name(character)} used {item.DisplayName}.");
		bool usefulRecovery = OpeningDungeonLesson.UsefulRecovery(target ?? character, item);
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
        if (item.ItemDefinition.ItemEffectDefinition is SkillItemEffectDefinition skillEffect && skillEffect.EffectSkill != null)
        {
            QueueSound(SoundCue.AbilityStart);
            GameMessages.AbilityCast(character, skillEffect.EffectSkill, item.DisplayName);
            var center = definition?.Targeting == SkillTargeting.Missile ? missileHit.Cell : target != null ? target.TilemapPosition : character.TilemapPosition;
            Visuals.Configure(skillEffect.EffectSkill.VisualProfile, character, center, definition != null ? definition.AreaRadius : 0);
            if (Visuals.Sequence != null) Visuals.Sequence.SingleFlight = definition?.Targeting == SkillTargeting.Missile;
        }
		if (consume && item.HasStacks)
		{
			item.Decrement();
		}

		if (consume && item.ShouldRemoveAfterUse())
		{
			inventory.Remove(item);
		}
		OpeningDungeonLesson.RecoveryUsed(character, item, usefulRecovery && effects.Count > 0);
		return effects;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation) yield break;
		if (item?.ItemDefinition != null && item.ItemName.IndexOf("Potion", System.StringComparison.OrdinalIgnoreCase) >= 0 && character is Ally hero)
			hero.HeroAnimator?.PlayOneShot(AnimatedAction.DrinkPotion);
		if (Visuals.Sequence != null) yield break;
		if (Definition?.Targeting == SkillTargeting.Missile)
			yield return MissileTargeting.Animate(character, missileHit.Cell, Definition.MissileProjectilePrefab);
		//TODO get sound from item


		yield return new WaitForSecondsRealtime(1f);
	}

	internal override bool IsValid(Character character) => ValidateCommand(character, out _);

	internal override bool ValidateCommand(Character character, out string reason)
	{
		if (!CanBegin(character, out reason)) return false;
		var definition = Definition;
		if (definition == null) return true;
		bool valid = definition.Targeting switch
		{
			SkillTargeting.InventoryItem => definition.GetInventoryTargets(character).Contains(inventoryTarget),
			SkillTargeting.Missile => MissileTargeting.IsDirection(direction),
			_ => definition.TargetingRules.GetAffected(character, target).Any()
		};
		if (!valid) reason = definition.Targeting == SkillTargeting.InventoryItem ? "That inventory item is no longer a valid target." :
			definition.Targeting == SkillTargeting.Missile ? "Choose a direction to use this item." : "That character is no longer a valid target.";
		return valid;
	}

	internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
	{
		foreach (var cell in base.AnimationCells(actor)) yield return cell;
		if (Definition?.Targeting == SkillTargeting.Missile)
			foreach (var cell in AnimationPath(actor, missileHit.Cell)) yield return cell;
	}
}
