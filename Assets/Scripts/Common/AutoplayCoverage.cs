using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Attached only to actions selected in an autoplay session. A charging action and its
// release share one trace, so selection, resolution and interruption cannot count twice.
internal sealed class AutoplayCastTrace
{
    private readonly Action<string,string> record;
    private bool ended, started;
    internal AutoplayCastTrace(Action<string,string> record) { this.record = record; }
    internal void Start() { if (started || ended) return; started = true; record("CastStarted",null); }
    internal void Complete() { if (ended) return; ended = true; record("CastCompleted",null); }
    internal void Interrupt(string reason) { if (ended) return; ended = true; record("CastInterrupted",reason); }
}

internal static class AutoplayCoverage
{
    internal static AllySkillChoice Choose(AllySkillContext context, IReadOnlyList<IAllySkillEvaluator> evaluators,
        Func<Skill,int> completed, Func<Skill,int> selected)
    {
        // Survival takes precedence even if these abilities have already been exercised.
        foreach (var evaluator in evaluators.Where(e => e is ReviveEvaluator or EmergencyHealEvaluator or CureEvaluator))
        {
            if (context.SearchFirst && evaluator is CureEvaluator) continue;
            var urgent = evaluator.Evaluate(context);
            if (urgent?.ToAction(context.Ally)?.IsValid(context.Ally) == true) return urgent;
        }
        if (context.SearchFirst) return null;
        var choices = new List<(AllySkillChoice choice,int priority)>();
        foreach (var skill in context.Castable)
        {
            var subset = context.ForSkill(skill);
            for (int i=0;i<evaluators.Count;i++)
            {
                var evaluator = evaluators[i];
                var choice = evaluator is DamageEvaluator damage ? damage.EvaluateCoverage(subset) : evaluator?.Evaluate(subset);
                if (choice?.ToAction(context.Ally)?.IsValid(context.Ally) == true) choices.Add((choice,i));
            }
        }
        return choices.OrderBy(c => completed(c.choice.Option.Skill) > 0 ? 1 : 0)
            .ThenBy(c => completed(c.choice.Option.Skill) > 0 ? 0 : selected(c.choice.Option.Skill))
            .ThenBy(c => c.priority).ThenByDescending(c => c.choice.Score).Select(c => c.choice).FirstOrDefault();
    }
}

public sealed partial class AutoplayRunner
{
    private readonly List<AutoplayCastTrace> castTraces = new();
    internal static string ActorId(Character actor) => actor is Ally ally && !string.IsNullOrEmpty(ally.TownAllyId)
        ? ally.TownAllyId : actor == null ? "" : "instance:" + actor.GetInstanceID();

    private AutoplayAbilityCoverage Ability(string actorId, string skill, int rank, bool active)
    {
        var row = Report.Abilities.FirstOrDefault(a => a.ActorId == actorId && a.Skill == skill);
        if (row == null) Report.Abilities.Add(row = new AutoplayAbilityCoverage { ActorId = actorId, Skill = skill });
        row.Rank = Math.Max(row.Rank,rank); row.Active = active;
        return row;
    }

    private void ObserveAbilities(Game game)
    {
        foreach (var ally in PartyRules.PartyMembers(game).Where(a => a != null))
            foreach (var skill in ally.Skills.Where(s => s != null))
            {
                var row = Ability(ActorId(ally),skill.SkillName,skill.Rank,skill.ActivationType == ActivationType.Active);
                if (!row.Active || row.Completed > 0) continue;
                string reason = !ally.CanCast(skill,out var rejection) ? rejection :
                    SkillIntents.Classify(skill).Has(SkillIntent.Never) ? "Excluded by safety/utility policy" :
                    "No context-appropriate completed cast observed";
                if (row.Selected == 0) row.Reason = reason;
                Skip("ability/"+row.ActorId+"/"+row.Skill,reason,row.ActorId,row.Skill);
            }
    }

    internal AllySkillChoice ChooseCoverage(AllySkillContext context, IReadOnlyList<IAllySkillEvaluator> evaluators)
    {
        int Count(Skill skill, bool completed)
        {
            var row = Report.Abilities.FirstOrDefault(a => a.ActorId == ActorId(context.Ally) && a.Skill == skill.SkillName);
            return row == null ? 0 : completed ? row.Completed : row.Selected;
        }
        return AutoplayCoverage.Choose(context,evaluators,s => Count(s,true),s => Count(s,false));
    }

    internal void TrackCast(Ally ally, AllySkillChoice choice, SkillAction action)
    {
        if (!Running || PlayerControlled || ally == null || choice == null || action == null) return;
        string actor = ActorId(ally), skill = choice.Option.Skill.SkillName;
        var row = Ability(actor,skill,choice.Option.Skill.Rank,true);
        var use = Report.SkillUses.FirstOrDefault(u => u.ActorId == actor && u.Skill == skill && u.Evaluator == choice.Evaluator);
        if (use == null) Report.SkillUses.Add(use = new AutoplaySkillUse {
            Actor = ally.CharacterName, ActorId = actor, Skill = skill, Evaluator = choice.Evaluator });
        use.Selected++; row.Selected++;
        string id = Report.RunId + "/cast-" + Report.SkillUses.Sum(u => u.Selected);
        void Record(string kind, string reason)
        {
            if (kind == "CastStarted") use.Started++;
            if (kind == "CastCompleted") { use.Casts++; row.Completed++; row.Reason = null; }
            if (kind == "CastInterrupted") { use.Interrupted++; row.Interrupted++; row.Reason = reason; }
            WriteEvent(new AutoplayEvent { Kind = kind, CastId = id, ActorId = actor, Skill = skill,
                TargetId = ActorId(choice.Option.Target), Evaluator = choice.Evaluator, Message = reason ?? choice.Reason,
                Cost = choice.Option.Skill.SPCost, Currency = "SP", Success = kind == "CastCompleted",
                Position = ally != null ? ally.TilemapPosition.ToString() : null });
        }
        Record("CastSelected",choice.Reason);
        AutoplayCastTrace trace = null;
        trace = new AutoplayCastTrace((kind,reason) => {
            Record(kind,reason);
            if (kind is "CastCompleted" or "CastInterrupted") castTraces.Remove(trace);
        });
        castTraces.Add(trace); action.AutoplayTrace = trace;
    }
}
