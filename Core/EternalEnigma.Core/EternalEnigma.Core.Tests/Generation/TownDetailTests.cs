using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using EternalEnigma.Core.World;
using Xunit;

namespace EternalEnigma.Core.Tests.Generation;

public class TownDetailTests
{
    private static (TownPlan Plan, TownPlanOptions Options) ServiceTown(int seed)
    {
        var layout = TownLayout.Create(seed, TownServiceCatalog.All, CampaignContext.AuthoredTownBuildings);
        return (TownPlanGenerator.Generate(layout.Options), layout.Options);
    }

    [Fact]
    public void BuildingsVaryInSizeAndShape()
    {
        var sizes = new HashSet<(int, int)>();
        bool notched = false;
        for (int seed = 0; seed < 20; seed++)
        {
            var (plan, _) = ServiceTown(seed);
            var inTown = plan.Footprints.Select(f => (f.Bounds.Width, f.Bounds.Height)).Distinct().ToArray();
            Assert.True(inTown.Length >= 3, $"Seed {seed}: only {inTown.Length} building sizes in one town.");
            foreach (var size in inTown) sizes.Add(size);
            notched |= plan.Footprints.Any(f => f.HasNotch);
        }
        Assert.True(sizes.Count >= 6, $"Only {sizes.Count} distinct building sizes across seeds.");
        Assert.All(sizes, s => Assert.True(s.Item1 % 2 == 1 && s.Item1 is >= 3 and <= 7 && s.Item2 is >= 4 and <= 6));
        Assert.True(notched, "No notched (L-shaped) building was generated.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-3)]
    public void EveryRoomFitsItsFootprint(int seed)
    {
        var (plan, options) = ServiceTown(seed);
        Assert.Equal(plan.BuildingSlots.Count, plan.Footprints.Count);
        for (int i = 0; i < plan.Footprints.Count; i++)
        {
            var room = plan.ShopRoomAt(plan.BuildingSlots[i]);
            if (room == null) continue;
            var cells = new HashSet<GridPoint>(plan.Footprints[i].Cells);
            Assert.All(room.Floor.Concat(room.Wall), c => Assert.Contains(c, cells));
            Assert.Equal(cells.Count, room.Floor.Count + room.Wall.Count);
            Assert.Contains(room.VendorAnchor, room.Floor);
        }
        Assert.True(TownPlanValidator.Validate(plan, options).IsValid);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99)]
    public void RoadsHaveMainRoadsArteriesAndAlleys(int seed)
    {
        var (plan, _) = ServiceTown(seed);
        var roads = plan.Layers[TownLayers.Roads];
        var main = plan.Layers[TownLayers.MainRoads];
        var alleys = plan.Layers[TownLayers.Alleys];

        // Wide main roads: three cells across the spine, and a three-row cross avenue.
        Assert.Equal(3, Enumerable.Range(0, plan.Width).Count(x => main[x, 0]));
        int avenueRows = Enumerable.Range(0, plan.Height).Count(y => Enumerable.Range(2, plan.Width - 4).All(x => main[x, y]));
        Assert.Equal(3, avenueRows);

        // Single-cell alleys (no 2x2 block of alley cells) and arteries that are neither main road nor alley.
        int alleyCells = 0, arteryCells = 0;
        for (int x = 0; x < plan.Width; x++)
            for (int y = 0; y < plan.Height; y++)
            {
                if (alleys[x, y]) alleyCells++;
                if (roads[x, y] && !main[x, y] && !alleys[x, y]) arteryCells++;
                if (x + 1 < plan.Width && y + 1 < plan.Height)
                    Assert.False(alleys[x, y] && alleys[x + 1, y] && alleys[x, y + 1] && alleys[x + 1, y + 1], $"Alley is wider than one cell at ({x},{y}).");
            }
        Assert.True(alleyCells >= 10, $"Only {alleyCells} alley cells.");
        Assert.True(arteryCells >= 10, $"Only {arteryCells} artery cells.");
        Assert.True(Enumerable.Range(0, plan.Width).Sum(x => Enumerable.Range(0, plan.Height).Count(y => main[x, y])) > alleyCells);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(42)]
    public void PropsFrameTheTownWithoutBlockingAnything(int seed)
    {
        var (plan, _) = ServiceTown(seed);
        var props = plan.Layers[TownLayers.Props];
        int frameProps = 0, frameCells = 0, innerProps = 0, innerCells = 0;
        for (int x = 0; x < plan.Width; x++)
            for (int y = 0; y < plan.Height; y++)
            {
                bool frame = Math.Min(Math.Min(x, plan.Width - 1 - x), Math.Min(y, plan.Height - 1 - y)) < 2;
                if (frame) frameCells++; else innerCells++;
                if (!props[x, y]) continue;
                if (frame) frameProps++; else innerProps++;
                Assert.True(plan.IsWalkable(new GridPoint(x, y)), $"Prop at ({x},{y}) is on a blocked cell.");
            }
        Assert.True(frameProps > 0 && innerProps > 0);
        Assert.True((double)frameProps / frameCells > 2.0 * innerProps / innerCells, "Props should be denser around the edge than inside.");
    }

    [Fact]
    public void PropsDoNotChangeWhereEverythingElseGoes()
    {
        // Props are drawn from their own stream after everything else, so the layout is fixed by the same seed.
        var (first, _) = ServiceTown(7);
        var (second, _) = ServiceTown(7);
        foreach (var name in TownLayers.All.Concat(TownLayers.Detail))
            Assert.Equal(first.Layers[name].ToArray().Cast<bool>(), second.Layers[name].ToArray().Cast<bool>());
    }

    [Fact]
    public void SimpleTownsAreUnchanged()
    {
        var plan = TownPlanGenerator.Generate(new TownPlanOptions(42));
        Assert.Empty(plan.Footprints);
        Assert.DoesNotContain(TownLayers.MainRoads, plan.Layers.Keys);
        Assert.Equal(new GridPoint(10, 2), plan.PartySpawn);
    }

    [Fact]
    public void DetailedSweepIsValidAtTheCampaignTownSize()
    {
        for (int seed = 0; seed < 150; seed++)
        {
            var (plan, options) = ServiceTown(seed);
            var result = TownPlanValidator.Validate(plan, options);
            Assert.True(result.IsValid, $"Seed {seed}: {string.Join("; ", result.Errors)}");
        }
    }
}
