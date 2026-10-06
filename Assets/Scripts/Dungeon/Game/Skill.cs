using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Game/SkilllData")]
public class Skill : ScriptableObject
{
	public string SkillName;
	public int LearnCost;
	// Authoring default; the class entry's MaxRank is authoritative for learning.
	[Min(1)] public int MaxRank = 5;
	public SkillRankScaling RankScaling = new();
	// Runtime rank of this per-character instance (set when the ally is built). Not saved in the asset.
	[System.NonSerialized] public int Rank = 1;
	internal SkillRankContext RankContext => new(Rank, RankScaling);
	public ActivationType ActivationType;

	public int SPCost;
	[Min(0)] public int CastTime;
	// Zero denotes unlimited penetration. Existing missiles stop at one encounter.
	[Min(0)] public int MissileTargets = 1;
	internal int InitialCastTime(Character caster) => Mathf.Max(0, CastTime -
		(caster.Skills != null && caster.Skills.Any(s => s != null && s.ActivationType == ActivationType.Passive && s.SkillName == "Quick Casting") ? 1 : 0));
	public TargetSelector TargetSelector;
	public SkillTargeting Targeting;
	public InventoryTargetSelector InventoryTargetSelector = new();
	[Min(0)] public int AreaRadius;
	[Min(1)] public int MissileRange = 8;
	public GameObject MissileProjectilePrefab;
	[Min(0)] public int ArrowCost;
	public ArrowCostMode ArrowCostMode;
	// Weapon skills are blocked by Arm bind (arrow skills are always treated as weapon skills).
	public bool IsWeaponSkill;
	public bool UsesArrows => ArrowCost > 0 || ActionEffects?.OfType<RandomHitsAction>().Any(e => e.ArrowPerHit) == true;
	internal ActionTargeting TargetingRules => new(Targeting, TargetSelector, InventoryTargetSelector, AreaRadius, MissileRange);
	internal bool RequiresTargetSelection => Targeting == SkillTargeting.Missile || Targeting == SkillTargeting.LockedDoor || TargetingRules.RequiresSelection;
	[SerializeReference]
	public List<GameAction> ActionEffects;

	public SkillAnimation SkillAnimation;
	public CombatEffectProfile VisualProfile;
	[Tooltip("Played once when a successful active skill begins, through the effects mixer.")]
	public AudioClip CastSound;
	public Sprite Icon;

	private void OnEnable()
	{
		if (string.IsNullOrEmpty(SkillName)) SkillName = name;
		if (ActionEffects == null) ActionEffects = new();
		if (RankScaling == null) RankScaling = new();
		if (PassiveResponses == null) PassiveResponses = new();
	}

	internal List<GameAction> GetEffects(Character caster, Character target)
	{
		return
			ActionEffects.Select(x => { var bound = x.AsTargetedSkill(caster, target, RankContext); bound.Visuals.BoundTarget = target; return bound; })
			.ToList();
	}

	internal IEnumerator ExecuteRoutine(Character caster, Character target)
	{
		if (SkillAnimation != null) yield return SkillAnimation.ExecuteRoutine(caster, target);
	}

	internal bool IsValid(Character caster)
	{
		return caster != null && caster.CanCast(this, out _);
	}

	internal List<InventoryItem> GetInventoryTargets(Character caster)
	{
		if (Targeting != SkillTargeting.InventoryItem || InventoryTargetSelector == null ||
			ActionEffects == null || ActionEffects.Count == 0 || ActionEffects.Any(effect => effect is not InventorySkillEffect)) return new();
		return InventoryTargetSelector.GetTargets(caster)
			.Where(item => ActionEffects.Cast<InventorySkillEffect>().All(effect => effect.CanTarget(caster, item))).ToList();
	}

	internal List<GameAction> GetInventoryEffects(Character caster, InventoryItem item) =>
		ActionEffects.Cast<InventorySkillEffect>().Select(effect => { var bound = effect.Bind(caster, item); bound.Visuals.BoundTarget = caster; return bound; }).ToList();

	internal List<Vector3Int> GetTargetTiles(Character caster) => Game.Instance.CurrentDungeon.GetVisibleTiles(caster, caster.TilemapPosition)
        .Where(cell => Game.Instance.CurrentDungeon.IsFloorCell(cell) && TileWorldDungeon.ChevDistance(cell, caster.TilemapPosition) <= MissileRange).ToList();

    internal List<Vector3Int> GetTargets(Character caster) => GetTargetCharacters(caster).Select(c => c.TilemapPosition).Distinct().ToList();

	internal List<Character> GetTargetCharacters(Character caster) =>
        TargetingRules.GetCharacters(caster).Where(c => !ActionEffects.Any(e => e is ReduceCastAction) || c.PendingCast?.Remaining > 0).ToList();

	internal List<Character> GetAffectedCharacters(Character caster, Character selectedTarget)
	{
		return TargetingRules.GetAffected(caster, selectedTarget).Where(c => !ActionEffects.Any(e => e is ReduceCastAction) || c.PendingCast?.Remaining > 0).ToList();
	}

	public StatModification PassiveStatModification;
	[SerializeReference]
	public List<PassiveResponse> PassiveResponses = new();

	// Passive bonus scaled by rank: ints +1 step per rank, crit/evasion/hit ×(1 + 0.25 per extra rank), DropRate unscaled.
	internal StatModification GetScaledPassiveModification() =>
		StatScaling.Scale(PassiveStatModification, RankScaling ?? new SkillRankScaling(), Rank < 1 ? 1 : Rank);

	[TextArea]
	public string Description;
}

public enum ActivationType
{
	Active,
	Passive
}

public enum SkillTargeting
{
	SelectedTarget, // Radius zero affects only the selected character.
	Self,           // Cast immediately, centered on the caster.
	AllTargets,     // Cast immediately on every character allowed by the selector.
	InventoryItem, // Select one eligible item from the party inventory.
	Missile,       // Aim in one of eight directions; the first character or wall stops the shot.
	Tile,
	LockedDoor     // Pick a visible closed door within MissileRange tiles; only door effects (UnlockDoorAction) apply.
}

public enum ArrowCostMode
{
	Fixed,     // needs and uses exactly ArrowCost arrows
	PerTarget  // needs at least 1; uses one arrow per affected recipient, skipping recipients once arrows run out
}
