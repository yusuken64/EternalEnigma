using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EternalEnigma.Core.Progression;

namespace EternalEnigma.Core.Generation;

public static class CampaignFingerprint
{
    /// <summary>Canonical logical content, independent of collection iteration order or current culture.</summary>
    public static string CanonicalText(Campaign campaign)
    {
        var output = new StringBuilder();
        void Row(params object?[] fields)
        {
            foreach (var field in fields)
            {
                string value = Convert.ToString(field, CultureInfo.InvariantCulture) ?? string.Empty;
                output.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
            }
            output.Append('\n');
        }
        Row("campaign", campaign.GeneratorVersion, campaign.Seed, campaign.StartLocationId, campaign.FinalLocationId);
        foreach (var c in campaign.Manifest.OrderBy(c => c.Id)) Row("capability", c.Id, c.Role, c.Tier);
        foreach (var r in campaign.Regions.OrderBy(r => r.Id, StringComparer.Ordinal)) Row("region", r.Id, r.Theme, r.Tier, r.ProgressionOrder);
        foreach (var l in campaign.Locations.OrderBy(l => l.Id, StringComparer.Ordinal)) Row("location", l.Id, l.RegionId, l.Tier, l.Kind, l.Required, l.Stage, l.ParentTownId);
        foreach (var r in campaign.Routes.OrderBy(r => r.Id, StringComparer.Ordinal)) Row("route", r.Id, r.From, r.To, r.Requirement, r.Form, r.Required, r.IsProgressionBoundary, r.ShortcutKind, r.UnlockingEndpoint, r.KeyId, r.KeyLocationId, r.IsWarp, r.KeyCondition, r.IsTownExit);
        foreach (var s in campaign.Sources.OrderBy(s => s.Id, StringComparer.Ordinal)) Row("source", s.Id, s.LocationId, s.Capability, s.CompanionId, s.Guaranteed, s.Prerequisites);
        foreach (var c in campaign.Companions.OrderBy(c => c.Id, StringComparer.Ordinal)) Row("companion", c.Id, c.Capability);
        foreach (var r in campaign.ReturnObjectives.OrderBy(r => r.DestinationIds[0], StringComparer.Ordinal))
            Row("return", r.RegionId, string.Join(",", r.DestinationIds.OrderBy(x => x, StringComparer.Ordinal)),
                string.Join(",", r.GateIds.OrderBy(x => x, StringComparer.Ordinal)), r.EnablingCapability, r.AcquisitionStage, r.Required, r.RewardCapability);
        return output.ToString();
    }

    public static string Compute(Campaign campaign)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalText(campaign)))
            .Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
