using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EternalEnigma.Core.World;

namespace EternalEnigma.Core.Generation;

public static class DungeonFloorFingerprint
{
    public static string Compute(DungeonFloor floor)
    {
        var text = new StringBuilder();
        void Row(params object?[] fields) { foreach (var field in fields) { var value = Convert.ToString(field, CultureInfo.InvariantCulture) ?? ""; text.Append(value.Length).Append(':').Append(value); } text.Append('\n'); }
        Row(DungeonFloor.GenerationVersion, floor.Seed, floor.Width, floor.Height, floor.Start, floor.Stairs, floor.Objective.Kind);
        foreach (var layer in floor.Layers.OrderBy(p => p.Key, StringComparer.Ordinal)) { Row(layer.Key); for (int y = 0; y < floor.Height; y++) for (int x = 0; x < floor.Width; x++) text.Append(layer.Value[x, y] ? '1' : '0'); }
        foreach (var p in floor.PuzzleObjects) Row("object", p.Id, p.Kind, p.Position, p.InitialState, p.TargetState);
        foreach (var p in floor.ObjectiveEnemies) Row("required", p.Id, p.Position, p.Family, p.Invulnerable, p.Footprint);
        foreach (var p in floor.RecoveryStashes) Row("stash", p.Id, p.Position);
        foreach (var p in floor.Enemies) Row("enemy", p.Cell, p.Roll);
        foreach (var p in floor.Gold) Row("gold", p.Cell, p.Roll);
        foreach (var p in floor.Items) Row("item", p.Cell, p.Roll);
        foreach (var p in floor.Traps) Row("trap", p.Cell, p.Roll);
        foreach (var p in floor.Scenery) Row("scenery", p.Cell, p.Kind, p.HitPoints, p.Roll, p.Reward, p.HoldsKey);
        foreach (var p in floor.GatheringSites) Row("gathering", p.Cell, p.Kind, p.Roll);
        foreach (var p in floor.Locks) Row("lock", p.Door, p.HitPoints, string.Join(",", p.Vault));
        foreach (var p in floor.Exits) Row("exit", p.Id, p.Position);
        foreach (var p in floor.SolutionWitness) Row("step", p);
        using var sha = SHA256.Create(); return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
