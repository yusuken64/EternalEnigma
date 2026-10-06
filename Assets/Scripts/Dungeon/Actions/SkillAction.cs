using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class SkillAction : GameAction
{
	private Character caster;
	private Skill skill;
	private Character target;
    private DungeonProp sceneryTarget;
	private InventoryItem inventoryTarget;
	private Vector3Int direction;
	private MissileTargeting.Hit missileHit;
	private List<Character> affected = new();
	private bool castSucceeded;
    private bool releasing;
    private bool charging;
    private bool resolved;
    private List<Character> captured;
    private UnityEngine.Object floor;
    private Team targetTeam;
    private Vector3Int? tile;
    internal static SkillAction ForTile(Character caster, Skill skill, Vector3Int cell) => new(caster, skill, null) { tile = cell };
    internal override bool ValidateCommand(Character character, out string reason)
    {
        if (skill != null && skill.UsesArrows && (!ArrowSupply.HasBow(character) || ArrowSupply.Count(character) < ArrowSupply.RequiredToCast(skill)))
        { reason = "no arrows"; return false; }
        reason = null;
        return IsValid(character);
    }
    private bool Eligible(Character recipient) => skill.TargetSelector.Eligible(caster, recipient) &&
        Game.Instance.AllCharacters.Concat(Game.Instance.DownedAllies).Contains(recipient);
    private List<Character> Recipients()
    {
        if (captured != null) return captured.Where(Eligible).ToList();
        if (skill.Targeting == SkillTargeting.Tile)
            return Game.Instance.AllCharacters.Where(c => Eligible(c) && TileWorldDungeon.ChevDistance(c.TilemapPosition, tile.Value) <= skill.AreaRadius).ToList();
        if (skill.Targeting == SkillTargeting.SelectedTarget && target != null)
        {
            if (!Eligible(target)) return new();
            if (skill.AreaRadius == 0) return new() { target };
            return Game.Instance.AllCharacters.Where(c => Eligible(c) && TileWorldDungeon.ChevDistance(c.TilemapPosition, target.TilemapPosition) <= skill.AreaRadius).ToList();
        }
        return skill.GetAffectedCharacters(caster, target);
    }

	internal Skill Skill => skill;

	public SkillAction()
	{

	}
	public SkillAction(Character caster, Skill skill, Character target)
	{
		this.caster = caster;
		this.skill = skill;
		this.target = target ?? (skill != null && !skill.RequiresTargetSelection ? caster : null);
	}

    internal static SkillAction ForScenery(Character caster, Skill skill, DungeonProp prop) => new SkillAction(caster,skill,null) { sceneryTarget = prop };
	internal static SkillAction ForInventoryItem(Character caster, Skill skill, InventoryItem item) =>
		new SkillAction(caster, skill, null) { inventoryTarget = item };
	internal static SkillAction ForMissile(Character caster, Skill skill, Vector3Int direction) =>
		new SkillAction(caster, skill, null) { direction = direction };

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		if (!releasing)
        {
            if (!IsValid(character)) return new();
            castSucceeded = true;
            AddMetricsModification(caster, (stats, vitals) => vitals.SP -= skill.SPCost);
            int charge = skill.InitialCastTime(caster);
            if (charge > 0)
            {
                charging = true;
                var release = new SkillAction(caster, skill, target) { releasing = true, direction = direction, tile = tile,
                    sceneryTarget = sceneryTarget, inventoryTarget = inventoryTarget, floor = Game.Instance.CurrentDungeon,
                    targetTeam = target != null ? target.Team : caster.Team,
                    captured = skill.SkillName == "Mass Revive" ? skill.GetTargetCharacters(caster).ToList() : null };
                caster.PendingCast = new PendingCast(release, skill.SkillName, charge, skill);
                GameMessages.ForCharacter(caster, $"[Cast] {GameMessages.Name(caster)} starts {skill.SkillName} ({charge} charging actions).");
                return new();
            }
            GameMessages.AbilityCast(caster, skill);
        }
        else
        {
            if (floor != Game.Instance.CurrentDungeon ||
                skill.Targeting == SkillTargeting.SelectedTarget && sceneryTarget == null && (!Eligible(target) || target.Team != targetTeam) ||
                captured != null && !captured.Any(Eligible) || sceneryTarget != null && !sceneryTarget.Alive ||
                skill.Targeting == SkillTargeting.InventoryItem && !skill.GetInventoryTargets(caster).Contains(inventoryTarget))
            {
                GameMessages.ForCharacter(caster, $"[Cast] {GameMessages.Name(caster)}'s {skill.SkillName} fizzled: invalid target.");
                return new();
            }
            GameMessages.ForCharacter(caster, $"[Cast] {GameMessages.Name(caster)} releases {skill.SkillName}.");
        }
        resolved = true;
        float ammunitionMultiplier = skill.UsesArrows ? ArrowSupply.DamageMultiplier(caster) : 1f;
		bool inventoryTargeting = skill.Targeting == SkillTargeting.InventoryItem;
		affected = inventoryTargeting ? new List<Character> { caster } : Recipients();
		if (skill.Targeting == SkillTargeting.Missile)
		{
			var trace = MissileTargeting.TraceLine(caster, direction, skill.MissileRange + (skill.UsesArrows ? ClassPassives.MissileRangeBonus(caster) : 0), skill.AreaRadius > 0 ? 1 : ArrowSupply.Penetration(caster, skill), skill.AreaRadius > 0);
            missileHit = new MissileTargeting.Hit(trace.Endpoint, trace.Encounters.LastOrDefault().Character);
            affected = skill.AreaRadius > 0 ? skill.TargetingRules.GetMissileAffected(caster, missileHit) : trace.Encounters.Select(h => h.Character).Where(c => skill.TargetSelector.Eligible(caster, c)).ToList();
		}
        var scenery = skill.Targeting == SkillTargeting.Tile ? Game.Instance.CurrentDungeon.Interactables.OfType<DungeonProp>()
            .Where(p => p.Alive && ScenerySkillTargets.Damaging(skill) && TileWorldDungeon.ChevDistance(p.Position, tile.Value) <= skill.AreaRadius).ToList() :
            ScenerySkillTargets.Affected(caster, skill, target, sceneryTarget, missileHit.Cell);
        if (sceneryTarget != null && skill.AreaRadius > 0)
            affected = skill.GetTargetCharacters(caster).Where(c => TileWorldDungeon.ChevDistance(c.TilemapPosition,sceneryTarget.Position) <= skill.AreaRadius).ToList();
		if (skill.ArrowCost > 0 && skill.Targeting != SkillTargeting.InventoryItem)
		{
			if (skill.ArrowCostMode == ArrowCostMode.PerTarget)
			{
				int fired = ArrowSupply.Consume(caster, affected.Count + scenery.Count, ClassPassives.ArrowRecoveryChance(caster));
				scenery = scenery.Take(Mathf.Max(0, fired - affected.Count)).ToList();
                if (fired < affected.Count) affected = affected.Take(fired).ToList();
			}
			else
			{
				ArrowSupply.Consume(caster, skill.ArrowCost, ClassPassives.ArrowRecoveryChance(caster));
			}
		}

        var center = tile ?? (skill.Targeting == SkillTargeting.Missile ? missileHit.Cell :
            sceneryTarget != null ? sceneryTarget.Position : target != null ? target.TilemapPosition : caster.TilemapPosition);
        int visualRadius = skill.AreaRadius;
        if (skill.Targeting == SkillTargeting.AllTargets && affected.Count > 0)
            visualRadius = Mathf.Max(visualRadius, affected.Max(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, center)));
        foreach (var random in skill.ActionEffects.OfType<RandomHitsAction>()) visualRadius = Mathf.Max(visualRadius, random.Radius);
        Visuals.Configure(skill.VisualProfile, caster, center, visualRadius);
        if (Visuals.Sequence != null)
        {
            Visuals.Sequence.SingleFlight = skill.Targeting == SkillTargeting.Missile || skill.AreaRadius > 0;
            Visuals.Sequence.ContinuousFlight = skill.Targeting == SkillTargeting.Missile && skill.AreaRadius == 0;
        }

		var effects = inventoryTargeting ? skill.GetInventoryEffects(caster, inventoryTarget) :
            affected.SelectMany(recipient => skill.GetEffects(caster, recipient)).Concat(scenery.SelectMany(p => ScenerySkillTargets.Effects(caster,skill,p,ammunitionMultiplier))).ToList();
        effects.RemoveAll(effect => effect is StepBackAction);
        foreach (var step in skill.ActionEffects.OfType<StepBackAction>())
        {
            var bound = (StepBackAction)step.AsTargetedSkill(caster, target ?? affected.FirstOrDefault(), skill.RankContext);
            if (skill.Targeting == SkillTargeting.Missile) bound.ShotDirection = direction;
            effects.Add(bound);
        }
        foreach (var effect in effects.OfType<ScaledDamageAction>()) effect.AmmunitionMultiplier = ammunitionMultiplier;
        return effects;
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
	{
		if (skipAnimation || charging || !resolved) yield break;
		if (caster is Ally hero)
		{
			var action = skill.SkillName switch
			{
				"Double Strike" or "Whirlwind" => AnimatedAction.Combo,
				"Lunge" or "Shadow Step" => AnimatedAction.Dash,
				_ => AnimatedAction.Attack
			};
			if (action != AnimatedAction.Attack) hero.HeroAnimator?.PlayOneShot(action);
			if (action == AnimatedAction.Dash) AudioManager.Instance?.PlayDashSounds();
		}
        if (castSucceeded && Visuals.Sequence == null) yield return new WaitForSecondsRealtime(.12f);
		if (Visuals.Sequence != null) yield break;
		if (skill.Targeting == SkillTargeting.Missile)
			yield return MissileTargeting.Animate(caster, missileHit.Cell, skill.MissileProjectilePrefab);
		foreach (var recipient in affected)
			if (recipient != null) yield return skill.ExecuteRoutine(caster, recipient);
	}

	internal void PlayCastSound()
	{
		if (releasing) CombatEffectPlayer.Get()?.ClearCasting(caster);
        if (!castSucceeded) return;
        if (charging || Visuals.Sequence == null || skill.VisualProfile.GroundCircle.Prefab == null && skill.VisualProfile.Muzzle.Prefab == null) CombatEffectPlayer.Get()?.ShowCasting(caster, skill.VisualProfile, charging);
		var audio = AudioManager.Instance;
		audio?.PlaySoundEffect(skill.CastSound != null ? skill.CastSound : audio.SoundEffects?.AbilityCast);
	}

    internal override IEnumerable<Vector3Int> AnimationCells(Character actor)
    {
        foreach (var cell in base.AnimationCells(actor)) yield return cell;
        if (skill?.Targeting == SkillTargeting.Missile)
            foreach (var cell in AnimationPath(actor, missileHit.Cell)) yield return cell;
        foreach (var recipient in affected)
            if (recipient != null)
                foreach (var cell in AnimationPath(actor, recipient.TilemapPosition)) yield return cell;
    }

	internal override bool IsValid(Character character)
	{
		return character == caster && skill != null && skill.IsValid(caster) &&
			(skill.Targeting == SkillTargeting.Tile ? tile.HasValue && skill.GetTargetTiles(caster).Contains(tile.Value) : skill.Targeting == SkillTargeting.Missile ? MissileTargeting.IsDirection(direction) : skill.Targeting == SkillTargeting.InventoryItem ?
				skill.GetInventoryTargets(caster).Contains(inventoryTarget) :
				inventoryTarget == null && (sceneryTarget != null ? ScenerySkillTargets.Candidates(caster,skill).Contains(sceneryTarget) : skill.GetAffectedCharacters(caster, target).Any() || (skill.Targeting != SkillTargeting.SelectedTarget && ScenerySkillTargets.Affected(caster,skill,target,null,default).Any())));
	}
}
