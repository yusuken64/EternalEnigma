#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum ChaosMode { Smoke, Full }
public enum ChaosDevice { Mixed, Keyboard, Mouse, Controller }

[Serializable]
public sealed class ChaosOptions
{
    public int Seed = 42, RunCount = 1;
    public float DurationSeconds = 300, ActionTimeoutSeconds = 10, LoadTimeoutSeconds = 60, RecoverySeconds = 10;
    public ChaosMode Mode;
    public ChaosDevice Device;
    public string ReportRoot = "", ReplayDirectory = "", ResumeDirectory = "", ScenarioDirectory = "";
    public string ScenarioFilter = "", AbilityFilter = "";
    public bool QuitWhenFinished = true;
    public void Validate()
    {
        if (RunCount < 1 || (long)Seed + RunCount - 1 > int.MaxValue ||
            !Enum.IsDefined(typeof(ChaosMode), Mode) || !Enum.IsDefined(typeof(ChaosDevice), Device))
            throw new ArgumentException("Invalid chaos mode, device, or seed range.");
        foreach (float seconds in new[] { DurationSeconds, ActionTimeoutSeconds, LoadTimeoutSeconds, RecoverySeconds })
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentException("Chaos durations must be finite and positive.");
        if (!string.IsNullOrEmpty(ReplayDirectory) && !string.IsNullOrEmpty(ResumeDirectory))
            throw new ArgumentException("Replay and resume cannot be combined.");
    }
}

// Explicit stable PRNG: fixture generation does not depend on Unity.Random or System.Random versions.
public sealed class ChaosRandom
{
    private uint state;
    public ChaosRandom(int seed) { state = (uint)seed; if (state == 0) state = 0x9e3779b9; }
    public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
    public int Range(int exclusive) { if (exclusive <= 0) throw new ArgumentOutOfRangeException(nameof(exclusive)); return (int)(Next() % (uint)exclusive); }
    public static int Derive(int seed, string stream)
    {
        uint hash = 2166136261;
        foreach (byte value in Encoding.UTF8.GetBytes(seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/" + stream))
            hash = unchecked((hash ^ value) * 16777619);
        return unchecked((int)hash);
    }
    public void Shuffle<T>(IList<T> list) { for (int i = list.Count - 1; i > 0; i--) { int j = Range(i + 1); (list[i], list[j]) = (list[j], list[i]); } }
}

[Serializable]
public sealed class ChaosPrerequisites
{
    public string HeroId, ClassId, SkillName;
    public int Rank = 1, Level = 1, HP = -1, Mana = -1;
    public bool ControlRequiredHero;
    public List<ItemSaveData> Equipment = new(), Inventory = new();
}

[Serializable]
public sealed class ChaosFixture
{
    public const int Version = 1;
    public int Schema = Version, Seed, HeroSeed, InventorySeed, InputSeed;
    public string Hash, Composition, ControlledHeroId, SaveJson, Entry = "Town", UnityRandomState;
    public List<string> Guaranteed = new();
    public List<TownAllyData> Party = new();
    public List<ItemSaveData> Inventory = new();
    public void Seal() { Hash = null; Hash = ChaosJson.Hash(JsonUtility.ToJson(this)); }
    public void Validate()
    {
        string expected = Hash; Hash = null;
        string actual;
        try { actual = ChaosJson.Hash(JsonUtility.ToJson(this)); } finally { Hash = expected; }
        if (Schema != Version || expected != actual) throw new InvalidDataException("Fixture version/hash mismatch.");
        var save = JsonUtility.FromJson<GameSaveData>(SaveJson);
        if (save == null || save.FormatVersion != SaveSystem.CurrentFormatVersion || !save.HasCampaign || save.TownSaveData == null)
            throw new InvalidDataException("Fixture must contain a campaign save in the current supported format.");
        if (JsonUtility.ToJson(new PartyEnvelope { Party = save.TownSaveData.RecruitedAlliesData, Inventory = save.TownSaveData.InventoryItems }) !=
            JsonUtility.ToJson(new PartyEnvelope { Party = Party, Inventory = Inventory })) throw new InvalidDataException("Fixture manifest differs from its save.");
    }
    [Serializable] private sealed class PartyEnvelope { public List<TownAllyData> Party; public List<ItemSaveData> Inventory; }
}

[Serializable]
public sealed class ChaosCase
{
    public string Id, Kind, Ability, Source, Variant, Device, Reason = "No verified scenario result";
    public int Rank;
    public bool Demo, Passed;
}

[Serializable]
public sealed class ChaosParticipation
{
    public string Id, Kind;
    public int Prepared, Exercised;
}

[Serializable]
public sealed class ChaosCoverage
{
    public List<ChaosCase> Cases = new();
    public List<ChaosParticipation> Participation = new();
    public bool FullPass => Cases.Any(c => !c.Demo) && Cases.Where(c => !c.Demo).All(c => c.Passed) &&
        Participation.Count > 0 && Participation.All(p => p.Exercised > 0);
}

[Serializable]
public sealed class ChaosEvidence
{
    public string CaseId, BeforeHash, AfterHash, Oracle, Failure;
    public bool MenuOpened, Invoked, EffectVerified, CostsVerified, TurnsVerified, ControlRestored, Triggered, PairedComparison;
    public bool Satisfies(ChaosCase row) => string.IsNullOrEmpty(Failure) && !string.IsNullOrEmpty(Oracle) &&
        !string.IsNullOrEmpty(BeforeHash) && !string.IsNullOrEmpty(AfterHash) && EffectVerified && ControlRestored &&
        (row.Kind == "Feature" || row.Kind == "ItemFlow" ? Invoked :
         row.Kind == "Passive" || row.Kind == "ClassPassive" ? Triggered && PairedComparison :
         MenuOpened && Invoked && CostsVerified && TurnsVerified);
}

[Serializable]
public sealed class ChaosIssue
{
    public string Kind, Message, Stack, Screenshot, Before, After;
    public double Seconds;
}

[Serializable]
public sealed class ChaosReport
{
    public int Schema = 1, Seed, Inputs;
    public string State = "Running", Reason, BuildId, ContentId, ScenarioSetId, FixtureVersion = "1", StartedUtc, EndedUtc;
    public double Seconds;
    public bool ArtifactsComplete, SmokePassed, FullPassed, ReplayDiverged;
    public ChaosOptions Options;
    public ChaosCoverage Coverage;
    public List<ChaosIssue> Issues = new();
    public List<string> FixtureHashes = new();
    public List<ChaosArtifact> Artifacts = new();
}

[Serializable]
public sealed class ChaosArtifact { public string Path, Sha256; public long Bytes; }

public static class ChaosJson
{
    public static string Hash(string value) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
    public static void Write(string path, object value) => AutoplaySweepManifest.WriteJson(path, value);
    public static T Read<T>(string path) => JsonUtility.FromJson<T>(File.ReadAllText(path));
    public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    public static string FileHash(string path) { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    public static void VerifyArtifacts(string directory, ChaosReport report)
    {
        if (!report.ArtifactsComplete || report.Artifacts.Count == 0) throw new InvalidDataException("Incomplete report artifacts.");
        string root = System.IO.Path.GetFullPath(directory) + System.IO.Path.DirectorySeparatorChar;
        foreach (var artifact in report.Artifacts)
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, artifact.Path));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || new FileInfo(path).Length != artifact.Bytes || FileHash(path) != artifact.Sha256)
                throw new InvalidDataException("Missing or changed artifact: " + artifact.Path);
        }
    }
    public static void RequireIdentity(ChaosReport previous, string build, string content)
    {
        if (previous.Schema != 1 || previous.FixtureVersion != ChaosFixture.Version.ToString() || previous.BuildId != build || previous.ContentId != content)
            throw new InvalidDataException("Build/content/fixture version changed; replay/resume refused.");
    }
}
#endif
