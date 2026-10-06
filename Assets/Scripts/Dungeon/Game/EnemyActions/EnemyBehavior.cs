using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Authored enemy abilities and runtime state; dormancy is not a status effect.</summary>
[DisallowMultipleComponent]
public sealed class EnemyBehavior : MonoBehaviour
{
    public bool WaitUntilAttacked;
    public bool RangedAttack;
    [Min(2)] public int Range = 6;
    public bool Steals;
    public bool AlwaysFlees;
    public bool RootsAdjacentTarget;
    public bool Stationary;
    public bool ExplodesOnDeath;
    [Min(1)] public int ExplosionDamage = 20;
    [Range(0, 1)] public float WarpWhenHitChance;
    [Min(1)] public int SpawnWeight = 20;
    [Min(1)] public int PackSize = 1;
    public bool StealItems = true;
    public bool StealGold = true;
    public ConfusionStatusEffect Confusion;
    public bool Mimic;
    public GameObject ChestVisualPrefab;
    private Enemy owner;
    private GameObject disguise;
    private readonly List<GameObject> hiddenChildren = new();
    private bool revealed;
    private int cooldown;
    private InventoryItem stolenItem;
    internal InventoryItem TrapCarriedItem { get => stolenItem; set => stolenItem = value; }
    private int stolenGold;
    public bool Disguised => Mimic && !revealed;
    public bool CarryingLoot => stolenItem != null || stolenGold > 0;
    public bool OnlyWakesWhenAttacked => WaitUntilAttacked || Mimic;
    public static bool IsDisguised(Character character) => character is Enemy enemy && enemy.GetComponent<EnemyBehavior>()?.Disguised == true;

    internal void Initialize(Enemy enemy)
    {
        owner = enemy;
        if (OnlyWakesWhenAttacked) owner.IsDormant = true;
        if (!Disguised || ChestVisualPrefab == null) return;
        foreach (Transform child in owner.VisualParent.transform)
            if (child.gameObject.activeSelf) { hiddenChildren.Add(child.gameObject); child.gameObject.SetActive(false); }
        disguise = Instantiate(ChestVisualPrefab, owner.VisualParent.transform);
        disguise.name = "Treasure chest disguise";
        disguise.transform.localPosition = Vector3.zero;
        disguise.transform.localRotation = Quaternion.identity;
        // Authored model uses the same local scale as ordinary dungeon treasure.
    }

    internal void Tick() { if (cooldown > 0) cooldown--; }

    internal List<GameAction> DeathEffects(Enemy enemy)
    {
        if (!CanExplode(enemy)) return new();
        GameMessages.ForCharacter(enemy, $"{GameMessages.Name(enemy)} exploded!");
        return Game.Instance.AllCharacters.Where(c => c != null && c != enemy && c.Vitals.HP > 0 &&
                TileWorldDungeon.ChevDistance(c.TilemapPosition, enemy.TilemapPosition) <= 1 &&
                Game.Instance.CurrentDungeon.CanWalkTo(enemy.TilemapPosition, c.TilemapPosition))
            .Select(c => (GameAction)new TakeDamageAction(enemy, c, ExplosionDamage) { AwardExperience = false }).ToList();
    }

    internal bool CanExplode(Enemy enemy) => ExplodesOnDeath &&
        !enemy.StatusEffects.Any(s => s is SilenceStatusEffect && !s.IsExpired());

    internal void RevealIfMoved() { if (Disguised) Provoke(); }

    internal void Provoke()
    {
        if (owner == null) owner = GetComponent<Enemy>();
        bool wasDormant = owner.IsDormant;
        owner.IsDormant = false;
        if (Disguised)
        {
            revealed = true;
            if (disguise != null) { disguise.SetActive(false); Destroy(disguise); }
            foreach (var child in hiddenChildren) if (child != null) child.SetActive(true);
            hiddenChildren.Clear();
            GameMessages.ForCharacter(owner, "The treasure chest was a mimic!");
        }
        else if (wasDormant) GameMessages.ForCharacter(owner, $"{GameMessages.Name(owner)} woke up!");
    }

    internal GameAction ChooseAction()
    {
        if (owner == null || owner.Team != Team.Enemy) return null;
        var target = owner.PursuitTarget;
        if (CarryingLoot || AlwaysFlees)
        {
            var threats = Game.Instance.AllCharacters.Where(c => c != null && c.Team == Team.Player && c.Vitals.HP > 0).ToList();
            if (threats.Count == 0) return new WaitAction();
            int Distance(Vector3Int p) => threats.Min(c => TileWorldDungeon.ChevDistance(c.TilemapPosition, p));
            var candidates = System.Enum.GetValues(typeof(Facing)).Cast<Facing>()
                .Select(f => owner.TilemapPosition + GridMovement.GetFacingOffset(f))
                .Where(p => new MovementAction(owner, owner.TilemapPosition, p).IsValid(owner) &&
                    !Game.Instance.AllCharacters.Any(c => c != null && c != owner && c.Vitals.HP > 0 && c.OverlapsWith(Character.ToBounds(p))))
                .OrderByDescending(Distance).ThenBy(p => Game.Instance.CurrentDungeon.IsHazard(p)).ToList();
            if (candidates.Count == 0) return null;
            var next = candidates[0];
            if (Distance(next) > Distance(owner.TilemapPosition))
            { owner.SetFacingByTargetPosition(next); return new MovementAction(owner, owner.TilemapPosition, next); }
            return null; // Cornered thieves fight back.
        }
        if (target == null || owner.Vitals.AttacksPerTurnLeft <= 0 || cooldown > 0) return null;
        if (RootsAdjacentTarget && AttackPolicy.CanAttack(Game.Instance,target,owner) &&
            !target.StatusEffects.OfType<GoopiRootStatusEffect>().Any(r=>r.Source==owner && r.Holding)) return new RootAttackAction(owner,target);
        if (Steals && AttackPolicy.CanAttack(Game.Instance, target, owner) && CanSteal()) return new StealAction(this, target);
        if (Confusion != null && !target.StatusEffects.Any(s => s is ConfusionStatusEffect && !s.IsExpired()) && ClearShot(target))
            return new EnemyAbilityAction(this, target, true);
        if (RangedAttack && TileWorldDungeon.ChevDistance(owner.TilemapPosition, target.TilemapPosition) > 1 && ClearShot(target))
            return new EnemyAbilityAction(this, target, false);
        return null;
    }

    internal bool ClearShot(Character target)
    {
        if (owner == null) owner = GetComponent<Enemy>();
        if (target == null || !EnemyAwareness.CanNotice(owner, target) || !Game.Instance.CurrentDungeon.CanSee(owner, target)) return false;
        var delta = target.TilemapPosition - owner.TilemapPosition;
        if (delta == Vector3Int.zero || (delta.x != 0 && delta.y != 0 && Mathf.Abs(delta.x) != Mathf.Abs(delta.y))) return false;
        var direction = new Vector3Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
        return MissileTargeting.Trace(owner, direction, Range).Character == target;
    }

    private List<InventoryItem> StealableItems() => Game.Instance.PlayerController.Inventory.InventoryItems
        .Where(i => StealItems && i != null && !PartyRules.PartyMembers(Game.Instance).Any(a => a.Equipment.IsEquipped(i))).ToList();
    internal bool CanSteal() => !CarryingLoot && ((StealGold && Game.Instance.PlayerController.Gold > 0) || StealableItems().Count > 0);

    internal void Steal()
    {
        var player = Game.Instance.PlayerController;
        var items = StealableItems();
        if (items.Count > 0 && (!StealGold || player.Gold == 0 || Random.value < .5f))
        {
            stolenItem = items[Random.Range(0, items.Count)];
            player.Inventory.Remove(stolenItem);
            GameMessages.ForCharacter(owner, $"{GameMessages.Name(owner)} stole {stolenItem.ItemName}!");
        }
        else if (StealGold && player.Gold > 0)
        {
            stolenGold = Mathf.Min(player.Gold, Mathf.Max(1, Mathf.CeilToInt(player.Gold * .2f)));
            player.Gold -= stolenGold;
            GameMessages.ForCharacter(owner, $"{GameMessages.Name(owner)} stole {stolenGold} gold!");
        }
    }

    internal void DropStolenLoot()
    {
        if (!CarryingLoot) return;
        var dungeon = Game.Instance.CurrentDungeon;
        var item = stolenItem; int gold = stolenGold;
        stolenItem = null; stolenGold = 0; // Commit once, including repeated death responses.
        var cell = dungeon.GetDropPosition(owner.TilemapPosition);
        if (item != null) dungeon.SetDroppedItem(cell, item.ItemDefinition, item.StackStock).InventoryItem = item;
        if (gold > 0) dungeon.SetGoldAmount(cell, gold);
        GameMessages.ForCharacter(owner, $"{GameMessages.Name(owner)} dropped the stolen loot.");
    }

    private sealed class StealAction : GameAction
    {
        readonly EnemyBehavior behavior; readonly Character target;
        public StealAction(EnemyBehavior behavior, Character target) { this.behavior = behavior; this.target = target; }
        internal override bool IsValid(Character c) => c == behavior.owner && c.Team == Team.Enemy && target != null && target.Team == Team.Player &&
            behavior.CanSteal() && AttackPolicy.CanAttack(Game.Instance, target, c);
        internal override List<GameAction> ExecuteImmediate(Character c)
        {
            if (!IsValid(c)) return new();
            AddMetricsModification(c, (s,v) => v.AttacksPerTurnLeft--);
            Visuals.Configure(c.GetComponent<CharacterCombatEffects>()?.Steal ?? CombatVisualCatalog.Instance?.Steal, c, target.TilemapPosition);
            behavior.Steal(); Visuals.AddImpact(target, target.TilemapPosition, true); return new();
        }
        internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false) { if (!skipAnimation && Visuals.Sequence == null) { c.PlayAttackAnimation(); yield return new WaitForSeconds(.25f); c.PlayIdleAnimation(); } }
    }

    private sealed class RootAttackAction : GameAction
    {
        readonly Enemy source; readonly Character target;
        public RootAttackAction(Enemy source,Character target) {this.source=source;this.target=target;}
        internal override bool IsValid(Character c) => source!=null && source.Vitals.HP>0 && target!=null && target.Vitals.HP>0 && AttackPolicy.CanAttack(Game.Instance,target,source);
        internal override List<GameAction> ExecuteImmediate(Character c)
        {
            if(!IsValid(c)) return new();
            GoopiRootStatusEffect.Hold(source,target);
            Visuals.Configure(source.GetComponent<CharacterCombatEffects>()?.Root ?? CombatVisualCatalog.Instance?.Root, source, target.TilemapPosition);
            return new() { new AttackAction(source,source.TilemapPosition,target.TilemapPosition) };
        }
        internal override IEnumerator ExecuteRoutine(Character c,bool skipAnimation=false) {yield break;}
    }

    private sealed class EnemyAbilityAction : GameAction
    {
        readonly EnemyBehavior behavior; readonly Character target; readonly bool confuse;
        public EnemyAbilityAction(EnemyBehavior behavior, Character target, bool confuse) { this.behavior = behavior; this.target = target; this.confuse = confuse; }
        internal override bool IsValid(Character c) => c == behavior.owner && c.Team == Team.Enemy && target != null && target.Team != c.Team && c.Vitals.AttacksPerTurnLeft > 0 && behavior.ClearShot(target);
        internal override List<GameAction> ExecuteImmediate(Character c)
        {
            if (!IsValid(c)) return new();
            AddMetricsModification(c, (s,v) => v.AttacksPerTurnLeft--);
            c.SetFacingByTargetPosition(target.TilemapPosition);
            behavior.cooldown = confuse ? 4 : 2;
            Visuals.Configure(confuse ? c.GetComponent<CharacterCombatEffects>()?.Confusion ?? CombatVisualCatalog.Instance?.Confusion : CharacterCombatEffects.Attack(c, true), c, target.TilemapPosition);
            if (confuse)
            {
                GameMessages.ForCharacter(c, $"{GameMessages.Name(c)} cast Confusion!");
                return new() { new ApplyStatusEffectAction(target, behavior.Confusion, c) };
            }
            return new() { new RangedAttackAction(c, target, Mathf.Max(1,c.FinalStats.Strength), null) };
        }
        internal override IEnumerator ExecuteRoutine(Character c, bool skipAnimation = false)
        { if (!skipAnimation && Visuals.Sequence == null) yield return MissileTargeting.Animate(c, target.TilemapPosition, null); }
    }
}
