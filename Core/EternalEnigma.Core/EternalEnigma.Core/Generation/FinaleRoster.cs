namespace EternalEnigma.Core.Generation;

/// <summary>Wave 4 families reserved for disguised finale locations.</summary>
public static class FinaleRoster
{
    public static IReadOnlyList<BossProfile> Bosses { get; } = Array.AsReadOnly(new[] {
        new BossProfile("Castle Monster", 4, "Telegraphed bombardment"),
        new BossProfile("Bone Dragon", 4, "Deathly breath cone"),
        new BossProfile("Ice Golem", 4, "Freezing ground slam") });
    public static IReadOnlyList<string> Towns { get; } = Array.AsReadOnly(new[] { "Castle Monster", "Bone Dragon", "Ice Golem" });
    public static IReadOnlyList<string> Dungeons { get; } = Array.AsReadOnly(new[] {
        "Flame Knight", "Little Demon", "Skeleton Mage", "Blue Slime", "Crawler", "Flower Monster", "Star Fish" });
    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(Towns.Concat(Dungeons).ToArray());
    public static bool IsBoss(string? family) => family != null && Towns.Contains(family);
    public static bool IsReserved(string name) => Required.Any(f => name == f || name == "Enemy_" + PrefabStem(f) || name == "Boss_" + PrefabStem(f));
    public static string PrefabStem(string family) => family.Replace(" ", "");
}
