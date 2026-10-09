
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Ally : Character
{
	public string TownAllyId;
	public Sprite Portrait;
	public ClassDefinition PrimaryClass;
	public ClassDefinition SecondaryClass;
	public AttributePoints Attributes;
	public bool AttributePromptPending;
	public bool IsSummon;
	public int PendingAttributePoints => IsSummon ? 0 : HeroAttributes.Pending(Vitals?.Level ?? 1, Attributes);
	public HeroAnimator HeroAnimator;
	internal Interactable currentInteractable;
	public AllyStrategy AllyStrategy;
	// True while this ally is in Game.DownedAllies (0 HP, not destroyed). Set by PartyRules.
	public bool IsDowned { get; internal set; }
	// Set by ForgetSkillsAction; DungeonReturnService then clears the hero's saved skills.
	internal bool SkillsForgotten;
    private AllyAttackPolicy AllyAttackPolicy;
	private AllyAwareness awareness;
	internal AllyAwareness Awareness => awareness ??= new AllyAwareness(this);
	internal AllySkillPolicy SkillPolicy;
	public override bool IsWaitingForPlayerInput { get; set; }

	public SpriteRenderer CirlcleRenderer;
	public Color AllyColor;
	public Color PlayerColor;

    private SpriteRenderer turnRing;
    private static Sprite turnRingSprite;

    internal bool IsTurnHighlighted => DisplayedVitals.HP > 0 && Game.Instance != null &&
        (Game.Instance.TurnManager.IsProcessingTurn ? Game.Instance.TurnManager.ActiveActor == this :
            Game.Instance.PlayerController.ControlledAlly == this);

    internal Color TurnHighlightColor
    {
        get
        {
            var turn = Game.Instance.TurnManager;
            float alpha = !turn.IsProcessingTurn || turn.AwaitingCommand ? 1f :
                .75f + .25f*Mathf.Sin(Time.unscaledTime*Mathf.PI*2f/1.6f);
            return new Color(1f,.84f,.30f,alpha);
        }
    }

    private void LateUpdate()
    {
        var game = Game.Instance;
        if (game == null || !game.IsReady || CirlcleRenderer == null) return;
        if (turnRing == null) { SelectionRing.Apply(CirlcleRenderer); AllyColor=new Color(.23f,.69f,.58f,.9f);PlayerColor=new Color(.12f,.87f,.76f,.95f);BuildTurnRing(); }
        bool controlled = game.PlayerController.ControlledAlly == this;
        bool downed = DisplayedVitals.HP <= 0;
        var color = controlled ? PlayerColor : AllyColor;
        CirlcleRenderer.color = downed ? new Color(.42f,.44f,.44f) :
            DisplayedVitals.ActionsPerTurnLeft > 0 ? color :
            new Color(color.r*.35f,color.g*.35f,color.b*.35f,color.a);
        turnRing.enabled = CirlcleRenderer.enabled && IsTurnHighlighted;
        turnRing.color = TurnHighlightColor;
    }

    private void BuildTurnRing()
    {
        if (turnRingSprite == null)
        {
            const int size = 128;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,false)
                { name = "World turn ring", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size*size];
            for (int y=0;y<size;y++)
                for (int x=0;x<size;x++)
                {
                    float radius = new Vector2(x+.5f-size*.5f,y+.5f-size*.5f).magnitude;
                    float alpha = Mathf.Clamp01(63-radius)*Mathf.Clamp01(radius-58);
                    pixels[y*size+x] = new Color(1,1,1,alpha);
                }
            texture.SetPixels(pixels); texture.Apply(false,true);
            turnRingSprite = Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            turnRingSprite.hideFlags = HideFlags.HideAndDontSave;
        }
        var ring = new GameObject("Turn ring");
        ring.layer = CirlcleRenderer.gameObject.layer;
        ring.transform.SetParent(CirlcleRenderer.transform,false);
        var bounds = CirlcleRenderer.sprite.bounds;
        ring.transform.localPosition = bounds.center;
        ring.transform.localScale = new Vector3(bounds.size.x*1.15f,bounds.size.y*1.15f,1);
        turnRing = ring.AddComponent<SpriteRenderer>();
        turnRing.sprite = turnRingSprite;
        turnRing.sharedMaterial = CirlcleRenderer.sharedMaterial;
        turnRing.sortingLayerID = CirlcleRenderer.sortingLayerID;
        turnRing.sortingOrder = CirlcleRenderer.sortingOrder;
        turnRing.maskInteraction = CirlcleRenderer.maskInteraction;
    }

	private void Start()
	{
		EnsurePolicies();
	}

    // Evaluate forced statuses only once per action, including random confusion/paralysis.
    internal bool PrepareManualAction(bool evaluateStatuses = true)
    {
        Awareness.Refresh(Game.Instance);
        global::PendingCast.Validate(this);
        if (PendingCast?.Remaining == 0 && PendingCast.Skill?.UsesArrows == true && (!ArrowSupply.HasBow(this) || ArrowSupply.Count(this) < ArrowSupply.RequiredToCast(PendingCast.Skill)))
        { global::PendingCast.Cancel(this, "no arrows"); _forcedAction = null; if (this == Game.Instance.PlayerController.ControlledAlly) GameMessages.Post("no arrows", true); return false; }
        if (IsDowned || Vitals.HP <= 0) { determinedActions = new(); return true; }
        var overrides = evaluateStatuses ? StatusEffects.Where(s => s != null && !s.IsExpired())
            .Select(s => s.GetActionOverride(this)).Where(a => a != null).ToList() : new List<GameAction>();
        if (overrides.Count > 0) { global::PendingCast.Cancel(this, "action disabled"); _forcedAction = null; determinedActions = overrides; return true; }
        if (PendingCast != null && (!global::PendingCast.Mobile(this) || this != Game.Instance.PlayerController.ControlledAlly || AutoplayRunner.BlocksPlayerInput))
        { determinedActions = new() { new AdvanceCastAction() }; _forcedAction = null; return true; }
        if (_forcedAction == null) return false;
        determinedActions = new() { _forcedAction }; _forcedAction = null; return true;
    }

	private void EnsurePolicies()
	{
		SkillPolicy ??= new AllySkillPolicy(Game.Instance, this, 0);
		AllyAttackPolicy ??= new AllyAttackPolicy(Game.Instance, this, 1);
	}

	public override void DetermineAction()
	{
		if (PrepareManualAction()) return;
		determinedActions = ChooseAutonomousActions();
	}

	// Autoplay companions use the same decisions. The turn manager still owns forced actions and casts.
	internal List<GameAction> ChooseAutonomousActions(bool includeControlled = false)
	{
		EnsurePolicies();
		Awareness.Refresh(Game.Instance);
		SkillPolicy.IncludeControlledAlly = includeControlled;
		if (SkillPolicy.ShouldRun()) return SkillPolicy.GetActions();
		if (!Awareness.SearchFirst && AllyAttackPolicy.ShouldRun()) return AllyAttackPolicy.GetActions();
		return new List<GameAction> { AllyNavigation.Travel(Game.Instance, this) };
	}

	public override List<GameAction> GetDeterminedAction()
	{
		this.Vitals.ActionsPerTurnLeft--;
		return determinedActions;
	}

	public override List<GameAction> ExecuteActionImmediate(GameAction action)
	{
		if (GetActionInterupt(action))
		{
			return new();
		}

		var sideEffects = ExecuteWithScenery(action);
		var actionResponses = GetActionResponses(action);
		var actionResponseEffects = actionResponses.SelectMany(x => x.ExecuteImmediate(this));
		sideEffects.AddRange(actionResponseEffects);

		return sideEffects;
	}

	public override IEnumerator ExecuteActionRoutine(GameAction action)
	{
		if (this == null) { yield break; }

		yield return action.Visuals.Play(action, this, !action.ShouldAnimate(this));
		if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations && Vitals.HP > 0) PlayIdleAnimation();
		action.UpdateDisplayedStats();
	}

	public override IEnumerable<GameAction> GetResponseTo(GameAction action)
	{
		if (this == null ||
			this.Vitals.HP <= 0)
		{
			return new List<GameAction>();
		}
		if (action is MovementAction) Awareness.Refresh(Game.Instance);
		return GetActionResponses(action);
	}

	public override List<GameAction> GetTrapSideEffects()
	{
		if (MovedThisTurn && currentInteractable is Trap trap && trap is not CaltropTrap && trap is not FantasyTrap)
		{
			currentInteractable = null;
			Game.Instance.DoFloatingText(trap.GetInteractionText(), Color.yellow, this);
			return trap.GetTrapSideEffects(this);
		}

		if (currentInteractable is Trap) currentInteractable = null;

		return new();
	}

	public override List<GameAction> GetInteractableSideEffects()
	{
        if (currentInteractable is not Trap and not DungeonProp and not null)
        {
			if (currentInteractable is not Stairs)
			{
				//Game.Instance.DoFloatingText(currentInteractable.GetInteractionText(), Color.yellow, this.VisualParent.transform.position);
				var effects = currentInteractable.GetInteractionSideEffects(this);
				currentInteractable = null;
				return effects;
			}
        }

        return new();
    }

	public override void StartTurn()
	{
		MovedThisTurn = false;
		determinedActions.Clear();
		Vitals.ActionsPerTurnLeft = FinalStats.ActionsPerTurnMax;
		Vitals.AttacksPerTurnLeft = FinalStats.AttacksPerTurnMax;
		ClassPassives.OnTurnStart(this);

		InvalidateCachedStats();
		SyncDisplayedStats();
		TrapSense.RevealAround(this);
		_forcedAction = null;
	}

	internal override void PlayWalkAnimation()
	{
		HeroAnimator?.PlayWalkAnimation();
	}

	internal override void PlayIdleAnimation()
	{
		HeroAnimator?.PlayIdleAnimation();
	}

	internal override void PlayAttackAnimation()
	{
		HeroAnimator?.PlayAttackAnimation();
	}

	internal override void PlayTakeDamageAnimation()
	{
		HeroAnimator?.PlayTakeDamageAnimation();
	}

	internal override void PlayDeathAnimation()
	{
		HeroAnimator?.PlayDeathAnimation();
	}

	protected override StatModification GetStartingStatBonus() =>
		PrimaryClass != null ? PrimaryClass.StartingStatBonus : null;

	internal void InitialzeModel(TownAlly townAlly)
	{
		Portrait = townAlly.Portrait;
		var heroAnimator = townAlly.GetComponent<HeroAnimator>();
		var newHeroAnimator = this.gameObject.AddComponent<HeroAnimator>();
		heroAnimator.CopyFieldsTo(newHeroAnimator);
		this.HeroAnimator = newHeroAnimator;
		this.HeroAnimator.Animator.applyRootMotion = false;

		ReplaceChildGameObject(this.gameObject, "GameObject/RPGHeroHP", townAlly.AnimatedModel);
        SilhouetteParticipant.Register(transform,SilhouetteRole.Caster,true);
	}

	private void ReplaceChildGameObject(GameObject gameObject, string childPath, GameObject animatedModel)
	{
		Transform targetTransform = gameObject.transform.Find(childPath);
		ReplaceModel(targetTransform.gameObject, animatedModel);

		Destroy(targetTransform.gameObject);
	}

	public void ReplaceModel(GameObject oldChild, GameObject newChild)
	{
		// Replace the old child with the new GameObject
		newChild.transform.parent = oldChild.transform.parent;
		newChild.transform.localPosition = oldChild.transform.localPosition;
		newChild.transform.localRotation = oldChild.transform.localRotation;
		newChild.transform.localScale = oldChild.transform.localScale;
		newChild.transform.SetAsFirstSibling();
	}

    public override void Inventory_HandleInventoryChanged()
	{
		base.Inventory_HandleInventoryChanged();

		HeroAnimator.SetWeapon(
			Equipment.EquippedWeapon?.ItemDefinition as EquipmentItemDefinition,
			Equipment.EquippedShield?.ItemDefinition as EquipmentItemDefinition);
	}

	public override void Equipment_HandleEquipmentChanged(EquipChangeType equipChangeType, EquipableInventoryItem item)
	{
		base.Equipment_HandleEquipmentChanged(equipChangeType, item);

		switch (equipChangeType)
		{
			case EquipChangeType.Equip:
				Game.Instance.PlayerController.Inventory.InventoryItems.Remove(item);
				break;
			case EquipChangeType.UnEquip:
				if (!ArrowSupply.IsArrow(item) || !item.StackIsEmpty()) Game.Instance.PlayerController.Inventory.InventoryItems.Add(item);
				break;
		}

		HeroAnimator?.SetWeapon(
			Equipment.EquippedWeapon?.ItemDefinition as EquipmentItemDefinition,
			Equipment.EquippedShield?.ItemDefinition as EquipmentItemDefinition);
        SilhouetteParticipant.Register(transform,SilhouetteRole.Caster,true);
	}

	internal bool IsRangedAttack(out GameObject projectilePrefab)
	{
		return Equipment.IsRangedAttack(out projectilePrefab);
	}

	internal void SetToCPU()
	{
		CirlcleRenderer.color = AllyColor;
	}

	internal void SetToPlayer()
	{
		CirlcleRenderer.color = PlayerColor;
	}
}

internal class AllyAttackPolicy : PolicyBase
{
    private readonly Ally ally;
    private AllyCombat.Attack attack;
    public AllyAttackPolicy(Game game, Character character, int priority) : base(game, character, priority)
    {
        ally = character as Ally;
    }
    public override bool ShouldRun()
    {
        attack = null;
        ally.Awareness.Refresh(game);
        if (ally.Awareness.SearchFirst) return false;
        attack = AllyCombat.ChooseAttack(game, ally);
        return attack != null;
    }
    public override List<GameAction> GetActions()
    {
        // A target may have moved, died or changed teams since evaluation.
        if (!ShouldRun()) return new List<GameAction> { new WaitAction() };
        ally.PursuitTarget = attack.Target;
        ally.PursuitPosition = attack.Target.TilemapPosition;
        return new List<GameAction> { attack.ToAction(ally) };
    }
}

public enum AllyStrategy
{
    Follow = 0,
    Aggresive = 1,
    HoldPosition = 2
}
