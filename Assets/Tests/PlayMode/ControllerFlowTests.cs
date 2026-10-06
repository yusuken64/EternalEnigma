#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using JuicyChickenGames.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)), PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class ControllerFlowTests
    {
        GameTestHarness harness;
        TestInputScope inputScope;
        Gamepad pad;
        bool? oldControl;

        [UnitySetUp] public IEnumerator Setup()
        {
            inputScope = new TestInputScope();
            harness = new GameTestHarness();
            oldControl = DungeonPreferences.FullControlOverride;
            DungeonPreferences.FullControlOverride = false;
            yield return null;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (pad != null) InputSystem.RemoveDevice(pad);
            yield return harness.Cleanup();
            DungeonPreferences.FullControlOverride = oldControl;
            inputScope.Dispose();
        }

        void ConnectPad()
        {
            // Connect after scene startup, exercising automatic device switching.
            pad = InputSystem.AddDevice<Gamepad>();
            MenuUIInputModule.Active.actionsAsset.devices = new InputDevice[] { pad };
        }

        IEnumerator Press(GamepadButton button) => Send(new GamepadState().WithButton(button));
        IEnumerator Send(GamepadState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
        }

        [UnityTest] public IEnumerator WaitingAlliesBlockMovementAndRecruitFromAdjacentFacingInteraction()
        {
            yield return harness.LoadTown(new TestScenario { Gold=10000 }.CreateSave());
            var town=Object.FindFirstObjectByType<Town>();
            var player=town.TownPlayer;
            var recruit=town.TownAllies.First();
            var from=new[]{Vector3Int.down,Vector3Int.left,Vector3Int.right,Vector3Int.up}.Select(d=>recruit.TilemapPosition+d).First(town.CanEnter);
            player.ControllingTownAlly.TilemapPosition=from;
            player.ControllingTownAlly.transform.position=town.WalkableMap.CellToWorld(from);
            player.ControllingTownAlly.SetFacing(Character.GetFacing(recruit.TilemapPosition-from));
            Assert.That(town.CanEnter(recruit.TilemapPosition),Is.False);
            new TownMovement(player,from,recruit.TilemapPosition).ExecuteImmediate();
            Assert.That(player.ControllingTownAlly.TilemapPosition,Is.EqualTo(from));
            ConnectPad();
            yield return Press(GamepadButton.South);
            var manager=Object.FindFirstObjectByType<TownMenuManager>();
            var dialog=manager.CurrentDialog as AllyRecruitDialog;
            Assert.That(dialog,Is.Not.Null);
            Assert.That(dialog.AllyRecruitDialogMode,Is.EqualTo(AllyRecruitDialogMode.Recruit));
            yield return Press(GamepadButton.East);
            Assert.That(player.ControllingTownAlly.TilemapPosition,Is.EqualTo(from));
            Assert.That(town.Services.Recruit(recruit,out _),Is.True);
            yield return Press(GamepadButton.South);
            dialog=manager.CurrentDialog as AllyRecruitDialog;
            Assert.That(dialog,Is.Not.Null);
            Assert.That(dialog.AllyRecruitDialogMode,Is.EqualTo(AllyRecruitDialogMode.Talk));
        }

        [UnityTest] public IEnumerator MainMenuHeroPickerAndBackUseControllerOnly()
        {
            yield return harness.LoadMainMenu(null);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>().IsReady, "menu ready");
            ConnectPad();
            var menu = Object.FindFirstObjectByType<MainMenu>();
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.StartButton));
            yield return Press(GamepadButton.South);
            Assert.That(Object.FindFirstObjectByType<ProtagonistHeroPicker>(), Is.Not.Null);
            var first = EventSystem.current.currentSelectedGameObject;
            yield return Press(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(first));
            yield return Send(new GamepadState { leftStick = Vector2.left });
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first));
            yield return Press(GamepadButton.East);
            Assert.That(Object.FindFirstObjectByType<ProtagonistHeroPicker>(), Is.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(menu.StartButton));
            yield return Press(GamepadButton.DpadDown);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(menu.StartButton));
            yield return Press(GamepadButton.South);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            yield return Press(GamepadButton.East);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
        }

        [UnityTest] public IEnumerator TownMovementInventorySkillsAndSettingsUseControllerOnly()
        {
            yield return harness.LoadTown(new TestScenario { Skills = new[] { "Damage" } }.CreateSave());
            ConnectPad();
            var town = Object.FindFirstObjectByType<Town>();
            var player = town.TownPlayer;
            var menus = Object.FindFirstObjectByType<TownMenuManager>();
            foreach (bool stick in new[] { false, true })
            {
                var origin = player.ControllingTownAlly.TilemapPosition;
                var offset = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right }
                    .First(d => player.WalkableMap.CanWalkTo(origin, origin + d) &&
                        !town.TownBuildings.Any(b => b.TilemapPosition == origin + d) &&
                        !town.TownAllies.Any(a => a.TilemapPosition == origin + d) &&
                        !town.ShopVendors.Any(v => v.TilemapPosition == origin + d));
                yield return Send(Direction(offset, stick));
                yield return harness.WaitUntil(() => !player.IsBusy, "town movement");
                Assert.That(player.ControllingTownAlly.TilemapPosition, Is.Not.EqualTo(origin), stick ? "stick" : "dpad");
            }
            yield return Press(GamepadButton.West);
            Assert.That(menus.Opened, Is.True);
            var position = player.ControllingTownAlly.TilemapPosition;
            yield return Press(GamepadButton.DpadDown);
            Assert.That(player.ControllingTownAlly.TilemapPosition, Is.EqualTo(position), "Menus must block walking.");
            yield return Press(GamepadButton.East);
            Assert.That(menus.Opened, Is.False);
            yield return Press(GamepadButton.LeftShoulder);
            Assert.That(menus.CurrentDialog, Is.TypeOf<PartyMenu>());
            Assert.That(menus.PartyMenu.Tab, Is.EqualTo(PartyMenuTab.Skills));
            yield return Press(GamepadButton.South);
            Assert.That(menus.DialogStack.Count, Is.EqualTo(1), "Unsupported town skills remain inspectable without a Cast action.");
            yield return Press(GamepadButton.East);
            Assert.That(menus.Opened, Is.False);
            yield return Press(GamepadButton.Start);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            yield return Press(GamepadButton.East);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            Assert.That(Common.Instance.MenuInputHandler.PlayerInput.currentActionMap.name, Is.EqualTo("Player"));
        }

        static GamepadState Direction(Vector3Int direction, bool stick)
        {
            if (stick) return new GamepadState { leftStick = new Vector2(direction.x, direction.y) };
            return new GamepadState().WithButton(direction.x > 0 ? GamepadButton.DpadRight :
                direction.x < 0 ? GamepadButton.DpadLeft : direction.y > 0 ? GamepadButton.DpadUp : GamepadButton.DpadDown);
        }

        [UnityTest] public IEnumerator TownShopAndTrainerSupportNavigationConfirmAndBack()
        {
            yield return harness.LoadTown(new TestScenario { Gold = 10000 }.CreateSave());
            ConnectPad();
            var town = Object.FindFirstObjectByType<Town>();
            var menu = Object.FindFirstObjectByType<TownMenu>();
            var manager = Object.FindFirstObjectByType<TownMenuManager>();
            var shop = (ShopMenuDialog)menu.OpenBuilding(town.Configuration.Buildings.First(b => b.Id == "shop"), town.TownPlayer, null);
            yield return null; yield return null;
            var first = EventSystem.current.currentSelectedGameObject;
            yield return Press(GamepadButton.DpadDown);
            var row = EventSystem.current.currentSelectedGameObject;
            Assert.That(row, Is.Not.EqualTo(first));
            yield return Press(GamepadButton.South);
            Assert.That(manager.CurrentDialog, Is.TypeOf<BuyConfirmationDialog>());
            yield return Press(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(shop.BuyConfirmationDialog.CancelButton.gameObject));
            yield return Press(GamepadButton.South);
            Assert.That(manager.CurrentDialog, Is.SameAs(shop));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(row));
            yield return Press(GamepadButton.East);
            Assert.That(manager.Opened, Is.False);
            var trainer = (BallistaDialog)menu.OpenBuilding(town.Configuration.Buildings.First(b => b.Id == "trainer"), town.TownPlayer, null);
            yield return null; yield return null;
            first = EventSystem.current.currentSelectedGameObject;
            yield return Press(GamepadButton.DpadRight);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(trainer.Layout.PreviewControl.gameObject));
            yield return Press(GamepadButton.DpadLeft);
            yield return Press(GamepadButton.DpadDown);
            row = EventSystem.current.currentSelectedGameObject;
            Assert.That(row, Is.Not.EqualTo(first));
            Assert.That(row.GetComponentInParent<SkillGridItem>(), Is.Not.Null);
            yield return Press(GamepadButton.South);
            Assert.That(manager.DialogStack.Count, Is.EqualTo(2), "Trainer confirm should show a purchase dialog or explain its requirement.");
            yield return Press(GamepadButton.East);
            Assert.That(manager.CurrentDialog, Is.SameAs(trainer));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(row));
            yield return Press(GamepadButton.East);
            Assert.That(manager.Opened, Is.False);
        }

        [UnityTest] public IEnumerator ControllerCanOpenAndCloseCampaignPartyInTown()
        {
            yield return harness.LoadMainMenu(null);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<MainMenu>().IsReady, "menu ready");
            Object.FindFirstObjectByType<MainMenu>().StartGame();
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true &&
                !Common.Instance.Travel.IsTransitioning, "campaign town");
            ConnectPad();
            yield return null;
            var hud = Object.FindFirstObjectByType<Town>().GetComponent<CampaignHUD>();
            yield return Press(GamepadButton.East);
            Assert.That(hud.IsPartyOpen, Is.True);
            Assert.That(MenuUIInputModule.IsUsable(EventSystem.current.currentSelectedGameObject), Is.True);
            yield return Press(GamepadButton.East);
            Assert.That(hud.IsPartyOpen, Is.False, "Back must close without reopening the party.");
            yield return Press(GamepadButton.East);
            Assert.That(hud.IsPartyOpen, Is.True);
            yield return Press(GamepadButton.South);
            Assert.That(hud.IsPartyOpen, Is.False, "Done must accept controller confirm.");
        }

        [UnityTest] public IEnumerator CombatMovementAttackSkillTargetCancelAndConfirmUseControllerOnly()
        {
            yield return harness.LoadDungeon(new TestScenario { Skills = new[] { "Damage", "Fire Bolt" }, SP = 50 });
            foreach (var enemy in harness.Game.Enemies.ToArray()) Object.Destroy(enemy.gameObject);
            harness.Game.Enemies.Clear();
            yield return null;
            ConnectPad();
            var hero = harness.Ally;
            Assert.That(PlayerInput.all.Count, Is.EqualTo(1), "Dungeon and Common must share one input owner so automatic device switching stays enabled.");
            foreach (bool stick in new[] { false, true })
            {
                var origin = hero.TilemapPosition;
                var offset = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right }
                    .First(d => harness.Game.CurrentDungeon.CanWalkTo(origin, origin + d));
                InputSystem.QueueStateEvent(pad, Direction(offset, stick));
                float deadline = Time.realtimeSinceStartup + 4;
                while (hero.TilemapPosition == origin && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(hero.TilemapPosition, Is.Not.EqualTo(origin),
                    $"Controller movement: stick={stick}, move={PlayerInputHandler.Instance.moveInput}, " +
                    $"scheme={PlayerInputHandler.Instance.PlayerInput.currentControlScheme}, map={PlayerInputHandler.Instance.PlayerInput.currentActionMap?.name}, " +
                    $"waiting={hero.IsWaitingForPlayerInput}, turn={harness.Game.TurnManager.IsProcessingTurn}, " +
                    $"mode={harness.Game.PlayerController.CurrentControlMode}, consumed={MenuUIInputModule.Active.InputConsumed}, " +
                    $"menu={MenuManager.Instance.Opened}, settings={Common.Instance.GlobalSettings.IsOpen}, autoplay={AutoplayRunner.BlocksPlayerInput}");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                yield return harness.WaitForIdle();
                Assert.That(hero.TilemapPosition, Is.Not.EqualTo(origin), stick ? "stick" : "dpad");
            }
            var cell = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right }
                .Select(d => hero.TilemapPosition + d).First(harness.Game.CurrentDungeon.IsWalkable);
            yield return harness.SpawnEnemy("Enemy_Slime", cell);
            var target = (Enemy)harness.Game.Enemies.Single();
            target.Policies.Clear();
            target.BaseStats.HPMax = 5000; target.InvalidateCachedStats(); target.Vitals.HP = 5000;
            hero.SetFacing(System.Enum.GetValues(typeof(Facing)).Cast<Facing>()
                .First(f => Dungeon.GetFacingOffset(f) == cell - hero.TilemapPosition));
            var hp = target.Vitals.HP;
            yield return Press(GamepadButton.South);
            yield return harness.WaitForIdle();
            Assert.That(target.Vitals.HP, Is.LessThan(hp), "South must attack in gameplay.");
            hp=target.Vitals.HP;var aimedOrigin=hero.TilemapPosition;
            yield return Send(new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.South));
            yield return harness.WaitForIdle();
            Assert.That(target.Vitals.HP,Is.LessThan(hp),"Holding target must attack the adjacent cell, not two cells away.");
            Assert.That(hero.TilemapPosition,Is.EqualTo(aimedOrigin),"An aimed attack must not move the hero.");
            yield return Press(GamepadButton.LeftShoulder);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<PartyMenu>());
            var damage = hero.Skills.Single(s => s.SkillName == "Damage");
            Assert.That(hero.CanCast(damage, out var reason), Is.True,
                $"{reason}; hero={hero.TilemapPosition}, target={target.TilemapPosition}, hp={target.Vitals.HP}, " +
                $"visible={harness.Game.CurrentDungeon.CanSee(hero, target)}, enemies={harness.Game.Enemies.Count}");
            var skillMenu = MenuManager.Instance.PartyMenu;
            var damageButton = skillMenu.EntryButtons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("Damage  "));
            for (int i = 0; i < skillMenu.EntryButtons.Count && EventSystem.current.currentSelectedGameObject != damageButton.gameObject; i++)
                yield return Press(GamepadButton.DpadDown);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(damageButton.gameObject));
            yield return Press(GamepadButton.South);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<PartyMenuPicker>());
            yield return Press(GamepadButton.South);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<TargetDialog>());
            int mana = hero.Vitals.SP;
            Assert.That(skillMenu.GetComponent<Canvas>().enabled,Is.False,"World targets must remain visible.");
            yield return Press(GamepadButton.Start);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True);
            yield return Press(GamepadButton.East);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<TargetDialog>());
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.True, "Back first focuses Resume.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Common.Instance.GlobalSettings.ResumeButton.gameObject));
            yield return Press(GamepadButton.East);
            Assert.That(Common.Instance.GlobalSettings.IsOpen, Is.False);
            yield return Press(GamepadButton.East);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<PartyMenuPicker>());
            Assert.That(hero.Vitals.SP, Is.EqualTo(mana), "Cancel must not cast.");
            Assert.That(skillMenu.GetComponent<Canvas>().enabled,Is.False,"Only the current action picker is visible.");
            Assert.That(MenuManager.Instance.CurrentDialog.GetComponent<Canvas>().enabled,Is.True);
            yield return Press(GamepadButton.South);
            hp = target.Vitals.HP;
            yield return Press(GamepadButton.South);
            yield return harness.WaitForIdle();
            Assert.That(MenuManager.Instance.Opened, Is.False);
            Assert.That(target.Vitals.HP, Is.LessThan(hp));
            Assert.That(hero.Vitals.SP, Is.EqualTo(mana - hero.Skills.Single(s => s.SkillName == "Damage").SPCost));

            // Missile abilities aim at cells instead of cycling character targets.
            yield return Press(GamepadButton.LeftShoulder);
            var boltButton = skillMenu.EntryButtons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith("Fire Bolt  "));
            for (int i = 0; i < skillMenu.EntryButtons.Count && EventSystem.current.currentSelectedGameObject != boltButton.gameObject; i++)
                yield return Press(GamepadButton.DpadDown);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(boltButton.gameObject));
            yield return Press(GamepadButton.South);
            yield return Press(GamepadButton.South);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<TargetDialog>());
            yield return Send(new GamepadState { leftStick = Vector2.right });
            Assert.That(MenuManager.Instance.TargetDialog.Direction, Is.EqualTo(Vector3Int.right));
            var aim = target.TilemapPosition - hero.TilemapPosition;
            yield return Send(Direction(aim, false));
            Assert.That(MenuManager.Instance.TargetDialog.CameraTarget, Is.SameAs(target));
            mana = hero.Vitals.SP;
            yield return Press(GamepadButton.South);
            yield return harness.WaitForIdle();
            Assert.That(MenuManager.Instance.Opened, Is.False);
            Assert.That(hero.Vitals.SP, Is.EqualTo(mana - hero.Skills.Single(s => s.SkillName == "Fire Bolt").SPCost));
            yield return Press(GamepadButton.East);
            Assert.That(MenuManager.Instance.CurrentDialog, Is.TypeOf<AllyActionDialog>());
            yield return Press(GamepadButton.East);
            Assert.That(MenuManager.Instance.Opened, Is.False);
        }
    }
}
#endif
