using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests.EditMode
{
    public sealed class PresentationGeometryTests
    {
        [TestCase(42, OverworldBiome.Grassland)]
        [TestCase(93, OverworldBiome.Volcanic)]
        [TestCase(12, OverworldBiome.Desert)]
        public void DistrictGateFramesJoinTheBoundaryAndKeepInteractionMarkers(int seed, OverworldBiome biome)
        {
            var floor = DungeonFloorGenerator.Generate(DungeonLayoutProfile.Options(seed, biome, 4, variant: DungeonVariant.Town));
            var plan = floor.TownPlan;
            var root = new GameObject("Gate fixture");
            const float size = 2.5f;
            try
            {
                foreach (var marker in new[] { plan.Exit, plan.DungeonEntrance })
                {
                    bool onward = marker.Equals(plan.DungeonEntrance);
                    var gate = TownGateVisuals.DistrictGateCell(plan, marker);
                    Assert.That(plan.Layers[TownLayers.Boundary][gate.X, gate.Y], Is.True);
                    Assert.That(TownGateVisuals.DistrictApproach(plan, marker, gate.X, gate.Y), Is.True);
                    string name = onward ? "Exit" : "Entrance";
                    TownGateVisuals.Create(EnvironmentKit.Load(), biome, root.transform, marker, size, name, false, onward,
                        TownGateVisuals.DistrictRotation(plan, marker, onward), gate);
                    var frame = root.transform.Find(name + "/Gate");
                    Assert.That(frame.position, Is.EqualTo(new Vector3(gate.X + .5f, gate.Y + .5f, 0) * size));
                    Assert.That(frame.parent.position, Is.EqualTo(new Vector3(marker.X + .5f, marker.Y + .5f, 0) * size));
                    Assert.That(frame.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.InRange(3f, 3.2f));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
