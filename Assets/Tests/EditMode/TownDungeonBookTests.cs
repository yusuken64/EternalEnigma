using System.Collections.Generic;
using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public sealed class TownDungeonBookTests
    {
        [Test, Timeout(600000)]
        public void HundredCampaignsPlaceBookOnReachableResidentialTableWithoutChangingTown()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
                var plan = TownPlanGenerator.Generate(context.TownLayout(context.BookDungeon.ParentTownId).Options);
                var before = plan.Layers.ToDictionary(p => p.Key, p => p.Value.ToArray());
                var home = plan.Interiors.Single(i => i.Spec.Kind == TownInteriorKind.Home);
                var tutorialBook = home.Props.Single(p => p.Asset == "Lectern").Cell;
                Assert.That(TownDungeonBook.TryFindTable(plan, out var table), Is.True, "Seed " + seed);
                Assert.That(TownDungeonBook.TryFindTable(plan, out var repeat), Is.True);
                Assert.That(repeat, Is.SameAs(table));
                var interior = plan.Interiors.Single(i => i.Props.Contains(table));
                Assert.That(interior.Spec.Kind, Is.EqualTo(TownInteriorKind.Residential));
                Assert.That(interior.Door, Is.EqualTo(plan.Interiors.Where(i => i.Spec.Kind == TownInteriorKind.Residential)
                    .OrderBy(i => i.Door.Y).ThenBy(i => i.Door.X).First().Door));
                Assert.That(table.Cell, Is.Not.EqualTo(tutorialBook));
                Assert.That(plan.IsWalkable(table.Cell), Is.False, "The furniture remains occupied.");
                var reachable = new HashSet<GridPoint>(GridSearch.VisitOrder(plan.PartySpawn,plan.CardinalNeighbors));
                Assert.That(plan.CardinalNeighbors(table.Cell).Any(reachable.Contains), Is.True);
                foreach (var layer in plan.Layers) Assert.That(layer.Value.ToArray(), Is.EqualTo(before[layer.Key]));
            }
        }

        [Test]
        public void LegacyResidentialHomeIsExcludedWithoutChangingSavedGeometry()
        {
            for (int seed = 0; seed < 24; seed++)
            {
                var context = new CampaignContext(new OverworldLaunchOptions(OverworldLaunchMode.Campaign, seed));
                context.State.TownLayoutVersion = 0;
                var layout = context.TownLayout(context.BookDungeon.ParentTownId);
                var plan = TownPlanGenerator.Generate(layout.Options);
                var configuration = Object.Instantiate(TownSceneLoader.Default);
                configuration.Id = context.BookDungeon.ParentTownId;
                try
                {
                    CampaignTownLayout.Configure(configuration, layout);
                    var home = configuration.SlotBuildings.Select((building,index) => (building,index))
                        .Single(pair => pair.building?.Id == "home");
                    var homeDoor = plan.BuildingSlots[home.index];
                    Assert.That(plan.Interiors.Single(i => i.Door.Equals(homeDoor)).Spec.Kind,
                        Is.EqualTo(TownInteriorKind.Residential));
                    var before = plan.Layers.ToDictionary(p => p.Key,p => p.Value.ToArray());
                    Assert.That(TownDungeonBook.TryFindTable(plan,out var table,configuration),Is.True);
                    var chosen = plan.Interiors.Single(i => i.Props.Contains(table));
                    Assert.That(chosen.Door,Is.Not.EqualTo(homeDoor));
                    Assert.That(chosen.Door,Is.EqualTo(plan.Interiors
                        .Where(i => i.Spec.Kind == TownInteriorKind.Residential && !i.Door.Equals(homeDoor))
                        .OrderBy(i => i.Door.Y).ThenBy(i => i.Door.X).First().Door));
                    foreach(var layer in plan.Layers) Assert.That(layer.Value.ToArray(),Is.EqualTo(before[layer.Key]));
                }
                finally
                {
                    foreach (var home in configuration.Buildings.Where(b => b.Id == "home")) Object.DestroyImmediate(home);
                    Object.DestroyImmediate(configuration);
                }
            }
        }

        [Test]
        public void BookPromptReusesCampaignCardAndHasNoSaveActions()
        {
            var prompt = Resources.Load<BookDungeonPrompt>("UI/BookDungeonPrompt");
            Assert.That(prompt, Is.Not.Null);
            Assert.That(prompt.Card, Is.Not.Null);
            Assert.That(prompt.Card.Artwork, Is.Not.Null);
            Assert.That(prompt.Card.Label, Is.Not.Null);
            Assert.That(prompt.Card.Button.enabled, Is.False);
            Assert.That(prompt.Card.Portraits, Is.Empty);
            Assert.That(prompt.Enter, Is.Not.Null);
            Assert.That(prompt.Back, Is.Not.Null);
            Assert.That(prompt.Details, Is.Not.Null);
            Assert.That(prompt.Requirement, Is.Not.Null);
            Assert.That(prompt.GetComponentsInChildren<CampaignSlots>(true), Is.Empty);
            Assert.That(prompt.Enter.onClick.GetPersistentEventCount(), Is.Zero);
            Assert.That(prompt.Back.onClick.GetPersistentEventCount(), Is.Zero);
            Assert.That(prompt.GetComponentsInChildren<Canvas>(true).Single().sortingOrder, Is.EqualTo(2100));
        }
    }
}
