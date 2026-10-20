using System.Collections;
using System.Linq;
using EternalEnigma.Core.Progression;
using UnityEngine;

/// <summary>Optional practice uses the same targeting, costs and actions as the adventure.</summary>
public sealed class OpeningDungeonLesson : MonoBehaviour
{
    private Game game;
    private Character practiceTarget, firstOpponent;
    private Skill skill;
    private bool pendingOpponent, opponentStarted;
    private float showResultUntil;
    private CampaignContext Context => Common.InstanceOrNull?.CampaignContext;
    private Ally Hero => game.Allies.FirstOrDefault(a => a != null && a.TownAllyId == Common.Instance.GameSaveData.ProtagonistId);

    public string Objective
    {
        get
        {
            if (Context == null) return "";
            if (game.PlayerController.Floor == 1 && !Context.State.PractisedOpeningSkill && skill != null)
            {
                // Resources and equipment can change while the player is here.
                // Recheck the actual skill so obtaining supplies immediately restores practice.
                var trainee = Hero;
                if (trainee != null && !trainee.CanCast(skill, out var reason))
                    return "Practice needs: " + reason + ". You can continue downstairs.";
                if (practiceTarget == null && skill.TargetSelector.Team == TargetTeam.Enemies)
                    return "Training target finished. Practise on your next opponent, or continue downstairs.";
                string action = skill.TargetSelector.Team == TargetTeam.Allies ? "Help the friendly slime with " : "Practise ";
                return $"{action}{skill.DisplayName} ({skill.ManaCost} Mana).\n{InputPrompts.Skills}: abilities. Stairs: continue.";
            }
            if (pendingOpponent) return "Ability used! Watch its effect and Mana cost. A single opponent is next.";
            var hero = Hero;
            if (!Context.State.UsedOpeningRecovery && hero != null && RecoveryItem(hero) is { } item)
                return $"Recovery: open Items ({InputPrompts.Inventory}), select {item.DisplayName}, then Use.";
            if (opponentStarted && firstOpponent != null && firstOpponent.Vitals.HP > 0)
                return $"One opponent: face it and press {InputPrompts.Interact} to attack, or use an ability.";
            return Context.OpeningObjective;
        }
    }

    internal static IEnumerator Prepare(Game game)
    {
        var context = Common.InstanceOrNull?.CampaignContext;
        if (context?.HasOpeningLesson != true || context.State.PendingDungeon != "story-0" || context.Completed.Contains("story-0")) yield break;
        var lesson = game.GetComponent<OpeningDungeonLesson>() ?? game.gameObject.AddComponent<OpeningDungeonLesson>();
        lesson.game = game; lesson.practiceTarget = lesson.firstOpponent = null;
        lesson.pendingOpponent = lesson.opponentStarted = false;
        var hero = lesson.Hero;
        if (game.PlayerController.Floor != 1 || hero == null || context.State.PractisedOpeningSkill) yield break;
        lesson.skill = hero.Skills.FirstOrDefault(s => s.SkillName == context.State.OpeningSkillId && s.ActivationType == ActivationType.Active)
            ?? hero.Skills.FirstOrDefault(s => s.ActivationType == ActivationType.Active);
        if (lesson.skill == null) yield break;
        // A friendly patient makes healing useful without injuring the player's party.
        bool friendly = lesson.skill.TargetSelector.Team == TargetTeam.Allies;
        lesson.practiceTarget = lesson.SpawnSlime(hero, friendly ? "Friendly training slime" : "Training slime");
        if (lesson.practiceTarget != null)
        {
            lesson.practiceTarget.Team = friendly ? hero.Team : Team.Enemy;
            lesson.practiceTarget.BaseStats.DropRate = 0; lesson.practiceTarget.BaseStats.EXPOnKill = 0;
            lesson.practiceTarget.BaseStats.HPMax = 60; lesson.practiceTarget.InvalidateCachedStats();
            lesson.practiceTarget.Vitals.HP = friendly ? 20 : 60; lesson.practiceTarget.SyncDisplayedStats();
        }
        GameMessages.Post(friendly ? "This friendly training slime needs healing. Practise your ability safely; it will never attack. You may also continue downstairs."
            : "The training slime will never attack. Practise your new ability here, or continue downstairs when you are ready.");
        OpeningGuidance.Ensure(game);
        yield return null;
    }

    private Character SpawnSlime(Ally hero, string name)
    {
        var prefab = game.EnemyManager.EnemyPrefabs?.FirstOrDefault(e => e != null && e.name == "Enemy_Slime")
            ?? game.EnemyManager.SpawnDefinitions?.Select(d => d.EnemyCharacterPrefab)
                .FirstOrDefault(e => e != null && e.name == "Enemy_Slime");
        if (prefab == null) return null;
        var cell = SkillCastOptions.Directions.Select(d => hero.TilemapPosition + d)
            .Where(p => game.CurrentDungeon.IsWalkable(p) && !game.AllCharacters.Any(c => c != null && c.TilemapPosition == p) &&
                game.CurrentDungeon.CanWalkTo(hero.TilemapPosition, p)).Select(p => (Vector3Int?)p).FirstOrDefault();
        if (!cell.HasValue) return null;
        var enemy = Instantiate(prefab, game.transform); enemy.CharacterName = name; enemy.DisplayName = name;
        enemy.InitialzeVitalsFromStats(); enemy.SetPosition(cell.Value); enemy.InitializeSpawn(); enemy.IsDormant = false;
        game.Enemies.Add(enemy); hero.SetFacing(Character.GetFacing(cell.Value - hero.TilemapPosition));
        return enemy;
    }

    internal bool IsPracticeTarget(Character target) => target != null && target == practiceTarget;

    internal static void SkillReleased(Character actor, Skill released, int effectCount, bool useful)
    {
        var lesson = Game.Instance?.GetComponent<OpeningDungeonLesson>();
        if (lesson == null || lesson.Context?.State.PractisedOpeningSkill != false || actor != lesson.Hero ||
            released != lesson.skill || effectCount == 0 || !useful) return;
        lesson.Context.State.PractisedOpeningSkill = true;
        lesson.pendingOpponent = lesson.game.PlayerController.Floor == 1;
        lesson.showResultUntil = Time.unscaledTime + 1.5f;
        GameMessages.Post($"{released.DisplayName} used! It costs {released.ManaCost} Mana. Watch the target and your Mana meter to see the result.");
    }

    internal static void RecoveryUsed(Character actor, InventoryItem item, bool useful)
    {
        var context = Common.InstanceOrNull?.CampaignContext;
        if (!useful || context?.HasOpeningLesson != true || context.State.UsedOpeningRecovery ||
            actor is not Ally hero || hero.TownAllyId != Common.Instance.GameSaveData.ProtagonistId) return;
        context.State.UsedOpeningRecovery = true;
        GameMessages.Post("Recovery used. Check your meters before the next fight; food restores Hunger and healing items restore HP.");
    }

    internal static bool UsefulRecovery(Character hero, InventoryItem item) =>
        item?.ItemDefinition.ItemEffectDefinition is ModifyStatsItemEffectDefinition effect &&
        (effect.VitalModification.Hp > 0 && hero.Vitals.HP < hero.FinalStats.HPMax ||
         effect.VitalModification.Hunger > 0 && hero.Vitals.Hunger < hero.FinalStats.HungerMax);

    private InventoryItem RecoveryItem(Character hero) => game.PlayerController.Inventory.InventoryItems.FirstOrDefault(item =>
        item.ItemDefinition.ItemEffectDefinition is ModifyStatsItemEffectDefinition effect &&
        (effect.VitalModification.Hp > 0 && hero.Vitals.HP <= hero.FinalStats.HPMax * .7f ||
         effect.VitalModification.Hunger > 0 && hero.Vitals.Hunger <= hero.FinalStats.HungerMax * .6f));

    private void Update()
    {
        if (game == null || !game.IsReady || game.TurnManager.IsProcessingTurn || Context?.State.PendingDungeon != "story-0" ||
            Common.Instance.GlobalSettings.IsOpen || MenuManager.Instance?.Opened == true || Common.Instance.Travel.IsTransitioning) return;
        if (pendingOpponent && Time.unscaledTime >= showResultUntil)
        {
            pendingOpponent = false;
            if (practiceTarget != null) { game.Enemies.Remove(practiceTarget); Destroy(practiceTarget.gameObject); practiceTarget = null; }
            if (Hero != null)
            {
                firstOpponent = SpawnSlime(Hero, "Young slime"); opponentStarted = firstOpponent != null;
                GameMessages.Post("Now try one young slime. Face it to attack, or use your ability. The stairs remain available.");
            }
        }
        if (opponentStarted && (firstOpponent == null || firstOpponent.Vitals.HP <= 0))
        {
            opponentStarted = false;
            GameMessages.Post("First opponent defeated! Check your HP and Hunger, then take the stairs when you are ready.");
        }
    }

    internal GameAction AutoplayAction(Ally hero)
    {
        if (hero != Hero || game.PlayerController.Floor != 1 || Context.State.PractisedOpeningSkill || skill == null) return null;
        if (hero.PendingCast != null) return new WaitAction();
        return SkillCastOptions.Enumerate(hero, skill).OrderByDescending(o => o.Target == practiceTarget)
            .Select(o => o.ToAction(hero)).FirstOrDefault(action => action.IsValid(hero));
    }
}
