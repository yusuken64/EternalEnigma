using System;
using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Classes;
using UnityEngine;

public sealed partial class AutoplayRunner
{
    readonly HashSet<Vector3Int> cautiousKnown = new(), cautiousWalkable = new(), cautiousHazards = new();
    readonly Dictionary<Vector3Int, int> cautiousVisits = new();
    readonly HashSet<Vector3Int> cautiousSurveyed = new();
    readonly Dictionary<Vector3Int, DungeonProp> cautiousProps = new();
    readonly HashSet<Vector3Int> cautiousPortals = new();
    readonly HashSet<string> cautiousUtilities = new();
    Vector3Int? cautiousStairs;
    Town cautiousRestTown;
    int cautiousBacksteps, cautiousWaiting;

    void ResetCautiousFloor()
    {
        cautiousKnown.Clear(); cautiousWalkable.Clear(); cautiousHazards.Clear(); cautiousVisits.Clear(); cautiousUtilities.Clear();
        cautiousSurveyed.Clear(); cautiousProps.Clear(); cautiousPortals.Clear(); ResetFinaleRoute();
        cautiousStairs = null; cautiousBacksteps = cautiousWaiting = 0;
    }

    bool PrepareCautiousTown(Town town)
    {
        if (cautiousRestTown == town || !town.Services.NeedsRest) return false;
        var inn = town.TownBuildings.FirstOrDefault(b => b.Definition.DialogId == "inn" || b.Definition.DialogPrefab is InnDialog);
        if (inn == null) { Finish("Unsupported", "Cautious preparation needs a reachable inn."); return true; }
        var target = ServicePosition(town, inn);
        if (town.TownPlayer.ControllingTownAlly.TilemapPosition != target) { MoveTowardTownTarget(town, target); return true; }
        if (town.Services.Rest(out var reason))
        { Report.Balance.RestVisits++; BalanceDecision("Rest", "Inn", "Restore carried vitals before spending on supplies.", true); Log("Rest at inn"); }
        else BalanceDecision("Rest", "Inn", reason);
        cautiousRestTown = town;
        return true;
    }

    static int RecoveryAmount(ItemDefinition item, int kind)
    {
        if (item?.ItemEffectDefinition is not ModifyStatsItemEffectDefinition effect || effect.VitalModification == null) return 0;
        return Math.Max(0, kind == 0 ? effect.VitalModification.Hp : kind == 1 ? effect.VitalModification.Mana : effect.VitalModification.Hunger);
    }

    bool CautiousWantsOffer(Town town, TownBuildingDefinition building, TownShopOffer offer)
    {
        if (town.Services.OffersOpeningBread(building) && offer.Item.ItemName == "Bread") return true;
        var player = town.TownPlayer;
        float score = CautiousOfferValue(town, offer);
        var alternatives = town.TownBuildings.SelectMany(b => town.Services.Catalog(b.Definition)
            .Where(o => PurchaseRejection(town, b.Definition, o) == null).Select(o => (building: b.Definition, offer: o)))
            .Select(x => (x.building, x.offer, value: CautiousOfferValue(town, x.offer)))
            .OrderByDescending(x => x.value / Math.Max(1, x.offer.Price)).ThenBy(x => x.offer.Price)
            .ThenBy(x => x.offer.Item.ItemName, StringComparer.Ordinal).FirstOrDefault();
        bool chosen = score > 0 && alternatives.offer == offer;
        // Catalog normally returns authored offers; opening bread returns a copied offer.
        chosen |= score > 0 && alternatives.offer?.Item == offer.Item && alternatives.building == building;
        string reason = score <= 0 ? "Reserve already sufficient, upgrade too small, or purchase would consume emergency budget." :
            chosen ? "Best useful purchase per gold within the current reserve budget." : "Saving for a more useful available purchase.";
        Skip("balance-buy/" + town.Configuration.Id + "/" + offer.Item.ItemName, reason, item: offer.Item.ItemName);
        if (chosen) BalanceDecision("Purchase", offer.Item.ItemName, reason, true, score, offer.Price);
        var observation = BalancePurchase(offer.Item.ItemName); observation.LastReason = reason; if (chosen) observation.Wanted++;
        return chosen;
    }

    float CautiousOfferValue(Town town, TownShopOffer offer)
    {
        var p = town.TownPlayer;
        int count = Math.Max(1, p.RecruitedAllies.Count);
        for (int kind = 0; kind < 3; kind++)
        {
            int amount = RecoveryAmount(offer.Item, kind);
            if (amount <= 0) continue;
            int held = p.Inventory.Sum(i => RecoveryAmount(i.ItemDefinition, kind) * (i.StackStock ?? 1));
            int desired = kind == 0 ? 40 * count : kind == 1 ? 8 * count : 50 * count;
            if (held >= desired) continue;
            // The first reserve is worth more than topping up an already viable expedition.
            float urgency = held < desired / 3 ? 4 : 1;
            return Math.Min(desired - held, amount * offer.StackCount) * urgency * (kind == 1 ? 3 : kind == 2 ? .5f : 1);
        }
        if (offer.Item is not EquipmentItemDefinition equipment) return 0;
        if (equipment.IsAmmunition)
            return p.Inventory.Where(i => i.ItemName == offer.Item.ItemName).Sum(i => i.StackStock ?? 1) < 30 ? 25 : 0;
        if (!p.RecruitedAllies.Any(a => IsUpgrade(a, equipment))) return 0;
        bool needsRecovery = Enumerable.Range(0, 3).Any(k => p.Inventory.Sum(i => RecoveryAmount(i.ItemDefinition, k) * (i.StackStock ?? 1)) < (k == 0 ? 40 : k == 1 ? 8 : 50) * count);
        if (needsRecovery && offer.Price > p.Gold * .6f) return 0;
        return Math.Max(1, EquipmentScore(equipment)) * 12;
    }

    static int CautiousTrainingPriority(TownAlly ally, TrainerOffer offer)
    {
        if (offer.Kind == SkillKind.Mastery && offer.CurrentRank == 0) return 0;
        var intent = SkillIntents.Classify(offer.Skill);
        bool HasIntent(SkillIntent wanted) => new[] { ally.PrimaryClass, ally.SecondaryClass }.Where(c => c != null)
            .SelectMany(c => c.Skills).Any(s => s.Skill != null && ally.GetRank(s.Skill.SkillName) > 0 && SkillIntents.Classify(s.Skill).Has(wanted));
        bool hasHealing = HasIntent(SkillIntent.Heal);
        if (intent.Has(SkillIntent.Heal) && !hasHealing) return 1;
        // Permanent gains help every exchange, including expeditions after Mana runs out.
        var stats = offer.Skill.PassiveStatModification;
        if (offer.CurrentRank == 0 && offer.Skill.ActivationType != ActivationType.Active && stats != null &&
            (stats.Strength > 0 || stats.MagicPower > 0 || stats.HPMax > 0 || stats.ManaMax > 0)) return 2;
        if (offer.CurrentRank == 0 && intent.Has(SkillIntent.Damage) && !HasIntent(SkillIntent.Damage)) return 3;
        if (offer.CurrentRank == 0 && (intent.Has(SkillIntent.Revive) || intent.Has(SkillIntent.Cure) || intent.Has(SkillIntent.CrowdControl))) return 4;
        return offer.CurrentRank == 0 ? 5 : 6 + offer.NextCost;
    }

    internal static float CautiousDamageValue(float damage, float hp, float incomingPrevented, int mana, int castTurns) =>
        (Math.Min(Math.Max(0, damage), Math.Max(0, hp)) + (damage >= hp ? incomingPrevented : 0)) /
        Math.Max(1, 1 + castTurns) - Math.Max(0, mana) * 1.5f;

    float IncomingDamage(Game game, Ally ally, Vector3Int cell, IReadOnlyList<Character> visible)
    {
        float sum = 0;
        foreach (var e in visible)
        {
            int distance = TileWorldDungeon.ChevDistance(cell, e.TilemapPosition);
            var behavior = e.GetComponent<EnemyBehavior>();
            bool ranged = behavior != null && (behavior.RangedAttack || behavior.FireBreath);
            if (distance > (ranged ? behavior.Range : 2)) continue;
            float damage = SkillEstimates.BaseAttackDamage(e, ally);
            sum += Math.Max(1, damage) * (distance <= 1 || ranged ? 1.25f : .6f);
        }
        return sum;
    }

    internal AllySkillChoice CautiousSkillChoice(AllySkillContext context, IReadOnlyList<IAllySkillEvaluator> evaluators)
    {
        var actor = context.Ally;
        var visible = context.VisibleEnemies;
        float incoming = IncomingDamage(context.Game, actor, actor.TilemapPosition, visible);
        float basic = AllyCombat.AttacksFrom(context.Game, actor, actor.TilemapPosition, visible)
            .Select(a => CautiousDamageValue(a.Value, a.Target.Vitals.HP, incoming, 0, 0)).DefaultIfEmpty(0).Max();
        AllySkillChoice best = null;
        foreach (var skill in actor.Skills.Where(s => s != null && s.ActivationType == ActivationType.Active))
        {
            var row = Report.Balance.Abilities.FirstOrDefault(a => a.ActorId == ActorId(actor) && a.Skill == skill.SkillName && a.Rank == skill.Rank);
            if (row == null) Report.Balance.Abilities.Add(row = new BalanceAbilityObservation { ActorId = ActorId(actor), Skill = skill.SkillName, Rank = skill.Rank });
            row.Decisions++;
            if (!actor.CanCast(skill, out var reason))
            { row.LastReason = reason; if (actor.Vitals.Mana < skill.ManaCost) row.Unaffordable++; continue; }
            var intent = SkillIntents.Classify(skill);
            if (intent.Has(SkillIntent.Never)) { row.LastReason = "Retreat is considered by expedition policy."; continue; }
            if (!context.CanAfford(skill)) { row.LastReason = "Preserve emergency Mana."; continue; }
            var options = SkillCastOptions.Enumerate(actor, skill).Where(o => o.Affected.All(c => c == actor || context.Game.CurrentDungeon.CanSee(actor, c))).ToArray();
            if (options.Length == 0) { row.LastReason = "No visible legal target."; continue; }
            row.LegalOpportunities++;
            AllySkillChoice choice = null;
            if (intent.Has(SkillIntent.Heal))
            {
                foreach (var option in options)
                {
                    float value = option.Affected.OfType<Ally>().Sum(a => {
                        float threat = IncomingDamage(context.Game, a, a.TilemapPosition, visible);
                        float restore = SkillEstimates.EstimateHealing(skill, actor, a);
                        bool urgent = a.Vitals.HP <= threat * 2 + a.FinalStats.HPMax * .2f;
                        if (!urgent && (a.Vitals.HP > a.FinalStats.HPMax * .7f || restore < a.FinalStats.HPMax * .25f)) return 0;
                        return restore * (urgent ? 5 : .8f);
                    }) - skill.ManaCost * 1.5f;
                    if (value > 0 && (choice == null || value > choice.Score)) choice = new AllySkillChoice("CautiousHeal", option, value, "Heal before projected incoming damage becomes lethal.");
                }
            }
            else if (intent.Has(SkillIntent.Damage))
            {
                foreach (var option in options.Where(o => o.Affected.All(visible.Contains)))
                {
                    float value = option.Affected.Sum(c => CautiousDamageValue(SkillEstimates.EstimateDamage(skill, actor, c), c.Vitals.HP,
                        IncomingDamage(context.Game, actor, actor.TilemapPosition, new[] { c }), 0, skill.InitialCastTime(actor))) - skill.ManaCost * 1.5f;
                    if (value > basic && (choice == null || value > choice.Score)) choice = new AllySkillChoice("CautiousDamage", option, value, "Effective damage and retaliation avoided justify Mana and casting turns.");
                }
            }
            else if (intent.Has(SkillIntent.Utility))
            {
                bool repeatable = skill.ActionEffects.Any(e => e is FieldKitchenAction);
                if (visible.Count == 0 && (repeatable || !cautiousUtilities.Contains(skill.SkillName)))
                    choice = new UtilityEvaluator().Evaluate(context.ForSkill(skill));
            }
            else
                foreach (var evaluator in evaluators.Where(e => e is not DamageEvaluator && e is not EmergencyHealEvaluator && e is not UtilityEvaluator))
                {
                    var candidate = evaluator.Evaluate(context.ForSkill(skill));
                    if (candidate != null) { choice = candidate; break; }
                }
            row.LastReason = choice?.Reason ?? "Alternative action has better expected value.";
            if (choice != null)
            {
                float score = choice.Score + (intent.Has(SkillIntent.Revive) ? 1000 : intent.Has(SkillIntent.Cure) ? 100 : 0);
                if (score > (best?.Score ?? Math.Max(0, basic))) best = new AllySkillChoice(choice.Evaluator, choice.Option, score, choice.Reason);
            }
        }
        if (best != null)
        {
            var row = Report.Balance.Abilities.First(a => a.ActorId == ActorId(actor) && a.Skill == best.Option.Skill.SkillName && a.Rank == best.Option.Skill.Rank);
            row.Selected++; row.EstimatedValue += best.Score;
            cautiousUtilities.Add(best.Option.Skill.SkillName);
            BalanceDecision("Ability", best.Option.Skill.SkillName, best.Reason, true, best.Score, best.Option.Skill.ManaCost, actor, incoming, best.Option.Skill.Rank);
        }
        return best;
    }

    void CautiousDungeonTick(Game game)
    {
        var ally = game.PlayerController.ControlledAlly;
        if (ally == null || game.Finale?.IsTransitioning == true) return;
        BalanceCheckpoint();
        var dungeon = game.CurrentDungeon;
        var from = ally.TilemapPosition;
        var sight = dungeon.GetVisibleTiles(ally, from);
        cautiousSurveyed.Add(from);
        foreach (var p in sight) { cautiousKnown.Add(p); if (dungeon.IsWalkable(p)) cautiousWalkable.Add(p); else cautiousWalkable.Remove(p); }
        foreach (var prop in dungeon.Interactables.OfType<DungeonProp>().Where(p => sight.Contains(p.Position) && p.Alive)) cautiousProps[prop.Position] = prop;
        foreach (var portal in dungeon.Interactables.OfType<FinalePortal>().Where(p => sight.Contains(p.Position))) cautiousPortals.Add(portal.Position);
        if (game.FloorReveal?.LayoutRevealed == true)
            for (int x = 0; x < dungeon.dungeonWidth; x++) for (int y = 0; y < dungeon.dungeonHeight; y++)
            { var p = new Vector3Int(x,y); cautiousKnown.Add(p); if (dungeon.IsWalkable(p)) cautiousWalkable.Add(p); }
        foreach (var p in sight.Where(dungeon.IsHazard)) cautiousHazards.Add(p);
        foreach (var trap in dungeon.Interactables.OfType<Trap>().Where(t => sight.Contains(t.Position) && t.VisualObject != null && t.VisualObject.activeInHierarchy)) cautiousHazards.Add(trap.Position);
        var stairs = dungeon.Interactables.OfType<Stairs>().FirstOrDefault(s => sight.Contains(s.Position) || game.FloorReveal?.LayoutRevealed == true);
        if (stairs != null) cautiousStairs = stairs.Position;
        cautiousVisits.TryGetValue(from, out var visits); cautiousVisits[from] = visits + 1;
        var enemies = AllyCombat.VisibleHostiles(game, ally);
        float incoming = IncomingDamage(game, ally, from, enemies);
        if (balanceStage != null) { if (enemies.Count > 0) balanceStage.VisibleEnemyDecisions++; if (incoming * 2 >= ally.Vitals.HP) balanceStage.DangerousDecisions++; }
        if (ally.PendingCast != null) { Act(ally, new WaitAction(), "Continue cautious cast"); return; }
        var practice = game.GetComponent<OpeningDungeonLesson>()?.AutoplayAction(ally);
        if (practice != null) { Act(ally, practice, "Practise opening ability"); return; }
        var puzzle = game.Encounter?.AutoplayPuzzleAction(ally);
        if (puzzle != null) { Act(ally, puzzle, "Solve encounter puzzle"); return; }
        if (TryCautiousRecovery(game, ally, incoming)) return;
        bool threatened = ally.Vitals.HP < Math.Max(ally.FinalStats.HPMax * .25f, incoming * 1.5f);
        var retreat = ally.Skills.FirstOrDefault(s => s.ActionEffects.Any(e => e is RetreatAction) && ally.CanCast(s, out _));
        if (threatened && retreat != null)
        {
            if (Report.Balance.Retreats >= Options.Balance.MaxRetreats) { Finish("ResourceLimit", "Cautious expedition exhausted its retreat budget."); return; }
            var action = new SkillAction(ally, retreat, ally);
            if (action.ValidateCommand(ally, out _))
            {
                Report.Balance.Retreats++; if (balanceStage != null) { balanceStage.Retreats++; balanceStage.Outcome = "Retreated"; }
                BalanceDecision("Retreat", retreat.SkillName, "Current health cannot safely absorb the visible threat.", true, cost: retreat.ManaCost, actor: ally, incoming: incoming);
                Act(ally, action, "Retreat to preserve party and loot"); return;
            }
        }
        var skillPolicy = new AllySkillPolicy(game, ally, 0) { IncludeControlledAlly = true };
        if (skillPolicy.ShouldRun())
        { var action = skillPolicy.GetActions().FirstOrDefault(); if (action != null) { Act(ally, action, "Cautious skill " + skillPolicy.LastChoice.Option.Skill.SkillName); return; } }
        var attacks = AllyCombat.AttacksFrom(game, ally, from, enemies);
        var attack = attacks.OrderByDescending(a => CautiousDamageValue(a.Value, a.Target.Vitals.HP,
            IncomingDamage(game, ally, from, new[] { a.Target }), 0, 0)).FirstOrDefault();
        var occupied = game.AllCharacters.Where(c => c != null && c != ally && c is not Ally && dungeon.CanSee(ally,c))
            .SelectMany(c => c.ToBounds().Cells()).ToHashSet();
        bool CanStep(Vector3Int a, Vector3Int b) => cautiousWalkable.Contains(b) && !cautiousHazards.Contains(b) &&
            (game.Finale?.OnOverworld != true || game.Finale.Session.CanStep(a.ToGridPoint(),b.ToGridPoint())) &&
            dungeon.CanWalkTo(a,b) && (b.x == a.x || b.y == a.y || cautiousWalkable.Contains(new Vector3Int(a.x,b.y)) && cautiousWalkable.Contains(new Vector3Int(b.x,a.y))) &&
            !occupied.Contains(b);
        var steps = Steps.Select(d => from + d).Where(p => sight.Contains(p) && CanStep(from,p) && DungeonPlacement.CanStep(dungeon,ally,from,p)).ToArray();
        var safest = steps.OrderBy(p => IncomingDamage(game,ally,p,enemies)).ThenBy(p => cautiousVisits.GetValueOrDefault(p)).ThenBy(p => p.y).ThenBy(p => p.x).Select(p => (Vector3Int?)p).FirstOrDefault();
        if (incoming > 0 && cautiousBacksteps < 3 && safest.HasValue && IncomingDamage(game,ally,safest.Value,enemies) < incoming * .8f &&
            (threatened || enemies.Count > 1) && !(attack != null && attack.Value >= attack.Target.Vitals.HP))
        { cautiousBacksteps++; MoveCautiously(ally,safest.Value,"Reduce exposure before committing to combat",incoming); return; }
        if (attack != null) { cautiousBacksteps = cautiousWaiting = 0; BalanceDecision("Attack",attack.Target.name,"Focus a visible target and reduce retaliation.",true,attack.Value,actor:ally,incoming:incoming); Act(ally,attack.ToAction(ally),"Cautious attack"); return; }
        if (enemies.Count > 0 && cautiousWaiting++ < 3)
        { BalanceDecision("Wait","Hold position","Let visible enemies approach without exposing unexplored terrain.",true,actor:ally,incoming:incoming); Act(ally,new WaitAction(),"Hold a known position for approaching enemies"); return; }
        if (enemies.Count == 0) cautiousWaiting = 0;
        var stash = dungeon.Interactables.OfType<EncounterStash>().FirstOrDefault(s => s.Position == from &&
            (s.IsRecovery ? game.Prison?.Recovered != true || game.Prison.Overflow.Count > 0 && game.PlayerController.Inventory.CanAdd() : !s.Opened));
        if (stash != null) { Act(ally,new InteractAction(stash),"Collect discovered encounter supplies"); return; }
        if (ally != game.PlayerController.PartyLeader)
        { Act(ally, AllyNavigation.FollowOrSearch(game,ally), "Cautious companion follow"); return; }
        if (cautiousStairs == from && game.CanExitFloor)
        { Act(ally,new InteractAction(dungeon.Interactables.OfType<Stairs>().First(s => s.Position == from)),"Use discovered stairs"); return; }
        // Only visible or remembered cells can enter the route search. No hidden occupancy queries.
        var queue = new Queue<Vector3Int>(); var previous = new Dictionary<Vector3Int,Vector3Int> { [from] = from };
        var distances = new Dictionary<Vector3Int,int> { [from] = 0 }; queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var next in Steps.Select(d => p+d).Where(n => !previous.ContainsKey(n) && CanStep(p,n)))
            { previous.Add(next,p); distances.Add(next,distances[p]+1); queue.Enqueue(next); }
            // An inspected warp exposes its available destination through the normal destination menu.
            if (game.Finale?.OnOverworld == true && cautiousPortals.Contains(p))
                foreach (var warp in game.Finale.Session.WarpDestinations(p.ToGridPoint()))
                {
                    var next = warp.Destination.ToCell();
                    if (!previous.ContainsKey(next) && !occupied.Contains(next))
                    { previous.Add(next,p); distances.Add(next,distances[p]+1); queue.Enqueue(next); }
                }
        }
        var loot = dungeon.Interactables.Where(i => i != null && sight.Contains(i.Position) && (i is Gold or DroppedItem or SmallKey || i is EncounterStash s && !s.Opened))
            .Where(i => i is not DroppedItem || game.PlayerController.Inventory.CanAdd()).Select(i => i.Position).Where(previous.ContainsKey).ToHashSet();
        Vector3Int? goal = null;
        if (enemies.Count == 0 && loot.Count > 0) goal = loot.Where(p => p != from).OrderBy(p => distances[p]).Select(p => (Vector3Int?)p).FirstOrDefault();
        if (!goal.HasValue && enemies.Count == 0 && cautiousStairs.HasValue && game.CanExitFloor && previous.ContainsKey(cautiousStairs.Value)) goal = cautiousStairs;
        bool Frontier(Vector3Int p) => Steps.Any(d => p.x+d.x >= 0 && p.y+d.y >= 0 && p.x+d.x < dungeon.dungeonWidth && p.y+d.y < dungeon.dungeonHeight && !cautiousKnown.Contains(p+d));
        if (!goal.HasValue && enemies.Count == 0) goal = previous.Keys.Where(p => !cautiousSurveyed.Contains(p) && Frontier(p))
            .OrderBy(p => distances[p]).ThenBy(p => p.y).ThenBy(p => p.x).Select(p => (Vector3Int?)p).FirstOrDefault();
        if (!goal.HasValue && enemies.Count > 0) goal = previous.Keys.Where(p => p != from && enemies.Any(e => TileWorldDungeon.ChevDistance(p,e.TilemapPosition) <= 1))
            .OrderBy(p => IncomingDamage(game,ally,p,enemies)).ThenBy(p => (p-from).sqrMagnitude).Select(p => (Vector3Int?)p).FirstOrDefault();
        if (!goal.HasValue)
        {
            var propRoute = cautiousProps.Where(p => p.Value != null && p.Value.Alive).SelectMany(p => Steps.Select(d => p.Key+d)
                .Where(previous.ContainsKey).Where(p2 => GridMovement.CanStep(p2,p.Key,cell => cell == p.Key || cautiousWalkable.Contains(cell))).Select(p2 => (prop:p.Value, stand:p2)))
                .OrderBy(p => distances[p.stand]).FirstOrDefault();
            if (propRoute.prop != null)
            {
                if (propRoute.stand != from) goal = propRoute.stand;
                else
                {
                    var prop = propRoute.prop; ally.SetFacingByTargetPosition(prop.Position);
                    bool open = prop.Definition.Kind == EternalEnigma.Core.World.DungeonSceneryKind.Container || prop.IsDoor && SmallKeys.Count > 0;
                    Act(ally,open ? new InteractAction(prop) : new AttackAction(ally,from,prop.Position),open ? "Open discovered container or door" : "Bash a discovered obstruction"); return;
                }
            }
            var mimic = NearbyMimicInteraction(game,ally);
            if (mimic != null) { Act(ally,mimic,"Inspect adjacent chest"); return; }
            if (!goal.HasValue) goal = previous.Keys.Where(p => !cautiousSurveyed.Contains(p)).OrderBy(p => distances[p]).Select(p => (Vector3Int?)p).FirstOrDefault();
            if (!goal.HasValue) { Finish("Unsupported", "Cautious exploration has no known route or supported interaction; this is not a balance defeat."); return; }
        }
        var nextCell = goal.Value;
        while (previous[nextCell] != from && previous[nextCell] != nextCell) nextCell = previous[nextCell];
        if (game.Finale?.OnOverworld == true && !game.Finale.Session.CanStep(from.ToGridPoint(),nextCell.ToGridPoint()))
        {
            var warp = game.Finale.Session.WarpDestinations(from.ToGridPoint()).FirstOrDefault(w => w.Destination.ToCell() == nextCell);
            var portal = dungeon.GetInteractable(from) as FinalePortal;
            if (portal == null || warp.RouteId == null) { Finish("Unsupported","Known warp route is no longer available."); return; }
            finaleChoice = "warp/" + warp.RouteId; CaptureFinale(game.Finale.Session);
            BalanceDecision("Warp",finaleChoice,"Use an available destination from a discovered warp.",true,actor:ally);
            Act(ally,new InteractAction(portal),"Open discovered warp"); return;
        }
        MoveCautiously(ally,nextCell,loot.Contains(goal.Value) ? "Collect reachable visible supplies" : goal == cautiousStairs ? "Travel to discovered stairs" : "Explore a known frontier",incoming);
    }

    bool TryCautiousRecovery(Game game, Ally ally, float incoming)
    {
        var items = game.PlayerController.Inventory.InventoryItems;
        foreach (var item in items.OrderBy(i => RecoveryAmount(i.ItemDefinition,0)).ToArray())
        {
            if (item.ItemDefinition.ItemEffectDefinition is not ModifyStatsItemEffectDefinition e) continue;
            bool heal = e.VitalModification.Hp > 0 && ally.Vitals.HP < Math.Max(incoming * 2 + 1, ally.FinalStats.HPMax * .4f) && ally.FinalStats.HPMax-ally.Vitals.HP >= e.VitalModification.Hp*.6f;
            bool food = e.VitalModification.Hunger > 0 && ally.Vitals.Hunger < ally.FinalStats.HungerMax * .3f;
            bool mana = e.VitalModification.Mana > 0 && ally.Vitals.Mana < AllySkillBudget.ManaReserve(ally.Skills,ally.AllyStrategy) && ally.FinalStats.ManaMax-ally.Vitals.Mana >= e.VitalModification.Mana*.6f;
            if (!heal && !food && !mana) continue;
            var action = new UseInventoryItemAction(game.PlayerController.Inventory,ally,item);
            if (!action.IsValid(ally)) continue;
            BalanceDecision("UseItem",item.ItemName,heal ? "Restore health before a dangerous exchange." : food ? "Maintain food reserve." : "Restore emergency Mana.",true,actor:ally,incoming:incoming);
            Report.ItemsUsed++; Act(ally,action,"Cautious use " + item.ItemName); return true;
        }
        return false;
    }

    void MoveCautiously(Ally ally, Vector3Int next, string reason, float incoming)
    {
        var other = Game.Instance.Allies.FirstOrDefault(a => a != ally && a.TilemapPosition == next);
        ally.SetFacingByTargetPosition(next);
        BalanceDecision("Move",next.ToString(),reason,true,actor:ally,incoming:incoming);
        Act(ally,other == null ? new MovementAction(ally,ally.TilemapPosition,next) : new SwapAllyPositionAction(ally,other),reason);
    }
}
