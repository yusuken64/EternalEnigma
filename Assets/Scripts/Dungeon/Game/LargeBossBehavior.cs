using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using UnityEngine;

/// <summary>Reusable telegraph, area, displacement, status and hazard rules for every large family.</summary>
[DisallowMultipleComponent]
public sealed class LargeBossBehavior : MonoBehaviour
{
    public string Family;
    public int WarningTurns = 1, CooldownTurns = 3, EffectTurns = 2;
    readonly List<GameObject> marks = new();
    readonly List<Field> fields = new();
    readonly List<(DungeonProp prop, int expires)> cages = new();
    readonly List<Vector3Int> warned = new();
    Enemy owner;
    Character markedTarget;
    Vector3Int markedCenter;
    int warning, cooldown, turns, combo;
    public bool IsTelegraphing => warning > 0;
    internal IReadOnlyList<Vector3Int> MarkedTiles => warned;
    internal static Enemy Prefab(string family) => Resources.Load<Enemy>("Bosses/" + family.Replace(" ", ""));
    sealed class Field { public HashSet<Vector3Int> Cells; public string Kind; public int Turns; public readonly List<GameObject> Visuals = new(); }

    internal GameAction ChooseAction()
    {
        owner ??= GetComponent<Enemy>();
        return new BossTurnAction(this);
    }

    List<GameAction> TakeTurn(GameAction turn)
    {
        turns++;
        var actions = TickFields();
        foreach (var cage in cages.Where(c => c.expires <= turns).ToArray())
        { if (cage.prop != null) cage.prop.ApplyDamage(int.MaxValue); cages.Remove(cage); }
        var targets = Game.Instance.AllCharacters.Where(c => c != null && c.Team != owner.Team && c.Vitals.HP > 0).ToList();
        var target = targets.OrderBy(c => TileWorldDungeon.ChevDistance(owner.TilemapPosition, c.TilemapPosition)).ThenBy(c => c.TilemapPosition.x).ThenBy(c => c.TilemapPosition.y).FirstOrDefault();
        if (target == null) return actions;
        if (warning > 0)
        {
            if (--warning == 0) { Feedback(turn, true); actions.AddRange(ResolveSpecial()); ClearMarks(); cooldown = CooldownTurns; }
            return actions;
        }
        if (combo > 0)
        {
            combo--;
            if (AttackPolicy.CanAttack(Game.Instance,target,owner)) actions.Add(new AttackAction(owner,owner.TilemapPosition,target.TilemapPosition));
            return actions;
        }
        if (cooldown > 0) cooldown--;
        if (cooldown == 0 && TileWorldDungeon.ChevDistance(owner.TilemapPosition, target.TilemapPosition) <= 8 &&
            !owner.StatusEffects.Any(s => s is SilenceStatusEffect && !s.IsExpired()))
        {
            markedTarget = target; markedCenter = target.TilemapPosition;
            if (Family == "Chest Monster") markedCenter = SkillMovement.LastFreeAlong(target,SkillMovement.Step(target.TilemapPosition,owner.TilemapPosition),2);
            warned.Clear(); warned.AddRange(Area(markedCenter).Where(Game.Instance.CurrentDungeon.IsFloorCell).Distinct());
            warning = Mathf.Max(1, WarningTurns);
            foreach (var cell in warned) marks.Add(EncounterRuntime.Marker(Game.Instance.CurrentDungeon, Family + " warning", cell, new Color(1,.25f,.05f), .04f));
            GameMessages.ForCharacter(owner, $"{GameMessages.Name(owner)} prepares {BossCatalog.All.Concat(FinaleRoster.Bosses).First(p => p.Family == Family).Ability}!");
            Feedback(turn, false);
            if (Family == "Black Knight") ApplyEffect(owner,"Guard",EffectTurns+1);
            if (Family == "Metal Slime") ApplyEffect(owner,"Armored",EffectTurns);
            if (Family == "Chest Monster") actions.Add(new BossDisplaceAction(owner,target,true,2));
            return actions;
        }
        if (AttackPolicy.CanAttack(Game.Instance,target,owner)) actions.Add(new AttackAction(owner,owner.TilemapPosition,target.TilemapPosition));
        else
        {
            owner.PursuitPosition = target.TilemapPosition;
            var path = owner.CalculatePursuitPath();
            if (path != null && path.Count > 0)
            {
                var next = new Vector3Int(path[0].X,path[0].Y);
                var step = new MovementAction(owner,owner.TilemapPosition,next);
                if (step.IsValid(owner)) actions.Add(step);
            }
        }
        return actions;
    }

    void Feedback(GameAction action, bool release)
    {
        var catalog = CombatVisualCatalog.Instance;
        if (catalog == null) return;
        var profile = !release ? catalog.Utility : Family switch
        {
            "Dragon" or "Bone Dragon" => catalog.FireBreath,
            "Salamander" or "Flying Demon" or "Demon King" or "Castle Monster" => catalog.Explosion,
            "Ice Golem" => catalog.Ice, "Stingray" => catalog.Lightning,
            "Smiling Mushroom" or "Specter" => catalog.Heal,
            "Beholder" or "Evil Mage" or "Bat" => catalog.Confusion,
            "Spider" or "Monster Plant" or "Crab Monster" or "Angry Mushroom" => catalog.Root,
            _ => catalog.Utility
        };
        action.Visuals.Configure(profile, owner, release ? markedCenter : owner.TilemapPosition);
        if (action.Visuals.Sequence != null) action.Visuals.Sequence.SingleFlight = true;
        action.Visuals.AddImpact(release ? markedTarget : owner, release ? markedCenter : owner.TilemapPosition, true);
        if (release) GameMessages.ForCharacter(owner, $"{GameMessages.Name(owner)} uses {BossCatalog.All.Concat(FinaleRoster.Bosses).First(p => p.Family == Family).Ability}!");
    }

    IEnumerable<Vector3Int> Area(Vector3Int target)
    {
        var center = owner.TilemapPosition;
        bool aroundSelf = Family is "Bat" or "Cactus" or "Angry Mushroom" or "Smiling Mushroom" or "Turtle Shell" or "Golem" or "Ice Golem" or "Orc" or "Black Knight";
        int radius = Family is "Turtle Shell" or "Golem" or "Ice Golem" or "Bat" ? 4 : Family is "Cyclops" or "Worm Monster" ? 1 : 2;
        if (aroundSelf) target = center;
        var direction = SkillMovement.Step(center,target);
        for (int x = -8; x <= 8; x++) for (int y = -8; y <= 8; y++)
        {
            var cell = center + new Vector3Int(x,y);
            int distance = Math.Max(Math.Abs(x),Math.Abs(y));
            bool include;
            if (Family == "Bishop Knight") include = distance <= 7 && (x == 0 || y == 0);
            else if (Family is "Specter" or "Rat Assassin" or "Salamander" or "Battle Bee" or "Metal Slime")
                include = distance <= 7 && x * direction.y == y * direction.x && x * direction.x + y * direction.y > 0;
            else if (Family is "Dragon" or "Bone Dragon" or "Stingray" or "Fishman")
                include = distance <= 6 && x * direction.x + y * direction.y > 0 && Math.Abs(x * direction.y - y * direction.x) <= distance;
            else if (Family == "Cactus") include = distance <= 6 && (x == 0 || y == 0 || Math.Abs(x) == Math.Abs(y));
            else include = TileWorldDungeon.ChevDistance(cell,target) <= radius;
            if (include) yield return cell;
        }
    }

    List<GameAction> ResolveSpecial()
    {
        var actions = new List<GameAction>();
        if (Family == "Orc") { ApplyEffect(owner,"Empowered",EffectTurns); return actions; }
        if (Family == "Black Knight") return actions;
        if (Family == "Smiling Mushroom") { AddField("Regeneration"); return TickFields(false); }
        if (Family is "Slime" or "Werewolf" or "Battle Bee" or "Worm Monster" or "Flying Demon" or "Salamander" or "Metal Slime")
        {
            Vector3Int? destination;
            if (Family is "Salamander" or "Battle Bee" or "Metal Slime")
            {
                var direction=SkillMovement.Step(owner.TilemapPosition,markedCenter);
                destination=SkillMovement.LastFreeAlong(owner,direction,6);
                if (Family == "Metal Slime" && destination.Value == owner.TilemapPosition)
                    destination=SkillMovement.LastFreeAlong(owner,new Vector3Int(-direction.y,direction.x),4);
            }
            else destination = Enumerable.Range(-3,7).SelectMany(x=>Enumerable.Range(-3,7).Select(y=>markedCenter+new Vector3Int(x,y)))
                .Where(p => SkillMovement.CanOccupy(owner,p)).OrderBy(p => TileWorldDungeon.ChevDistance(p,markedCenter)).ThenBy(p=>p.x).ThenBy(p=>p.y).Cast<Vector3Int?>().FirstOrDefault();
            if (destination.HasValue) actions.Add(new BossPlaceAction(owner,destination.Value));
        }
        var hit = Game.Instance.AllCharacters.Where(c => c != null && c.Team != owner.Team && c.Vitals.HP > 0 && c.ToBounds().Cells().Any(warned.Contains)).ToArray();
        foreach (var target in hit)
        {
            int damage = Mathf.Max(1,owner.FinalStats.Strength);
            if (Family == "Cyclops") damage *= 2;
            var element = Family is "Dragon" or "Bone Dragon" or "Salamander" or "Flying Demon" or "Demon King" or "Castle Monster" ? DamageElement.Fire : Family == "Ice Golem" ? DamageElement.Ice : Family == "Stingray" ? DamageElement.Lightning : DamageElement.Physical;
            actions.Add(new TakeDamageAction(owner,target,damage,element:element));
            if (Family == "Rat Assassin")
                for (int bolt=0;bolt<2;bolt++) actions.Add(new TakeDamageAction(owner,target,Mathf.Max(1,damage/2)));
            if (Family is "Slime" or "Fishman" or "Turtle Shell" or "Golem") actions.Add(new BossDisplaceAction(owner,target,false,2));
            if (Family is "Monster Plant" or "Chest Monster") actions.Add(new BossDisplaceAction(owner,target,true,2));
            if (Family == "Naga Wizard")
            {
                var destination = SkillMovement.FindTileBehind(target,owner);
                if (destination.HasValue) actions.Add(new BossPlaceAction(target,destination.Value));
            }
            string ailment = Family switch {
                "Bat" => "Silence", "Crab Monster" or "Monster Plant" or "Spider" or "Ice Golem" => "Root",
                "Evil Mage" => "Sleep", "Rat Assassin" or "Battle Bee" or "Angry Mushroom" => "Poison",
                "Werewolf" => "Bleed", "Beholder" => "Confusion", _ => null
            };
            if (ailment != null) actions.Add(new BossAilmentAction(owner,target,ailment,EffectTurns));
            if (Family == "Specter") actions.Add(new ModifyStatAction(owner,owner,(_,v) => v.HP += damage,false));
        }
        if (Family is "Angry Mushroom" or "Spider" or "Salamander" or "Flying Demon" or "Demon King")
            AddField(Family == "Spider" ? "Root" : Family == "Angry Mushroom" ? "Poison" : "Fire");
        if (Family == "Metal Slime") ApplyEffect(owner,"Exposed",2);
        if (Family == "Lizard Warrior") combo = 2;
        if (Family == "Skeleton")
            foreach (var cell in PuzzleState.CardinalNeighbors(new GridPoint(markedCenter.x,markedCenter.y)).Select(p=>p.ToCell()))
                if (Game.Instance.CurrentDungeon.IsWalkable(cell) && Game.Instance.CurrentDungeon.OverlapsAnyOtherCharacter(null,Character.ToBounds(cell)) == null)
                {
                    var definition = new DungeonScenery(new GridPoint(cell.x,cell.y),DungeonSceneryKind.Destructible,Mathf.Max(1,owner.FinalStats.Strength),0,SceneryReward.None);
                    var prop = DungeonProp.Create(Game.Instance.CurrentDungeon,definition,Game.Instance.DungeonGenerator.ThemeCatalog.Get(Game.Instance.DungeonGenerator.CurrentVisuals));
                    prop.name = "Destructible bone cage"; cages.Add((prop,turns+EffectTurns));
                }
        return actions;
    }

    void AddField(string kind)
    {
        var field = new Field { Cells = new HashSet<Vector3Int>(warned), Kind = kind, Turns = EffectTurns };
        foreach (var cell in field.Cells) field.Visuals.Add(EncounterRuntime.Marker(Game.Instance.CurrentDungeon,kind+" field",cell,kind == "Regeneration" ? Color.green : kind == "Fire" ? new Color(1,.4f,.1f) : new Color(.6f,.2f,.8f),.03f));
        fields.Add(field);
    }

    List<GameAction> TickFields(bool age = true)
    {
        var actions = new List<GameAction>();
        foreach (var field in fields.ToArray())
        {
            foreach (var target in Game.Instance.AllCharacters.Where(c => c != null && c.Vitals.HP > 0 && field.Cells.Contains(c.TilemapPosition)))
            {
                if (field.Kind == "Regeneration") { if (target.Team == owner.Team) actions.Add(new ModifyStatAction(owner,target,(_,v)=>v.HP+=Mathf.Max(1,owner.FinalStats.HPMax/10),false)); }
                else if (target.Team != owner.Team)
                {
                    if (field.Kind == "Fire") actions.Add(new TakeDamageAction(owner,target,Mathf.Max(1,owner.FinalStats.Strength/2),element:DamageElement.Fire));
                    else actions.Add(new BossAilmentAction(owner,target,field.Kind,EffectTurns));
                }
            }
            if (age && --field.Turns <= 0) { foreach (var visual in field.Visuals) if (visual != null) Destroy(visual); fields.Remove(field); }
        }
        return actions;
    }
    void ClearMarks() { foreach (var mark in marks) if (mark != null) Destroy(mark); marks.Clear(); }
    void OnDestroy()
    {
        ClearMarks(); foreach (var field in fields) foreach (var visual in field.Visuals) if (visual != null) Destroy(visual);
        foreach (var cage in cages) if (cage.prop != null) cage.prop.ApplyDamage(int.MaxValue);
    }

    internal static void ApplyEffect(Character target,string kind,int duration,Character source = null)
    {
        var effect = target.StatusEffects.OfType<BossEffectStatus>().FirstOrDefault(s=>s.Kind==kind);
        if (effect == null) { effect = new GameObject(kind).AddComponent<BossEffectStatus>(); effect.transform.SetParent(target.transform); target.StatusEffects.Add(effect); }
        effect.Kind = kind; effect.TurnsLeft = Math.Max(effect.TurnsLeft,duration); effect.DamageSource = source;
        target.InvalidateCachedStats();
    }
    sealed class BossTurnAction : GameAction
    {
        readonly LargeBossBehavior behavior;
        public BossTurnAction(LargeBossBehavior behavior) { this.behavior=behavior; }
        internal override bool IsValid(Character c) => behavior!=null && c.Vitals.HP>0;
        internal override List<GameAction> ExecuteImmediate(Character c) => behavior.TakeTurn(this);
        internal override IEnumerator ExecuteRoutine(Character c,bool skipAnimation=false)
        { yield break; }
    }
}

internal sealed class BossAilmentAction : GameAction
{
    readonly Character source,target; readonly string kind; readonly int duration;
    public BossAilmentAction(Character source,Character target,string kind,int duration) { this.source=source;this.target=target;this.kind=kind;this.duration=duration; }
    internal override bool IsValid(Character c) => target!=null && target.Vitals.HP>0;
    internal override List<GameAction> ExecuteImmediate(Character c) { if(IsValid(c)) LargeBossBehavior.ApplyEffect(target,kind,duration,source);return new(); }
    internal override IEnumerator ExecuteRoutine(Character c,bool skipAnimation=false) { yield break; }
}
internal sealed class BossPlaceAction : GameAction
{
    readonly Character target; readonly Vector3Int destination;
    public BossPlaceAction(Character target,Vector3Int destination) { this.target=target;this.destination=destination; }
    internal override bool IsValid(Character c) => target!=null && SkillMovement.CanOccupy(target,destination);
    internal override List<GameAction> ExecuteImmediate(Character c) { if(IsValid(c)) SkillMovement.Place(target,destination);return new(); }
    internal override IEnumerator ExecuteRoutine(Character c,bool skipAnimation=false) { if(target!=null) target.transform.position=Game.Instance.CurrentDungeon.CellToWorld(target.TilemapPosition);yield break; }
}
internal sealed class BossDisplaceAction : GameAction
{
    readonly Character source,target; readonly bool pull; readonly int distance;
    public BossDisplaceAction(Character source,Character target,bool pull,int distance) { this.source=source;this.target=target;this.pull=pull;this.distance=distance; }
    internal override bool IsValid(Character c) => source!=null && target!=null && target.Vitals.HP>0;
    internal override List<GameAction> ExecuteImmediate(Character c)
    { if(IsValid(c)) SkillMovement.Place(target,SkillMovement.LastFreeAlong(target,SkillMovement.Step(source.TilemapPosition,target.TilemapPosition)*(pull?-1:1),distance));return new(); }
    internal override IEnumerator ExecuteRoutine(Character c,bool skipAnimation=false) { if(target!=null) target.transform.position=Game.Instance.CurrentDungeon.CellToWorld(target.TilemapPosition);yield break; }
}
