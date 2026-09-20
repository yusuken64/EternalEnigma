#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class SkillRegressionTests
    {
        private GameTestHarness harness;
        private TestInputScope inputScope;
        private Gamepad pad;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<Skill> skills = new();
        private readonly List<Object> itemAssets = new();
        private Ally caster => harness.Ally;
        private Ally friend;
        private Character first, second, distant;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            yield return harness.LoadDungeon(new TestScenario { AdditionalAllies = new[] { "Reese" } });
            friend = harness.Game.Allies.Single(a => a != caster);
            var dungeon = harness.Game.CurrentDungeon;
            var cells = Enumerable.Range(0, dungeon.dungeonWidth).SelectMany(x =>
                Enumerable.Range(0, dungeon.dungeonHeight).Select(y => new Vector3Int(x, y)))
                .Where(dungeon.IsWalkable).ToList();
            var center = cells.First(p => Enumerable.Range(-1, 3).All(x =>
                Enumerable.Range(-1, 3).All(y => dungeon.IsWalkable(p + new Vector3Int(x, y)))));
            caster.SetPosition(center);
            friend.SetPosition(center + Vector3Int.left);
            friend.AllyStrategy = AllyStrategy.HoldPosition;
            var positions = new[] { center + Vector3Int.right, center + Vector3Int.right + Vector3Int.up,
                cells.First(p => TileWorldDungeon.ChevDistance(p, center) > 3) };
            foreach (var position in positions) yield return harness.SpawnEnemy("Enemy_Slime", position);
            first = harness.Game.Enemies[0]; second = harness.Game.Enemies[1]; distant = harness.Game.Enemies[2];
            foreach (var enemy in harness.Game.Enemies.Cast<Enemy>()) enemy.Policies.Clear();
            foreach (var actor in harness.Game.AllCharacters)
            {
                actor.BaseStats.HPMax = actor.BaseStats.SPMax = 100;
                actor.BaseStats.HPRegenAcccumlateThreshold = actor.BaseStats.SPRegenAcccumlateThreshold = 10000;
                actor.InvalidateCachedStats();
                actor.Vitals.HP = 60; actor.Vitals.SP = 20;
                actor.SyncDisplayedStats();
            }
            harness.Game.UpdateMiniMap();
            pad = InputSystem.AddDevice<Gamepad>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad, keyboard, mouse };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            try { yield return harness.Cleanup(); }
            finally
            {
                foreach (var skill in skills) Object.DestroyImmediate(skill);
                skills.Clear();
                foreach (var asset in itemAssets) Object.DestroyImmediate(asset);
                itemAssets.Clear();
                inputScope.Dispose();
            }
        }

        private Skill Learn(string name, Character actor = null)
        {
            var skill = Common.Instance.SkillManager.GetSkillInstanceByName(name);
            Assert.That(skill, Is.Not.Null, name);
            skills.Add(skill);
            (actor ?? caster).Skills.Add(skill);
            (actor ?? caster).InvalidateCachedStats();
            (actor ?? caster).SyncDisplayedStats();
            return skill;
        }

        private IEnumerator Cast(Skill skill, Character target)
        {
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new SkillAction(caster, skill, target));
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissileSkillAimsDiagonallyCancelsAndHitsOnlyFirstCharacter()
        {
            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.Missile;
            skill.TargetSelector.Area = TargetArea.All;
            yield return Press(Key.R);
            yield return Press(Key.Enter);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow, Key.RightArrow));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(MenuManager.Instance.TargetDialog.Direction, Is.EqualTo(new Vector3Int(1, 1)));
            Assert.That(MenuManager.Instance.TargetDialog.CameraTarget, Is.SameAs(second));
            yield return Press(Key.Escape);
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
            yield return Press(Key.Enter);
            yield return Press(GamepadButton.DpadRight);
            Assert.That(MenuManager.Instance.TargetDialog.Direction, Is.EqualTo(Vector3Int.right));
            friend.SetAction(new WaitAction());
            yield return Press(GamepadButton.South);
            yield return harness.WaitForIdle();
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
            Assert.That(second.Vitals.HP, Is.EqualTo(60));
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator MissileRangeWallsFriendlyBlockersAndEmptyShotsAreRespected()
        {
            var skill = Learn("Damage");
            skill.Targeting = SkillTargeting.Missile;
            skill.TargetSelector.Area = TargetArea.All;
            Assert.That(MissileTargeting.Trace(caster, Vector3Int.left, 8).Character, Is.SameAs(friend));
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, skill, Vector3Int.left));
            Assert.That(friend.Vitals.HP, Is.EqualTo(60), "Team filter prevents damage, but the ally blocks the shot.");
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            Assert.That(MissileTargeting.Trace(caster, Vector3Int.up, 1).Cell, Is.EqualTo(caster.TilemapPosition + Vector3Int.up));
            var hit = MissileTargeting.Trace(caster, Vector3Int.up, 1000);
            Assert.That(hit.Character, Is.Null);
            Assert.That(harness.Game.CurrentDungeon.IsWalkable(hit.Cell), Is.True);
            Assert.That(harness.Game.CurrentDungeon.IsWalkable(hit.Cell + Vector3Int.up), Is.False);
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(SkillAction.ForMissile(caster, skill, Vector3Int.up));
            Assert.That(caster.Vitals.SP, Is.EqualTo(18), "A confirmed miss still spends energy.");
            Assert.That(SkillAction.ForMissile(caster, skill, Vector3Int.zero).IsValid(caster), Is.False);
            skill.MissileRange = 0;
            Assert.That(caster.CanCast(skill, out _), Is.False);
        }

        private InventoryItem HealingItem(SkillTargeting targeting, TargetTeam team)
        {
            var definition = ScriptableObject.CreateInstance<UsableItemDefinition>();
            var effect = ScriptableObject.CreateInstance<ModifyStatsItemEffectDefinition>();
            itemAssets.Add(definition); itemAssets.Add(effect);
            definition.ItemName = "Targeted potion";
            definition.Targeting = targeting;
            definition.TargetSelector = new TargetSelector { Team = team, Area = TargetArea.All };
            definition.ItemEffectDefinition = effect;
            definition.StackMax = 10;
            effect.StatModification = new StatModification();
            effect.VitalModification = new VitalModification { Hp = 10 };
            var item = definition.AsInventoryItem(3);
            harness.Game.PlayerController.Inventory.Add(item);
            return item;
        }

        [UnityTest]
        public IEnumerator ItemSelectedAllyWorkflowCancelsThenHealsWithoutSpCost()
        {
            var item = HealingItem(SkillTargeting.SelectedTarget, TargetTeam.Allies);
            yield return Press(Key.Q);
            yield return Press(Key.Enter);
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.True);
            yield return Press(Key.Escape);
            Assert.That(item.StackStock, Is.EqualTo(3));
            yield return Press(Key.Enter);
            yield return Press(Key.LeftArrow);
            Assert.That(MenuManager.Instance.TargetDialog.CameraTarget, Is.SameAs(friend));
            friend.SetAction(new WaitAction());
            yield return Press(Key.Enter);
            yield return harness.WaitForIdle();
            Assert.That(friend.Vitals.HP, Is.EqualTo(70));
            Assert.That(caster.Vitals.HP, Is.EqualTo(60));
            Assert.That(item.StackStock, Is.EqualTo(2));
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator ItemAreaAllTargetsAndSelfConsumeOncePerUse()
        {
            var item = HealingItem(SkillTargeting.SelectedTarget, TargetTeam.Allies);
            var definition = (UsableItemDefinition)item.ItemDefinition;
            definition.AreaRadius = 1;
            var inventory = harness.Game.PlayerController.Inventory;
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new UseInventoryItemAction(inventory, caster, item).WithTarget(caster));
            Assert.That(caster.Vitals.HP, Is.EqualTo(70));
            Assert.That(friend.Vitals.HP, Is.EqualTo(70));
            Assert.That(item.StackStock, Is.EqualTo(2));
            definition.Targeting = SkillTargeting.AllTargets;
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new UseInventoryItemAction(inventory, caster, item));
            Assert.That(caster.Vitals.HP, Is.EqualTo(80));
            Assert.That(friend.Vitals.HP, Is.EqualTo(80));
            definition.Targeting = SkillTargeting.Self; definition.AreaRadius = 0;
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new UseInventoryItemAction(inventory, caster, item));
            Assert.That(caster.Vitals.HP, Is.EqualTo(90));
            Assert.That(friend.Vitals.HP, Is.EqualTo(80));
            Assert.That(inventory.InventoryItems, Has.No.Member(item));
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator MissileItemUsesChosenDirectionAndConsumesOneArrow()
        {
            var definition = AssetDatabase.LoadAssetAtPath<UsableItemDefinition>("Assets/Prefabs/Dungeon/Items/Arrows_WoodenArrows.asset");
            Assert.That(definition.Targeting, Is.EqualTo(SkillTargeting.Missile));
            Assert.That(definition.MissileProjectilePrefab, Is.Not.Null);
            var item = definition.AsInventoryItem(3);
            harness.Game.PlayerController.Inventory.Add(item);
            caster.CurrentFacing = Facing.Left;
            yield return Press(Key.Q);
            yield return Press(Key.Enter);
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.TargetDialog.Direction, Is.EqualTo(Vector3Int.left));
            yield return Press(Key.RightArrow);
            friend.SetAction(new WaitAction());
            yield return Press(Key.Enter);
            yield return harness.WaitForIdle();
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
            Assert.That(friend.Vitals.HP, Is.EqualTo(60));
            Assert.That(item.StackStock, Is.EqualTo(2));
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator SelectorsRespectTeamsRangeLifeAndActualTarget()
        {
            var selector = new TargetSelector { Team = TargetTeam.Allies, Area = TargetArea.All };
            Assert.That(selector.GetCharacters(caster), Is.EquivalentTo(new Character[] { caster, friend }));
            selector.Team = TargetTeam.Enemies;
            Assert.That(selector.GetCharacters(caster), Is.EquivalentTo(new[] { first, second, distant }));
            Assert.That(selector.GetCharacters(first), Is.EquivalentTo(new Character[] { caster, friend }));
            selector.Team = TargetTeam.Self;
            Assert.That(selector.GetCharacters(caster), Is.EqualTo(new[] { caster }));
            selector.Team = TargetTeam.All;
            Assert.That(selector.GetCharacters(caster).Count, Is.EqualTo(5));
            first.Vitals.HP = 0;
            Assert.That(selector.GetCharacters(caster), Has.No.Member(first));
            first.Vitals.HP = 60;
            var skill = Learn("ShieldBash");
            Assert.That(skill.GetTargetCharacters(caster), Has.Member(first));
            Assert.That(skill.GetTargetCharacters(caster), Has.No.Member(distant));
            Assert.That(new SkillAction(caster, skill, distant).IsValid(caster), Is.False);
            Assert.That(new SkillAction(caster, skill, friend).IsValid(caster), Is.False);
            Assert.That(new SkillAction(caster, skill, null).IsValid(caster), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingleDamageHealingAndSelfEffectsChargeExactlyOnce()
        {
            var damage = Learn("Damage");
            yield return Cast(damage, first);
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
            Assert.That(second.Vitals.HP, Is.EqualTo(60));
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            var healing = Learn("Healing");
            yield return Cast(healing, friend);
            Assert.That(friend.Vitals.HP, Is.EqualTo(70));
            friend.Vitals.HP = 98; friend.SyncDisplayedStats();
            yield return Cast(healing, friend);
            Assert.That(friend.Vitals.HP, Is.EqualTo(100));
            yield return Cast(healing, caster);
            Assert.That(caster.Vitals.HP, Is.EqualTo(70));
            Assert.That(caster.Vitals.SP, Is.EqualTo(16));
            Assert.That(caster.DisplayedVitals.SP, Is.EqualTo(16));
        }

        [UnityTest]
        public IEnumerator AreaAndUntargetedCastsFilterRecipientsAndPayOneCost()
        {
            var skill = Learn("Damage");
            skill.TargetSelector.Area = TargetArea.All;
            skill.AreaRadius = 1;
            yield return Cast(skill, first);
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
            Assert.That(second.Vitals.HP, Is.EqualTo(55));
            Assert.That(distant.Vitals.HP, Is.EqualTo(60));
            Assert.That(caster.Vitals.HP, Is.EqualTo(60));
            Assert.That(friend.Vitals.HP, Is.EqualTo(60));
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            skill.Targeting = SkillTargeting.AllTargets;
            yield return Cast(skill, null);
            Assert.That(first.Vitals.HP, Is.EqualTo(50));
            Assert.That(second.Vitals.HP, Is.EqualTo(50));
            Assert.That(distant.Vitals.HP, Is.EqualTo(55));
            Assert.That(caster.Vitals.SP, Is.EqualTo(18));
            var heal = Learn("Healing");
            heal.Targeting = SkillTargeting.Self; heal.AreaRadius = 1;
            yield return Cast(heal, null);
            Assert.That(caster.Vitals.HP, Is.EqualTo(70));
            Assert.That(friend.Vitals.HP, Is.EqualTo(70));
            Assert.That(first.Vitals.HP, Is.EqualTo(50));
        }

        [UnityTest]
        public IEnumerator RequirementsRejectInvalidCastsWithoutSpendingEnergyOrATurn()
        {
            var skill = Learn("Damage");
            caster.Vitals.SP = 0; caster.SyncDisplayedStats();
            Assert.That(caster.CanCast(skill, out _), Is.False);
            caster.SetAction(new SkillAction(caster, skill, first));
            Assert.That(harness.Game.TurnManager.IsProcessingTurn, Is.False);
            Assert.That(first.Vitals.HP, Is.EqualTo(60));
            caster.Vitals.SP = 1; caster.SyncDisplayedStats();
            Assert.That(caster.CanCast(skill, out _), Is.True);
            yield return Cast(skill, first);
            Assert.That(caster.Vitals.SP, Is.Zero);
            caster.Vitals.SP = 20; caster.SyncDisplayedStats();
            var passive = Learn("Strength Up");
            Assert.That(caster.CanCast(passive, out _), Is.False);
            var silence = Learn("ShieldBash").ActionEffects.OfType<ApplyStatusEffectAction>().Single().StatusEffect;
            var instance = caster.ApplyStatusEffect(silence);
            Assert.That(caster.CanCast(skill, out _), Is.False);
            Assert.That(new SkillAction(caster, skill, first).ExecuteImmediate(caster), Is.Empty);
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
            caster.RemoveStatusEffect(instance); Object.Destroy(instance.gameObject);
            caster.Skills.Remove(skill);
            Assert.That(caster.CanCast(skill, out _), Is.False);
            caster.Skills.Add(skill);
            var queued = new SkillAction(caster, skill, first);
            Assert.That(queued.IsValid(caster), Is.True);
            first.Vitals.HP = 0;
            Assert.That(queued.ExecuteImmediate(caster), Is.Empty);
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
            first.Vitals.HP = 60;
            skill.SPCost = 0;
            caster.Vitals.SP = 0; caster.SyncDisplayedStats();
            Assert.That(caster.CanCast(skill, out _), Is.True);
            yield return Cast(skill, first);
            Assert.That(caster.Vitals.SP, Is.Zero);
            skill.SPCost = -1;
            Assert.That(caster.CanCast(skill, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator StatusTypesStackIndependentlyAndExpiredEffectsDoNotTickAgain()
        {
            var dot = Learn("Dot");
            var hot = Learn("Hot");
            hot.TargetSelector.Team = TargetTeam.Enemies;
            var silence = Learn("ShieldBash");
            yield return Cast(dot, first);
            var dotInstance = first.StatusEffects.OfType<DotStatusEffect>().Single();
            Assert.That(first.Vitals.HP, Is.EqualTo(60 - dotInstance.TickDamage));
            int dotTurns = dotInstance.TurnsLeft;
            yield return Cast(hot, first);
            Assert.That(first.StatusEffects.OfType<HotStatusEffect>().Count(), Is.EqualTo(1));
            Assert.That(dotInstance.TurnsLeft, Is.EqualTo(dotTurns - 1));
            yield return Cast(silence, first);
            Assert.That(first.StatusEffects.OfType<SilenceStatusEffect>().Count(), Is.EqualTo(1));
            int oldTurns = dotInstance.TurnsLeft;
            yield return Cast(dot, first);
            Assert.That(first.StatusEffects.OfType<DotStatusEffect>().Count(), Is.EqualTo(1));
            Assert.That(dotInstance.TurnsLeft, Is.GreaterThan(oldTurns));
            dotInstance.TurnsLeft = 0;
            var remaining = first.StatusEffects.OfType<HotStatusEffect>().Single();
            int hp = first.Vitals.HP;
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());
            Assert.That(first.StatusEffects.OfType<DotStatusEffect>(), Is.Empty);
            Assert.That(first.StatusEffects, Has.Member(remaining));
            Assert.That(first.Vitals.HP, Is.EqualTo(hp + remaining.TickDamage));
            remaining.TurnsLeft = 1;
            friend.SetAction(new WaitAction());
            yield return harness.ExecuteAction(new WaitAction());
            Assert.That(first.StatusEffects.OfType<HotStatusEffect>(), Is.Empty, "Remove after the final tick.");
        }

        [UnityTest]
        public IEnumerator GamepadOpensSelectsCancelsReopensAndConfirmsSkill()
        {
            Learn("Damage"); Learn("Strength Up");
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
            yield return Press(GamepadButton.LeftShoulder);
            Assert.That(harness.Game.SkillDialog.Buttons.Count, Is.EqualTo(1), "Passive skills cannot be invoked.");
            yield return Press(GamepadButton.South);
            var dialog = MenuManager.Instance.TargetDialog;
            Assert.That(dialog.enabled, Is.True);
            Assert.That(dialog.CameraTarget, Is.SameAs(first));
            yield return Press(GamepadButton.DpadUp);
            Assert.That(dialog.CameraTarget, Is.SameAs(second));
            yield return Press(GamepadButton.Start);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            yield return Press(GamepadButton.Start);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(dialog.CameraTarget, Is.SameAs(second));
            yield return Press(GamepadButton.East);
            Assert.That(EventSystem.current.enabled, Is.True);
            Assert.That(dialog.TargetIndicator.activeSelf, Is.False);
            Assert.That(dialog.enabled, Is.False);
            Assert.That(dialog.SelectTargetPrompt.activeSelf, Is.False);
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
            yield return Press(GamepadButton.South);
            friend.SetAction(new WaitAction());
            yield return Press(GamepadButton.South);
            yield return harness.WaitForIdle();
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            Assert.That(MenuManager.Instance.Opened, Is.False);
            Assert.That(EventSystem.current.enabled, Is.True);
            Assert.That(dialog.enabled, Is.False);
            Assert.That(harness.Game.PlayerController.CurrentControlMode, Is.EqualTo(PlayerControlMode.FollowAlly));
        }

        [UnityTest]
        public IEnumerator KeyboardInvokesSelfAndUntargetedSkillsAndClosesTargeting()
        {
            var anger = Learn("Anger");
            yield return Press(Key.R);
            friend.SetAction(new WaitAction());
            yield return Press(Key.Enter);
            yield return harness.WaitForIdle();
            Assert.That(caster.StatusEffects.OfType<StrengthStatusEffect>().Count(), Is.EqualTo(1));
            Assert.That(caster.FinalStats.Strength, Is.EqualTo(caster.BaseStats.Strength + 5));
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
            Assert.That(MenuManager.Instance.Opened, Is.False);
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            caster.Skills.Remove(anger);
            var damage = Learn("Damage"); damage.Targeting = SkillTargeting.AllTargets;
            damage.TargetSelector.Area = TargetArea.All;
            yield return Press(Key.R);
            friend.SetAction(new WaitAction());
            yield return Press(Key.Space);
            yield return harness.WaitForIdle();
            Assert.That(harness.Game.Enemies.All(e => e.Vitals.HP == 55), Is.True);
            Assert.That(caster.Vitals.SP, Is.EqualTo(18));
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
            damage.Targeting = SkillTargeting.SelectedTarget;
            yield return Press(Key.R);
            yield return Press(Key.Enter);
            yield return Press(Key.Escape);
            Assert.That(EventSystem.current.enabled, Is.True);
            Assert.That(MenuManager.Instance.Opened, Is.True);
            yield return Press(Key.R);
            Assert.That(MenuManager.Instance.Opened, Is.False);
        }

        [UnityTest]
        public IEnumerator AllyMenuQueuesSkillForTheChosenAlly()
        {
            Learn("Healing", friend);
            MenuManager.Instance.OpenAllyMenu(friend);
            yield return null;
            MenuManager.Instance.AllyActionDialog.Skill_Clicked();
            yield return null;
            yield return Press(GamepadButton.South);
            yield return Press(GamepadButton.South);
            yield return harness.ExecuteAction(new WaitAction());
            Assert.That(caster.Vitals.HP, Is.EqualTo(70));
            Assert.That(friend.Vitals.SP, Is.EqualTo(19));
            Assert.That(caster.Vitals.SP, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator MouseInvokesAreaSkillAndInvalidMenuCastsKeepTheirEnergy()
        {
            var skill = Learn("Damage"); skill.AreaRadius = 1;
            caster.Vitals.SP = 0; caster.SyncDisplayedStats();
            yield return Press(Key.R);
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.SameAs(harness.Game.SkillDialog));
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
            Assert.That(first.Vitals.HP, Is.EqualTo(60));
            caster.Vitals.SP = 20; caster.SyncDisplayedStats();
            var button = harness.Game.SkillDialog.Buttons[0].Button;
            var canvas = button.GetComponentInParent<Canvas>();
            var position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.True);
            friend.SetAction(new WaitAction());
            yield return Press(Key.Enter);
            yield return harness.WaitForIdle();
            Assert.That(first.Vitals.HP, Is.EqualTo(55));
            Assert.That(second.Vitals.HP, Is.EqualTo(55));
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            foreach (var enemy in harness.Game.Enemies) enemy.Vitals.HP = 0;
            yield return Press(Key.R);
            yield return Press(Key.Enter);
            Assert.That(MenuManager.Instance.TargetDialog.enabled, Is.False);
            Assert.That(caster.Vitals.SP, Is.EqualTo(19));
            yield return Press(Key.Escape);
            Assert.That(MenuManager.Instance.Opened, Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyCasterUsesItsOwnTeamAndEnergyAndCannotCastWhileAsleep()
        {
            var skill = Learn("Damage", first);
            var action = new SkillAction(first, skill, caster);
            Assert.That(action.IsValid(first), Is.True);
            var effects = first.ExecuteActionImmediate(action);
            foreach (var effect in effects) first.ExecuteActionImmediate(effect);
            yield return first.ExecuteActionRoutine(action);
            foreach (var effect in effects) yield return first.ExecuteActionRoutine(effect);
            Assert.That(first.Vitals.SP, Is.EqualTo(19));
            Assert.That(caster.Vitals.HP, Is.EqualTo(55));
            Assert.That(new SkillAction(first, skill, second).IsValid(first), Is.False);
            var sleep = AssetDatabase.LoadAssetAtPath<SleepStatusEffect>("Assets/Prefabs/Dungeon/StatusEffects/SleepStatus.prefab");
            Assert.That(sleep, Is.Not.Null);
            first.ApplyStatusEffect(sleep);
            Assert.That(first.CanCast(skill, out _), Is.False);
            Assert.That(new SkillAction(first, skill, caster).ExecuteImmediate(first), Is.Empty);
            Assert.That(first.Vitals.SP, Is.EqualTo(19));
        }
    }
}
#endif
