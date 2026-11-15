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
            Assert.That(Common.Instance.MessageDialog.PromptText.text,Does.Contain(npc.Definition.DisplayName));
            Assert.That(Common.Instance.MessageDialog.PromptText.text,Does.Not.Contain(npc.Definition.Greeting));
            Assert.That(Common.Instance.MessageDialog.PortraitFrame.activeSelf,Is.False);
            Common.Instance.MessageDialog.Ok_Clicked();Assert.That(player.Gold,Is.EqualTo(gold));
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
            Assert.That(town.Townsfolk.Single(n => n.Definition.Id == "Cat").Definition.DialogueTopics, Is.Empty);
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
