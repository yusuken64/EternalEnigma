using System.Text.Json;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public sealed class TownDungeonTests
{
    [Theory]
    [InlineData(OverworldBiome.Grassland)] [InlineData(OverworldBiome.Forest)]
    [InlineData(OverworldBiome.Desert)] [InlineData(OverworldBiome.Water)]
    [InlineData(OverworldBiome.Mountain)] [InlineData(OverworldBiome.Tundra)]
    [InlineData(OverworldBiome.Marsh)] [InlineData(OverworldBiome.Volcanic)]
    public void DistrictSweep(OverworldBiome biome)
    {
        for (int tier = 0; tier < 5; tier++)
        foreach (var role in Enum.GetValues<DungeonFloorRole>())
        for (int seed = 0; seed < 24; seed++)
        {
            var options = DungeonLayoutProfile.Options(seed, biome, tier, role, variant: DungeonVariant.Town);
            var floor = DungeonFloorGenerator.Generate(options);
            Assert.Equal(DungeonFloorFingerprint.Compute(floor), DungeonFloorFingerprint.Compute(DungeonFloorGenerator.Generate(options)));
            Assert.True(DungeonFloorValidator.Validate(floor, options).IsValid, $"{biome}/{tier}/{role}/{seed}");
            var town = Assert.IsType<TownPlan>(floor.TownPlan);
            Assert.Equal(8 + tier, town.Interiors.Count);
            Assert.Equal(town.Layers[TownLayers.Walkable].ToArray().Cast<bool>(), floor.Layers[DungeonLayers.Floor].ToArray().Cast<bool>());
            var occupied = floor.Enemies.Select(p => p.Cell).Concat(floor.Items.Select(p => p.Cell)).Concat(floor.Gold.Select(p => p.Cell))
                .Concat(floor.Scenery.Select(p => p.Cell)).Concat(floor.GatheringSites.Select(p => p.Cell)).ToArray();
            Assert.Equal(occupied.Length, occupied.Distinct().Count());
            Assert.All(occupied, p => Assert.True(floor.IsWalkable(p)));
            Assert.DoesNotContain(occupied, p => TownDungeonGenerator.BossSquare(town).Contains(p));
            foreach (var door in town.BuildingSlots)
            {
                Assert.DoesNotContain(door, occupied);
                Assert.True(floor.Distance(floor.Start, door) >= 0);
                Assert.True(floor.CanStep(door, new GridPoint(door.X, door.Y + 1)));
            }
            var blocked = floor.Scenery.Where(p => p.Kind != DungeonSceneryKind.Hazard).Select(p => p.Cell).ToHashSet();
            var reachable = GridSearch.VisitOrder(floor.Start, p => BiomeDungeonGenerator.Neighbors(floor.Layers[DungeonLayers.Floor], p, blocked).Where(q => town.AllowsDoorwayStep(p,q))).ToHashSet();
            Assert.All(town.ShopRooms.SelectMany(r => r.Floor).Where(p => floor.IsWalkable(p) && !blocked.Contains(p)), p => Assert.Contains(p, reachable));
            blocked.UnionWith(floor.Scenery.Select(p => p.Cell));
            blocked.UnionWith(floor.Enemies.Select(p => p.Cell));
            Assert.Contains(floor.Stairs, GridSearch.VisitOrder(floor.Start, p => BiomeDungeonGenerator.Neighbors(floor.Layers[DungeonLayers.Floor], p, blocked).Where(q => town.AllowsDoorwayStep(p,q))));
            if (role != DungeonFloorRole.Regular) { Assert.Empty(occupied); Assert.True(floor.IsThroneFloor); }
        }
    }

    [Fact]
    public void FriendlyAndHostileTownDoorsRequireCardinalStepsAndRemainReachable()
    {
        var district = DungeonFloorGenerator.Generate(DungeonLayoutProfile.Options(42, OverworldBiome.Forest, 2, variant: DungeonVariant.Town));
        var friendly = TownPlanGenerator.Generate(TownLayout.Create(42, TownServiceCatalog.All, 1, 0, 7).Options);
        foreach (var plan in new[] { friendly, district.TownPlan! })
        foreach (var door in plan.BuildingSlots)
        {
            foreach (var offset in GridSteps.EightWay.Where(p => p.X != 0 && p.Y != 0))
            {
                var side = new GridPoint(door.X + offset.X, door.Y + offset.Y);
                Assert.False(plan.CanStep(side, door)); Assert.False(plan.CanStep(door, side));
                if (ReferenceEquals(plan, district.TownPlan))
                { Assert.False(district.CanStep(side, door)); Assert.DoesNotContain(door, district.Neighbors(side)); }
            }
            Assert.True(plan.CanStep(new GridPoint(door.X, door.Y - 1), door));
            Assert.Equal(plan.ShopRoomAt(door) != null, plan.CanStep(door, new GridPoint(door.X, door.Y + 1)));
        }
        Assert.True(district.Distance(district.Start, district.Stairs) > 0);
    }

    [Fact]
    public void AssignmentIsStableExcludesStartersAndPreservesExistingProgression()
    {
        for (int seed = 0; seed < 24; seed++)
        {
            var campaign = CampaignGenerator.Generate(seed);
            var legacy = CampaignGenerator.GenerateVersion(seed, CampaignGenerator.LegacyVersion);
            var eligible = campaign.Locations.Where(l => l.Kind == LocationKind.RepeatableDungeon && l.Encounter == null && !DungeonLayoutProfile.IsStarter(l.Id)).ToArray();
            var selected = campaign.Locations.Where(l => l.Variant == DungeonVariant.Town).ToArray();
            Assert.Equal((eligible.Length + 1) / 2, selected.Length);
            Assert.All(selected, l => Assert.Contains(l, eligible));
            Assert.Equal(selected.Select(l => l.Id), CampaignGenerator.Generate(seed).Locations.Where(l => l.Variant == DungeonVariant.Town).Select(l => l.Id));
            Assert.Equal(legacy.Routes.Select(r => (r.Id, r.From, r.To, r.Requirement.ToString(), r.KeyId)), campaign.Routes.Select(r => (r.Id, r.From, r.To, r.Requirement.ToString(), r.KeyId)));
            Assert.Equal(legacy.Sources.Select(s => (s.Id, s.LocationId, s.Capability)), campaign.Sources.Select(s => (s.Id, s.LocationId, s.Capability)));
            foreach (var location in selected)
            {
                var run = new DungeonRunPlan(seed, location);
                var old = new DungeonRunPlan(seed, legacy.Locations.Single(l => l.Id == location.Id));
                Assert.Equal(old.Floors.Select(f => (f.Number, f.CombatDifficulty, f.Role)), run.Floors.Select(f => (f.Number, f.CombatDifficulty, f.Role)));
                Assert.All(run.Floors, f => Assert.Equal(DungeonVariant.Town, f.Options.Variant));
            }
        }
    }

    [Fact]
    public void NewAndUnversionedLegacySavesRoundTripWithoutChangingTheirCampaign()
    {
        var launch = new OverworldLaunchOptions(OverworldLaunchMode.Campaign, 42);
        var context = new CampaignContext(launch);
        var json = new JsonSerializerOptions { IncludeFields = true };
        var snapshot = JsonSerializer.Deserialize<CampaignSnapshot>(JsonSerializer.Serialize(context.Capture(), json), json)!;
        Assert.Equal(CampaignGenerator.Version, snapshot.GeneratorVersion);
        Assert.Equal(CampaignFingerprint.Compute(context.Campaign), CampaignFingerprint.Compute(new CampaignContext(launch, snapshot).Campaign));
        var legacy = new CampaignSnapshot { Seed = 42, Fingerprint = "c4da7117be88acad6b4d14703d1579d2db1c2d525e85c600daafa4215ef9980c" };
        var restored = new CampaignContext(launch, legacy);
        Assert.All(restored.Campaign.Locations, l => Assert.Equal(DungeonVariant.Standard, l.Variant));
        Assert.Equal(CampaignGenerator.LegacyVersion, restored.Capture().GeneratorVersion);
        Assert.Equal(legacy.Fingerprint, new CampaignContext(launch, restored.Capture()).State.Fingerprint);
    }

    [Fact]
    public void VariantSeparatesCacheIdentityAndDefaultsRemainStandard()
    {
        var district = DungeonLayoutProfile.Options(42, OverworldBiome.Desert, 2, variant: DungeonVariant.Town);
        var standard = new DungeonFloorOptions(42, district.Width, district.Height, enemyCount: 10, goldCount: 5, itemCount: 5,
            trapCount: 0, gatheringCount: 3, layoutVersion: 1, biome: OverworldBiome.Desert, tier: 2);
        Assert.NotEqual(standard, district);
        Assert.Equal(2, new HashSet<DungeonFloorOptions> { standard, district }.Count);
        Assert.Equal(DungeonVariant.Standard, new DungeonFloorOptions(42).Variant);
        Assert.NotEqual(DungeonFloorFingerprint.Compute(DungeonFloorGenerator.Generate(standard)), DungeonFloorFingerprint.Compute(DungeonFloorGenerator.Generate(district)));
    }
}
