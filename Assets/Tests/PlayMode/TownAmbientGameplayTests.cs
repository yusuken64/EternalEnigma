using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
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
            var npc=town.Townsfolk[0];var from=npc.Cell+Vector3Int.down;
            Assert.That(town.CanEnter(npc.Cell),Is.False);Assert.That(npc.GetComponent<TownCharacter>(),Is.Null);
            player.ControllingTownAlly.TilemapPosition=from;
            var action=new TownMovement(player,from,npc.Cell);action.ExecuteImmediate();
            Assert.That(player.ControllingTownAlly.TilemapPosition,Is.EqualTo(from));
            int gold=player.Gold;npc.Greet(from);yield return null;
            Assert.That(Common.Instance.MessageDialog.PromptText.text,Does.Contain(npc.Definition.Greeting));
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
    }
}
