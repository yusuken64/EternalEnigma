using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class AutoplayRunner
{
    public bool IsBalance => Options?.Balance != null;
    BalanceExperimentScope balanceScope;
    BalanceStage balanceStage;
    readonly HashSet<string> balanceDowned = new();
    int balanceDamage, balanceHealing, balanceItems, balanceTurns;
    string balanceStageKey;
    StreamWriter balanceDecisions;

    void InitializeBalance()
    {
        if (IsBalance) balanceScope = new BalanceExperimentScope(Options.Balance);
    }

    TownAlly BalanceHero()
    {
        if (!IsBalance || string.IsNullOrEmpty(Options.Balance.HeroId)) return null;
        return TownSceneLoader.Default.AllyCatalog.FirstOrDefault(a => a.Id == Options.Balance.HeroId) ??
            throw new ArgumentException("Unknown balance protagonist: " + Options.Balance.HeroId);
    }

    void BeginBalanceRun()
    {
        balanceDecisions?.Dispose(); balanceDecisions = null;
        balanceStage = null; balanceStageKey = null; balanceDowned.Clear();
        ResetCautiousFloor(); cautiousRestTown = null;
        if (!IsBalance) return;
        var o = Options.Balance;
        Report.Policy = BalanceExperimentOptions.PolicyVersion;
        Report.Observation = new BalanceRunReport().Observation;
        Report.ContentId = o.ContentId;
        Report.Balance = new BalanceRunReport { ExperimentId = o.ExperimentId, CandidateId = o.CandidateId,
            ContentId = o.ContentId, Cohort = o.Cohort, HeroId = o.HeroId };
    }

    List<BalanceActorSnapshot> BalanceParty(Game game) => PartyRules.PartyMembers(game).Where(a => a != null).Select(a =>
        new BalanceActorSnapshot { Id = ActorId(a), Class = HeroClass.Label(a.PrimaryClass, a.SecondaryClass),
            Level = a.Vitals.Level, HP = a.Vitals.HP, MaxHP = a.FinalStats.HPMax, Mana = a.Vitals.Mana, MaxMana = a.FinalStats.ManaMax,
            Hunger = a.Vitals.Hunger, MaxHunger = a.FinalStats.HungerMax,
            Equipment = a.Equipment.GetEquippedItems().Select(i => i.ItemName).ToArray(),
            Skills = a.Skills.Select(s => s.SkillName + ":" + s.Rank).ToArray() }).ToList();

    void BalanceCheckpoint()
    {
        if (!IsBalance || Report?.Balance == null || !startedCampaign) return;
        var c = Common.Instance?.CampaignContext;
        var game = Game.Instance;
        if (c == null || Common.Instance.Travel.IsTransitioning || game != null && !game.IsReady) return;
        string key = game == null ? null : c.State.PendingDungeon + "/" + game.PlayerController.Floor + "/" + game.Finale?.Session.CurrentFloor + "/" + game.CurrentDungeon.GetInstanceID();
        if (key != balanceStageKey)
        {
            if (balanceStage != null && balanceStage.Outcome == "Running")
                balanceStage.Outcome = c.Completed.Contains(balanceStage.Location) || game != null ? "Cleared" : "Returned";
            balanceStage = null; balanceStageKey = key;
            if (game != null)
            {
                var location = c.Campaign.Locations.FirstOrDefault(l => l.Id == c.State.PendingDungeon);
                balanceStage = new BalanceStage { Location = c.State.PendingDungeon, Kind = location?.Kind.ToString() ?? "Dungeon",
                    GenerationFingerprint = EternalEnigma.Core.Generation.DungeonFloorFingerprint.Compute(game.CurrentDungeon.Floor),
                    Tier = location?.Tier ?? 0, Floor = game.PlayerController.Floor, EntryAction = Report.Actions,
                    EntryGold = game.PlayerController.Gold, EntryParty = BalanceParty(game),
                    Visit = Report.Balance.Stages.Count(s => s.Location == c.State.PendingDungeon && s.Floor == game.PlayerController.Floor) + 1 };
                Report.Balance.Stages.Add(balanceStage);
                balanceDamage = Report.DamageTaken; balanceHealing = Report.HealingReceived; balanceItems = Report.ItemsUsed;
                balanceTurns = Report.Balance.ActualTurns; balanceDowned.Clear();
                ResetCautiousFloor();
            }
        }
        if (game == null || balanceStage == null) return;
        balanceStage.ExitAction = Report.Actions; balanceStage.ExitGold = game.PlayerController.Gold;
        balanceStage.ExitParty = BalanceParty(game);
        balanceStage.Turns = Report.Balance.ActualTurns - balanceTurns;
        balanceStage.DamageTaken = Report.DamageTaken - balanceDamage;
        balanceStage.HealingReceived = Report.HealingReceived - balanceHealing;
        balanceStage.ItemUses = Report.ItemsUsed - balanceItems;
        balanceStage.DiscoveredCells = cautiousKnown.Count;
        foreach (var member in balanceStage.ExitParty)
        {
            if (member.MaxHP > 0) balanceStage.MinimumHealthFraction = Math.Min(balanceStage.MinimumHealthFraction, (float)member.HP / member.MaxHP);
            if (member.HP <= 0 && balanceDowned.Add(member.Id)) balanceStage.Downings++;
            if (member.HP > 0) balanceDowned.Remove(member.Id);
        }
    }

    void EndBalanceRun(string outcome)
    {
        if (!IsBalance) return;
        BalanceCheckpoint();
        if (balanceStage?.Outcome == "Running") balanceStage.Outcome = outcome;
        balanceDecisions?.Dispose(); balanceDecisions = null;
    }

    BalancePurchaseObservation BalancePurchase(string name)
    {
        var row = Report.Balance.Purchases.FirstOrDefault(p => p.Item == name);
        if (row == null) Report.Balance.Purchases.Add(row = new BalancePurchaseObservation { Item = name });
        return row;
    }

    void ObserveBalanceOffer(TownShopOffer offer, string reason)
    {
        var row = BalancePurchase(offer.Item.ItemName); row.Evaluations++;
        if (reason == "Not enough gold.") row.Unaffordable++;
        row.LastReason = reason ?? "Available";
    }

    void BalanceCastEvent(Ally ally, Skill skill, string kind)
    {
        if (!IsBalance) return;
        var row = Report.Balance.Abilities.FirstOrDefault(a => a.ActorId == ActorId(ally) && a.Skill == skill.SkillName && a.Rank == skill.Rank);
        if (row == null) Report.Balance.Abilities.Add(row = new BalanceAbilityObservation { ActorId = ActorId(ally), Skill = skill.SkillName, Rank = skill.Rank });
        if (kind == "CastStarted") { row.Started++; row.ManaSpent += skill.ManaCost; }
        if (kind == "CastCompleted") row.Releases++;
        if (kind == "CastInterrupted") row.Interrupted++;
    }

    void BalanceDecision(string kind, string name, string reason, bool chosen = false, float value = 0,
        int cost = 0, Ally actor = null, float incoming = 0, int rank = 0)
    {
        if (!IsBalance || !Running) return;
        Directory.CreateDirectory(DirectoryPath);
        balanceDecisions ??= new StreamWriter(Path.Combine(DirectoryPath, "decisions.jsonl"), append: true) { AutoFlush = true };
        var game = Game.Instance; var c = Common.Instance?.CampaignContext;
        var entry = new BalanceDecision { Kind = kind, Name = name, Reason = reason, Chosen = chosen,
            Value = value, Cost = cost, ActorId = actor != null ? ActorId(actor) : null, Mana = actor?.Vitals.Mana ?? 0,
            Action = Report.Actions, Location = c?.State.PendingDungeon, Floor = game?.PlayerController?.Floor ?? 0,
            Gold = game?.PlayerController?.Gold ?? Common.Instance?.GameSaveData?.TownSaveData?.Gold ?? 0,
            IncomingDamage = incoming, Rank = rank };
        balanceDecisions.WriteLine(JsonUtility.ToJson(entry)); Report.Balance.Decisions++;
    }

    public static void BalanceTurnStarted()
    {
        if (Active?.IsBalance == true && Active.Running) Active.Report.Balance.ActualTurns++;
    }

    internal static void BeginBalanceAction(GameAction action, Character actor)
    {
        if (Active?.IsBalance != true || !Active.Running) return;
        if (action is SkillAction cast && cast.Skill != null)
            action.BalanceSource = new BalanceActionSource { ActorId=ActorId(actor), Skill=cast.Skill.SkillName, Rank=cast.Skill.Rank, RootAction="Skill" };
        action.BalanceSource ??= new BalanceActionSource { ActorId=ActorId(actor), Item=(action as UseInventoryItemAction)?.BalanceItemName, RootAction=action.GetType().Name };
    }

    internal static void BalanceMetrics(GameAction action, Character target, int hp, int mana, int hunger)
    {
        var run=Active;
        if (run?.IsBalance != true || !run.Running || target == null) return;
        int hpDelta=target.Vitals.HP-hp, manaDelta=target.Vitals.Mana-mana, foodDelta=target.Vitals.Hunger-hunger;
        if (hpDelta==0 && manaDelta==0 && foodDelta==0) return;
        var source=action.BalanceSource ?? new BalanceActionSource { RootAction=action.GetType().Name };
        string team=target.Team.ToString();
        var row=run.Report.Balance.Effects.FirstOrDefault(e=>e.ActorId==source.ActorId && e.Skill==source.Skill && e.Item==source.Item &&
            e.Rank==source.Rank && e.RootAction==source.RootAction && e.TargetTeam==team);
        if(row==null) run.Report.Balance.Effects.Add(row=new BalanceEffectTotal {ActorId=source.ActorId,Skill=source.Skill,Item=source.Item,Rank=source.Rank,RootAction=source.RootAction,TargetTeam=team});
        row.HPRemoved+=Math.Max(0,-hpDelta);row.HPRestored+=Math.Max(0,hpDelta);
        row.ManaRemoved+=Math.Max(0,-manaDelta);row.ManaRestored+=Math.Max(0,manaDelta);row.HungerRestored+=Math.Max(0,foodDelta);row.Changes++;
    }
}
