using EternalEnigma.Core.Generation;
using EternalEnigma.Core.World;
using System;

var floor42 = DungeonFloorGenerator.Generate(new DungeonFloorOptions(42));
Console.WriteLine($"Seed 42: {floor42.GatheringSites.Count} gathering sites (expected 1-3)");

var throne = DungeonFloorGenerator.Generate(new DungeonFloorOptions(seed: 1, isThroneFloor: true));
Console.WriteLine($"Throne floor: {throne.GatheringSites.Count} gathering sites (expected 0)");

if (floor42.GatheringSites.Count > 0)
{
    Console.WriteLine("Seed 42 sites:");
    foreach (var site in floor42.GatheringSites)
    {
        Console.WriteLine($"  - {site.Cell} ({site.Kind}) roll={site.Roll}");
    }
}
