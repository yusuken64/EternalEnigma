using UnityEngine;

using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "UsableItemDefinition", menuName = "Game/Item/UsableItemDefinition")]
public class UsableItemDefinition : ItemDefinition
{
	public SkillTargeting Targeting = SkillTargeting.Self;
	public TargetSelector TargetSelector = new() { Team = TargetTeam.Self, Area = TargetArea.Self };
	public InventoryTargetSelector InventoryTargetSelector = new();
	[Min(0)] public int AreaRadius;
	[Min(1)] public int MissileRange = 8;
	public GameObject MissileProjectilePrefab;
	[Tooltip("InventorySkillEffect templates used when targeting an inventory item.")]
	[SerializeReference] public List<GameAction> InventoryEffects = new();
	internal ActionTargeting TargetingRules => new(Targeting, TargetSelector, InventoryTargetSelector, AreaRadius, MissileRange);
	internal List<InventoryItem> GetInventoryTargets(Character caster) =>
		Targeting == SkillTargeting.InventoryItem && InventoryTargetSelector != null && InventoryEffects != null &&
		InventoryEffects.Count > 0 && InventoryEffects.All(e => e is InventorySkillEffect) ?
		InventoryTargetSelector.GetTargets(caster).Where(item => InventoryEffects.Cast<InventorySkillEffect>()
			.All(effect => effect.CanTarget(caster, item))).ToList() : new();

	internal override InventoryItem AsInventoryItem(int? stock)
	{
		return new UsableInventoryItem(this, stock);
	}
}
