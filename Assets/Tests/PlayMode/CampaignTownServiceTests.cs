using System.Collections;
using System.Linq;
using EternalEnigma.Core.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EternalEnigma.Tests
{
    [PrebuildSetup(typeof(HarnessSceneBootstrap))]
    [PostBuildCleanup(typeof(HarnessSceneBootstrap))]
    public class CampaignTownServiceTests
    {
        private GameTestHarness harness;
        [UnitySetUp] public IEnumerator Setup() { harness = new GameTestHarness { TimeoutSeconds = 120 }; yield return null; }
        [UnityTearDown] public IEnumerator Cleanup() => harness.Cleanup();

        [UnityTest]
        public IEnumerator DetailedTownExposesServicesAndInnRestoresItsCheckpoint()
        {
            yield return harness.LoadMainMenu(null);
            var common = Common.Instance;
            common.GameSaveData = Object.FindFirstObjectByType<MainMenu>().CreateNewSave(42);
            common.Travel.NewCampaign(42);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>()?.IsReady == true, "detailed campaign town");
            var town = Object.FindFirstObjectByType<Town>();
            Assert.That(town.Roofs.Count, Is.EqualTo(town.Plan.BuildingSlots.Count));
            Assert.That(town.Roofs.Select(r => r.Door), Is.EquivalentTo(town.Plan.BuildingSlots));
            Assert.That(town.Plan.Width, Is.EqualTo(common.CampaignContext.TownLayout("town-0").Options.Width));
            Assert.That(town.TownPlayer.ControllingTownAlly.TilemapPosition, Is.EqualTo(Object.FindFirstObjectByType<HomeBed>().Tile - Vector3Int.up));
            foreach (var building in town.TownBuildings)
            {
                var door = building.TilemapPosition;
                var footprint = town.Plan.Footprints.Single(f => f.Door.Equals(door.ToGridPoint()));
                var visualCell = door + (footprint.Cells.Count > 0 ? Vector3Int.up : Vector3Int.zero);
                Assert.That(building.transform.position, Is.EqualTo(town.WalkableMap.CellToWorld(visualCell)), building.Definition.Id);
                Assert.That(town.Plan.IsWalkable(door.ToGridPoint()), Is.True, building.Definition.Id);
                if (footprint.Cells.Count > 0 && !building.Definition.HasInterior)
                    Assert.That(town.Plan.IsWalkable(visualCell.ToGridPoint()), Is.False, building.Definition.Id);
            }
            foreach (var service in TownServiceCatalog.All)
            {
                var building = town.TownBuildings.Single(b => b.Definition.Id == service.Id);
                Assert.That(building.HasInterior, Is.True, service.Id);
                var vendor = town.ShopVendors.Single(v => v.Building == building.Definition);
                Assert.That(town.Plan.TryGetVendorAnchor(building.TilemapPosition.ToGridPoint(), out var anchor), Is.True);
                Assert.That(vendor.TilemapPosition, Is.EqualTo(anchor.ToCell()));
            }
            var inn = town.TownBuildings.Single(b => b.Definition.Id == "inn");
            var menu = Object.FindFirstObjectByType<TownMenu>();
            var hero = town.TownPlayer.ControllingTownAlly;
            hero.Hp = 1; hero.Sp = 0;
            var dialog = (InnDialog)menu.OpenBuilding(inn.Definition, town.TownPlayer, null);
            dialog.RestButton.onClick.Invoke();
            Assert.That(hero.Hp, Is.EqualTo(-1)); Assert.That(hero.Sp, Is.EqualTo(-1));
            common.MessageDialog.Ok_Clicked();
            town.TownPlayer.Gold = 345;
            dialog = (InnDialog)menu.OpenBuilding(inn.Definition, town.TownPlayer, null);
            dialog.SaveButton.onClick.Invoke();
            Assert.That(InnCheckpoint.Exists(SaveSystem.LoadData()), Is.True);
            common.MessageDialog.Ok_Clicked();
            town.TownPlayer.Gold = 1; town.SaveProgress();
            Assert.That(InnCheckpoint.TryRestore(common), Is.True);
            yield return harness.WaitUntil(() => Object.FindFirstObjectByType<Town>() != town && Object.FindFirstObjectByType<Town>()?.IsReady == true, "inn checkpoint town");
            var restored = Object.FindFirstObjectByType<Town>();
            Assert.That(restored.TownPlayer.Gold, Is.EqualTo(345));
            Assert.That(restored.Roofs.Count, Is.EqualTo(restored.Plan.BuildingSlots.Count));
            Assert.That(restored.Roofs.All(r => r != null), Is.True);
            Assert.That(common.CampaignContext.IsGridGenerated, Is.False);
        }
    }
}
