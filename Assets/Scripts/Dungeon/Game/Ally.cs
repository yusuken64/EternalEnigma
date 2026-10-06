
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
	public HeroAnimator HeroAnimator;
	internal Interactable currentInteractable;
	public AllyStrategy AllyStrategy;
	// True while this ally is in Game.DownedAllies (0 HP, not destroyed). Set by PartyRules.
	public bool IsDowned { get; internal set; }
	// Set by ForgetSkillsAction; DungeonReturnService then clears the hero's saved skills.
	internal bool SkillsForgotten;
    private AllyAttackPolicy AllyAttackPolicy;
	private AllyRangedPositioningPolicy AllyRangedPositioningPolicy;
	private AllyPursuitPolicy PursuitPolicy;
	private WanderPolicy WanderPolicy;
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
		SkillPolicy = new AllySkillPolicy(Game.Instance, this, 0);
		AllyAttackPolicy = new AllyAttackPolicy(Game.Instance, this, 1);
		AllyRangedPositioningPolicy = new AllyRangedPositioningPolicy(Game.Instance, this, 2);
		PursuitPolicy = new AllyPursuitPolicy(Game.Instance, this, 3);
		WanderPolicy = new WanderPolicy(Game.Instance, this, 4);
	}

    // Evaluate forced statuses only once per action, including random confusion/paralysis.
    internal bool PrepareManualAction(bool evaluateStatuses = true)
    {
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

	public override void DetermineAction()
	{
        if (PrepareManualAction()) return;

		if (SkillPolicy != null && SkillPolicy.ShouldRun())
		{
			determinedActions = SkillPolicy.GetActions();
			return;
		}

		if (AllyAttackPolicy.ShouldRun())
		{
			determinedActions = AllyAttackPolicy.GetActions();
			return;
		}

		PursuitTarget = GetTarget();
		if (PursuitTarget != null)
		{
			PursuitPosition = PursuitTarget.TilemapPosition;
		}
		if (AllyRangedPositioningPolicy.ShouldRun())
		{
			determinedActions = AllyRangedPositioningPolicy.GetActions();
			return;
		}
		if (PursuitPolicy.ShouldRun())
		{
			determinedActions = PursuitPolicy.GetActions();
			return;
		}
		if (WanderPolicy.ShouldRun())
		{
			determinedActions = WanderPolicy.GetActions();
			return;
		}

		determinedActions = new List<GameAction>()
		{
			new WaitAction()
		};
	}

	private Character GetTarget()
	{
		var game = Game.Instance;

		List<Character> pursuitTargets = new List<Character>();

		if (AllyStrategy == AllyStrategy.Aggresive)
		{
			pursuitTargets.AddRange(game.Enemies.Where(x => x != null && !EnemyBehavior.IsDisguised(x) && x.Team != Team));

			var aggressiveTarget = pursuitTargets
				.Where(x => game.CurrentDungeon.CanSee(this, x))
				.OrderBy(x => TileWorldDungeon.ChevDistance(x.TilemapPosition, TilemapPosition))
				.ThenBy(x => x.TilemapPosition == PursuitPosition)
				.FirstOrDefault();

			if (aggressiveTarget != null) { return aggressiveTarget; }
		}
		
		if (AllyStrategy != AllyStrategy.HoldPosition)
		{
			if (TileWorldDungeon.ChevDistance(game.PlayerController.PartyLeader.TilemapPosition,
				TilemapPosition) < 5)
			{
				return game.PlayerController.PartyLeader;
			}
		}

		return null;
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
		if (action is MovementAction movementAction)
		{
			var target = GetTarget();
			if (target != null)
			{
				PursuitPosition = target.TilemapPosition;
			}
		}
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
	private readonly Ally _ally;
	private Character target;
    private bool _isRangedAttack;
    private GameObject _projectilePrefab;
    private Facing _rangedAttackFacing;

    public AllyAttackPolicy(Game game, Character character, int priority) : base(game, character, priority)
	{
		_ally = character as Ally;
	}

	public override List<GameAction> GetActions()
	{
		if (_isRangedAttack)
		{
			character.SetFacing(_rangedAttackFacing);
			return new List<GameAction>()
			{
				new RangedAttackAction(_ally, null, _ally.FinalStats.Strength, _projectilePrefab)
			};
		}
		else
		{
			character.SetFacingByTargetPosition(target.TilemapPosition);
			return new List<GameAction>() { new AttackAction(_ally, _ally.TilemapPosition, target.TilemapPosition) };
		}
	}

	public override bool ShouldRun()
	{
		_isRangedAttack = _ally.IsRangedAttack(out _projectilePrefab);
        if (_isRangedAttack && ArrowSupply.HasBow(_ally) && ArrowSupply.Count(_ally) < 1) return false;
		if (_isRangedAttack)
		{
			var visibleTiles = game.CurrentDungeon.GetVisibleTiles(_ally, _ally.TilemapPosition);
			var facings = Enum.GetValues(typeof(Facing)).Cast<Facing>().ToArray();
			Shuffle(facings);
			foreach (Facing direction in facings)
			{
				var line = MissileTargeting.TraceLine(_ally, Dungeon.GetFacingOffset(direction), 10 + ClassPassives.MissileRangeBonus(_ally), ArrowSupply.Penetration(_ally));
                Vector3Int pos = line.Encounters.FirstOrDefault().Cell;

				if (!visibleTiles.Contains(pos)) { continue; }

				// Find enemy at pos (if any)
				target = line.Encounters.Select(h => h.Character).FirstOrDefault(x => !EnemyBehavior.IsDisguised(x));

				if (target != null)
				{
					_rangedAttackFacing = direction;
					return true;
				}
			}
		}
		else
		{
			var attackBounds = _ally.GetAttackBounds();
			target = Game.Instance.AllCharacters
				.Where(x => x != null)
				.Where(x => x.Team != _ally.Team && !EnemyBehavior.IsDisguised(x))
				.Where(x => attackBounds.Overlaps2D(x.ToBounds()))
                .Where(x => game.CurrentDungeon.CanSee(_ally, x))
				.FirstOrDefault();
		}

		return target != null;
	}

	private void Shuffle<T>(T[] array)
	{
		for (int i = array.Length - 1; i > 0; i--)
		{
			int j = UnityEngine.Random.Range(0, i + 1);
			T temp = array[i];
			array[i] = array[j];
			array[j] = temp;
		}
	}
}

public class AllyPursuitPolicy : PolicyBase
{
	private readonly Ally ally;
	private List<AStar.Node> path;

	public AllyPursuitPolicy(Game game, Ally ally, int priority) : base(game, ally as Character, priority)
	{
		this.ally = ally;
	}

	public override List<GameAction> GetActions()
	{
		var newMapPosition = new Vector3Int(path[0].X, path[0].Y);
		character.SetFacingByTargetPosition(newMapPosition);
		return new List<GameAction>() { new MovementAction(character, character.TilemapPosition, newMapPosition) };
	}

	public override bool ShouldRun()
	{
		if (ally.AllyStrategy == AllyStrategy.HoldPosition)
		{
			return false;
		}

		if (character.PursuitPosition == null) { return false; }

		path = character.CalculatePursuitPath();

		if (path != null &&
			path.Count > 0)
		{
			return true;
		}

		return false;
	}
}

public class AllyRangedPositioningPolicy : PolicyBase
{
	private readonly Ally ally;
	private Vector3Int desiredPosition;
	private List<AStar.Node> path;

	private const int RangedAttackDistance = 3;

	public AllyRangedPositioningPolicy(Game game, Ally ally, int priority) : base(game, ally, priority)
	{
		this.ally = ally;
	}

	public override bool ShouldRun()
	{
		if (ally.AllyStrategy == AllyStrategy.HoldPosition)
		{
			return false;
		}

		var isRangedAttack = ally.IsRangedAttack(out _);
		if (!isRangedAttack)
        {
			return false;
        }

		// Find shortest path among candidates from current position
		List<AStar.Node> bestPath = null;
		Vector3Int bestPos = default;
		int bestPathLength = int.MaxValue;

		foreach (var target in game.Enemies)
		{
            if (target == null || EnemyBehavior.IsDisguised(target) || !game.CurrentDungeon.CanSee(ally, target)) { continue; }

			// If already exactly 3 tiles away, no need to reposition
			if (TileWorldDungeon.ManhattanDistance(ally.TilemapPosition, target.TilemapPosition) == RangedAttackDistance)
			{
				return false; // Positioning not needed, attack policy can take over
			}

			// Find candidate positions 3 tiles away from target that are walkable and reachable
			var candidates = GetPositionsAtDistance(target.TilemapPosition, RangedAttackDistance)
				.Where(pos =>
					Game.Instance.CurrentDungeon.IsWalkable(pos) && // Your method to check walkability
					!Game.Instance.AllCharacters.Any(c => c.TilemapPosition == pos) // Position not occupied
				)
				.ToList();

			if (candidates.Count == 0) continue;

			var grid = Character.GetAStarGrid();
			foreach (var candidate in candidates)
			{
				var candidatePath = ally.CalculateRangedAttackPath(candidate, grid);
				if (candidatePath != null && candidatePath.Count < bestPathLength)
				{
					bestPathLength = candidatePath.Count;
					bestPath = candidatePath;
					bestPos = candidate;
				}
			}
		}

		if (bestPath == null) return false; // No reachable candidate positions

		path = bestPath;
		desiredPosition = bestPos;

		return true;
	}

	public override List<GameAction> GetActions()
	{
		if (path == null || path.Count == 0) return new List<GameAction>();

		var nextStep = new Vector3Int(path[0].X, path[0].Y);

		// Face towards target
		ally.SetFacingByTargetPosition(desiredPosition);

		// Move towards next step in path
		return new List<GameAction> { new MovementAction(ally, ally.TilemapPosition, nextStep) };
	}

	private IEnumerable<Vector3Int> GetPositionsAtDistance(Vector3Int origin, int distance)
	{
		for (int dx = -distance; dx <= distance; dx += distance)
		{
			for (int dy = -distance; dy <= distance; dy += distance)
			{
				if (dx == 0 && dy == 0)
					continue;

				yield return new Vector3Int(origin.x + dx, origin.y + dy, origin.z);
			}
		}
	}
}

public enum AllyStrategy
{
	Follow,
	Aggresive,
	HoldPosition
}
