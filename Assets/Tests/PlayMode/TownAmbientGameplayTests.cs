using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap)),PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public sealed class TownAmbientGameplayTests
    {
        GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup(){harness=new GameTestHarness();yield return null;}
        [UnityTearDown] public IEnumerator Cleanup()=>harness.Cleanup();
        private static TownMenuManager Menus => Object.FindFirstObjectByType<TownMenuManager>();
        private static Town World => Object.FindFirstObjectByType<Town>();

        private static void Choose(string label)
        {
            var picker = (PartyMenuPicker)Menus.CurrentDialog;
            var row = picker.Rows.GetComponentsInChildren<AuthoredButton>().Single(r => r.Label.text == label);
            row.Button.Select();
            row.Button.onClick.Invoke();
        }

        private IEnumerator StartCampaign()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => World?.IsReady == true && !common.Travel.IsTransitioning &&
                !common.ScreenTransition.BlocksInput, "guide's opening town");
            harness.UnlockOpeningHome();
        }

        private static void PlaceLeader(Town town, Vector3Int cell)
        {
            var hero = town.TownPlayer.ControllingTownAlly;
            hero.TilemapPosition = cell;
            hero.transform.position = town.WalkableMap.CellToWorld(cell);
            town.RefreshRoofs();
            town.TownPlayer.CameraController.SnapToFollowTarget();
        }

        private static void ClearTownDungeon()
        {
            var context = Common.Instance.CampaignContext;
            context.Keys.Add(EternalEnigma.Core.Progression.CampaignContext.OpeningDayKey);
            Assert.That(context.BeginTownDungeon("story-0"), Is.True);
            Assert.That(context.CompleteDungeon(true), Is.True);
            World.Guide.PlaceGateRecruit();
        }

        [UnityTest]
        public IEnumerator MochiTeachesTheMenuAndAnswersFromTheCurrentTownPartyAndCampaign()
        {
            yield return StartCampaign();
            var town = World;
            var guide = town.Guide;
            var mochi = guide.Mochi;
            var hero = town.TownPlayer.ControllingTownAlly;
            var context = Common.Instance.CampaignContext;
            var stand = town.Plan.CardinalNeighbors(mochi.Cell.ToGridPoint()).Select(p => p.ToCell())
                .First(p => town.CanEnter(p) && !town.Plan.Layers[EternalEnigma.Core.World.TownLayers.ShopFloor].At(p.ToGridPoint()));
            PlaceLeader(town, stand);
            hero.SetFacing(new TownMovement(town.TownPlayer, stand, mochi.Cell).GetFacing(mochi.Cell - stand));
            yield return null; yield return null;
            var objective = town.GetComponentsInChildren<Canvas>(true).Single(c => c.name == "Current objective");
            Assert.That(objective.enabled, Is.True);
            Assert.That(objective.GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain("Talk to Mochi"));
            Assert.That(TerminalSceneSnapshots.Town(town).Context.Last(), Does.Contain(InputPrompts.Attack));
            mochi.Greet(stand);
            yield return null;
            var topics = Menus.CurrentDialog;
            Choose("Where do I go next?");
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain("free loaf"));
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(topics));
            context.State.BoughtOpeningBread = true;
            Choose("Where do I go next?");
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain("trainer"));
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Assert.That(context.IsGridGenerated, Is.False, "Asking about the first errands must keep the overworld lazy.");
            Choose("Where can I recruit someone?");
            yield return null;
            var classes = Menus.CurrentDialog;
            var wanted = town.TownAllies.First(a => a.SecondaryClass != null).SecondaryClass;
            Choose(wanted.DisplayName);
            var named = town.TownAllies.Where(a => a.PrimaryClass == wanted || a.SecondaryClass == wanted)
                .OrderBy(a => a.RecruitCost).First();
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(named.Name));
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(named.RecruitCost + " gold"));
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Assert.That(Menus.CurrentDialog, Is.SameAs(classes));
            Choose("Back to my questions");
            yield return null;
            hero.Hp = 1;
            Choose("What should I do before I leave?");
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain("inn"));
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Choose("That's all, thanks.");
            Assert.That(Common.Instance.GameSaveData.MetMochi, Is.True);
            Assert.That(Menus.Opened, Is.False);
            Assert.That(context.State.LearnedOpeningSkill, Is.False, "Talking is optional and never completes an errand.");
            hero.Hp = -1;
            town.TownPlayer.Gold = named.RecruitCost;
            Assert.That(guide.PreparationHint(), Does.Contain("company"));
            context.State.LearnedOpeningSkill = true;
            Assert.That(guide.Directions(mochi.Cell), Does.Contain("Sleep in your own bed"));
            var bed = town.GetComponentInChildren<HomeBed>();
            town.WriteSaveData();
            Assert.That(CampaignSaving.Commit(Common.Instance, "town-0/home", bed.Tile - Vector3Int.up,
                Facing.Down, out var saveError, beginOpeningDay: true), Is.True, saveError);
            Assert.That(SaveSystem.LoadData().MetMochi, Is.True);
            Assert.That(guide.Directions(mochi.Cell), Does.Contain("clear the town dungeon"));

            ClearTownDungeon();
            var outer = context.Campaign.Routes.Single(r => r.Id == "starter-exit");
            string key = context.Campaign.KeyLabel(outer.KeyId);
            Assert.That(guide.Directions(mochi.Cell), Does.Contain("east"));
            Assert.That(guide.Directions(mochi.Cell), Does.Contain(key));
            Assert.That(guide.Directions(mochi.Cell), Does.Contain("Clear that dungeon"));
            Assert.That(context.IsGridGenerated, Is.False);
            context.Keys.Add(outer.KeyId);
            context.Completed.Add("repeatable-0");
            Assert.That(guide.Directions(mochi.Cell), Does.Contain("Use " + key));
        }

        [UnityTest]
        public IEnumerator MochiOnlyNamesRealClassOffersInVisitedTowns()
        {
            yield return StartCampaign();
            var town = World;
            var context = Common.Instance.CampaignContext;
            var local = town.TownAllies.SelectMany(a => new[] { a.PrimaryClass, a.SecondaryClass }).ToHashSet();
            var hired = context.Roster.Append(Common.Instance.GameSaveData.ProtagonistId);
            var lead = (from place in context.Campaign.Locations
                        where place.Kind == EternalEnigma.Core.Progression.LocationKind.Town && place.Id != town.Configuration.Id
                        from offer in town.TownAllyManager.SelectOffers(context.Town(place.Id).AllySlots.Select(s => s.Roll).ToArray(), hired)
                        from cls in new[] { offer.Ally.PrimaryClass, offer.Ally.SecondaryClass }
                        where cls != null && !local.Contains(cls)
                        select (place, offer, cls)).First();
            string placeName = context.GetTownDisplayName(lead.place.Id);
            Assert.That(town.Guide.RecruitHint(lead.cls), Does.Not.Contain(placeName));
            // Older saves have visited towns but no guide records.
            context.Towns.Add(lead.place.Id);
            var hint = town.Guide.RecruitHint(lead.cls);
            Assert.That(hint, Does.Contain(placeName));
            Assert.That(hint, Does.Contain(lead.offer.Ally.Name));
            Assert.That(hint, Does.Contain(lead.offer.Cost + " gold"));
            context.Roster.Add(lead.offer.Ally.Id);
            Assert.That(town.Guide.RecruitHint(lead.cls), Does.Not.Contain(lead.offer.Ally.Name + " is taking work"));
        }

        [UnityTest]
        public IEnumerator SoloGateInvitationUsesAnExistingRecruitAndTheNormalHiringPrice()
        {
            yield return StartCampaign();
            var town = World;
            var before = town.TownAllies.ToArray();
            var otherCells = before.Skip(1).Select(a => a.TilemapPosition).ToArray();
            ClearTownDungeon();
            var ally = before[0];
            Assert.That(town.TownAllies, Is.EqualTo(before));
            Assert.That(town.TownAllies.Skip(1).Select(a => a.TilemapPosition), Is.EqualTo(otherCells));
            var exit = town.Plan.Exit.ToCell();
            Assert.That((ally.TilemapPosition - exit).sqrMagnitude, Is.InRange(1, 2));
            Assert.That(town.CanEnter(exit), Is.True);
            var from = exit + Vector3Int.up * 4;
            PlaceLeader(town, from);
            town.TownPlayer.SetAction(new TownMovement(town.TownPlayer, from, from + Vector3Int.down));
            yield return harness.WaitUntil(() => Menus.CurrentDialog == Common.Instance.MessageDialog, "gate invitation on approach");
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(ally.Name));
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain("east"));
            Assert.That(Common.Instance.MessageDialog.PortraitFrame.activeSelf, Is.False);
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Assert.That(town.Guide.IntroduceGateSpeaker(), Is.False);
            Assert.That(town.TownPlayer.RecruitedAllies.Count, Is.EqualTo(1));
            Assert.That(town.Guide.TalkToGateSpeaker(ally), Is.True);
            yield return null;
            Choose("Want to come with me?");
            yield return null;
            var hiring = (AllyRecruitDialog)Menus.CurrentDialog;
            town.TownPlayer.Gold = ally.RecruitCost - 1;
            hiring.Recruit_Clicked();
            Assert.That(Menus.CurrentDialog, Is.SameAs(hiring));
            Assert.That(hiring.DescriptionText.text, Does.Contain("Not enough gold"));
            town.TownPlayer.Gold = ally.RecruitCost;
            hiring.Recruit_Clicked();
            Assert.That(town.TownPlayer.Gold, Is.Zero);
            Assert.That(town.TownPlayer.RecruitedAllies, Does.Contain(ally));
            Assert.That(town.TownAllies.Contains(ally), Is.False);
            Assert.That(town.Services.SaveGame(out var error), Is.True, error);
            Assert.That(SaveSystem.LoadData().HeardOpeningGateInvitation, Is.True);
        }

        [UnityTest]
        public IEnumerator EarlyHireSpeaksAtTheGateAndOtherRecruitsKeepTheirPlaces()
        {
            yield return StartCampaign();
            var town = World;
            var ally = town.TownAllies.Last();
            town.TownPlayer.Gold = ally.RecruitCost;
            Assert.That(town.Services.Recruit(ally, out var error), Is.True, error);
            var otherCells = town.TownAllies.Select(a => a.TilemapPosition).ToArray();
            ClearTownDungeon();
            Assert.That(town.TownAllies.Select(a => a.TilemapPosition), Is.EqualTo(otherCells));
            PlaceLeader(town, town.Plan.Exit.ToCell() + Vector3Int.up * 2);
            Assert.That(town.Guide.IntroduceGateSpeaker(), Is.True);
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain(ally.Name));
            Assert.That(Common.Instance.MessageDialog.PromptText.text, Does.Contain("together"));
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Assert.That(town.Guide.TalkToGateSpeaker(ally), Is.True);
            yield return null;
            var rows = ((PartyMenuPicker)Menus.CurrentDialog).Rows.GetComponentsInChildren<AuthoredButton>();
            Assert.That(rows.Select(r => r.Label.text), Does.Contain("Manage party"));
            Assert.That(rows.Select(r => r.Label.text), Does.Not.Contain("Want to come with me?"));
        }

        [UnityTest]
        public IEnumerator TownsfolkGreetBlockPartyMovementAndRegenerateWithoutDuplicates()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town=Object.FindFirstObjectByType<Town>();var player=town.TownPlayer;
            Assert.That(town.Townsfolk.Count,Is.EqualTo(2));Assert.That(town.Townsfolk.Select(n=>n.Definition.Id),Is.Unique);
            var npc=town.Townsfolk.Single(n=>n.Definition.Id=="Cat");var from=npc.Cell+Vector3Int.down;
            Assert.That(town.CanEnter(npc.Cell),Is.False);Assert.That(npc.GetComponent<TownCharacter>(),Is.Null);
            player.ControllingTownAlly.TilemapPosition=from;
            var action=new TownMovement(player,from,npc.Cell);action.ExecuteImmediate();
            Assert.That(player.ControllingTownAlly.TilemapPosition,Is.EqualTo(from));
            int gold=player.Gold;npc.Greet(from);yield return null;
            Choose("Where do I go next?");
            Assert.That(Common.Instance.MessageDialog.PromptText.text,Does.Contain(npc.Definition.DisplayName));
            Assert.That(Common.Instance.MessageDialog.PromptText.text,Does.Not.Contain(npc.Definition.Greeting));
            Assert.That(Common.Instance.MessageDialog.PortraitFrame.activeSelf,Is.False);
            Common.Instance.MessageDialog.Ok_Clicked();
            yield return null;
            Choose("That's all, thanks.");
            Assert.That(player.Gold,Is.EqualTo(gold));
            var cells=town.Townsfolk.Select(n=>n.Cell).ToArray();
            TownInteriorRendering.SpawnTownsfolk(town);TownInteriorRendering.Build(town.WalkableMap.TileWorldCreator);yield return null;
            Assert.That(town.GetComponentsInChildren<TownNpc>().Length,Is.EqualTo(2));Assert.That(town.Townsfolk.Select(n=>n.Cell),Is.EqualTo(cells));
            Assert.That(town.WalkableMap.TileWorldCreator.worldObject.GetComponentInChildren<TownInteriorOutput>().Birds.Count,Is.EqualTo(3));
            Assert.That(town.ShopVendors.All(v=>v.Building.VendorPrefab!=null),Is.True);
            int vendors=town.ShopVendors.Count,party=player.RecruitedAllies.Count;
            var former=player.ControllingTownAlly;
            town.WalkableMap.TileWorldCreator.ExecuteAllBuildLayers(true);
            yield return harness.WaitUntil(()=>town.IsReady && player.ControllingTownAlly!=former,"full furnished town rebuild");
            Assert.That(player.RecruitedAllies.Count,Is.EqualTo(party));Assert.That(player.Gold,Is.EqualTo(gold));
            Assert.That(town.ShopVendors.Count,Is.EqualTo(vendors));Assert.That(town.GetComponentsInChildren<TownNpc>().Length,Is.EqualTo(2));
            Assert.That(town.Townsfolk.Select(n=>n.Cell),Is.EqualTo(cells));
            town.WriteSaveData();SaveSystem.SaveData(Common.Instance.GameSaveData);
            var configuration=town.Configuration;var saved=SaveSystem.LoadData();
            yield return harness.Cleanup();harness=new GameTestHarness();
            yield return harness.LoadTown(saved,configuration);
            town=Object.FindFirstObjectByType<Town>();
            Assert.That(town.TownPlayer.Gold,Is.EqualTo(gold));Assert.That(town.Townsfolk.Select(n=>n.Cell),Is.EqualTo(cells));
            Assert.That(town.ShopVendors.Count,Is.EqualTo(vendors));
        }

        [UnityTest]
        public IEnumerator RowanOffersTopicsAndAnswersReturnToTheSelectedQuestion()
        {
            yield return harness.LoadTown(new TestScenario().CreateSave());
            var town = Object.FindFirstObjectByType<Town>();
            var manager = Object.FindFirstObjectByType<TownMenuManager>();
            var rowan = town.Townsfolk.Single(n => n.Definition.Id == "Dog");
            Assert.That(rowan.Definition.DialogueTopics.Length, Is.EqualTo(6));
            Assert.That(rowan.Definition.DialogueTopics.Select(t => t.Topic), Is.Unique);
            rowan.Greet(rowan.Cell + Vector3Int.down);
            yield return null;
            Assert.That(manager.CurrentDialog, Is.TypeOf<PartyMenuPicker>());
            var picker = (PartyMenuPicker)manager.CurrentDialog;
            Assert.That(picker.Title.text, Is.EqualTo("What do you want to know?"));
            var rows = picker.Rows.GetComponentsInChildren<AuthoredButton>();
            Assert.That(rows.Length, Is.EqualTo(rowan.Definition.DialogueTopics.Length + 1));
            foreach (var topic in rowan.Definition.DialogueTopics)
            {
                var row = rows.Single(r => r.Label.text == topic.Topic);
                row.Button.Select();
                row.Button.onClick.Invoke();
                yield return null;
                var answer = Common.Instance.MessageDialog;
                Assert.That(manager.CurrentDialog, Is.SameAs(answer));
                Assert.That(manager.DialogStack.Count, Is.EqualTo(2));
                Assert.That(answer.PromptText.text, Is.EqualTo(GameplayText.Format(rowan.Definition.DisplayName + "\n\n" + topic.Answer)));
                Assert.That(answer.PortraitFrame.activeSelf, Is.False);
                Assert.That(answer.PromptText.text, Does.Not.Contain(rowan.Definition.Greeting));
                answer.Ok_Clicked();
                yield return null;
                Assert.That(manager.CurrentDialog, Is.SameAs(picker));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(row.gameObject));
            }
            rows.Single(r => r.Label.text == "That's all, thanks.").Button.onClick.Invoke();
            yield return null;
            Assert.That(manager.Opened, Is.False);
            rowan.Greet(rowan.Cell + Vector3Int.down);
            yield return null;
            picker = (PartyMenuPicker)manager.CurrentDialog;
            Assert.That(picker.Rows.GetComponentsInChildren<AuthoredButton>().Length, Is.EqualTo(rows.Length));
            picker.BackButton.onClick.Invoke();
            yield return null;
            Assert.That(manager.Opened, Is.False);
        }
    }
}
