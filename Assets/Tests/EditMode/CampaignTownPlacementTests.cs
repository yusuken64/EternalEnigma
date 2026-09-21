using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class CampaignTownPlacementTests
    {
        [Test]
        public void CorridorShortfallGetsDeterministicReachableUnoccupiedReplacement()
        {
            var floor = new bool[20, 20]; var allies = new bool[20, 20];
            for (int x = 0; x < 20; x++) for (int y = 0; y < 20; y++) floor[x, y] = x != 3;
            allies[7, 2] = true;
            var existing = new[] { new Vector3Int(6, 7), new Vector3Int(15, 7), new Vector3Int(15, 13) };
            var spawn = new Vector3Int(10, 2);
            var result = CampaignTownCorridor.CompleteBuildingPositions(floor, allies, existing, spawn, 4);
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result.Take(3), Is.EqualTo(existing));
            Assert.That(result.All(p => p.x > 3 && floor[p.x, p.y] && !allies[p.x, p.y] &&
                !CampaignTownCorridor.IsReserved(p, 20)), Is.True);
            Assert.That(result.Distinct().Count(), Is.EqualTo(4));
            Assert.That(CampaignTownCorridor.CompleteBuildingPositions(floor, allies, existing, spawn, 4), Is.EqualTo(result));
        }
    }
}
