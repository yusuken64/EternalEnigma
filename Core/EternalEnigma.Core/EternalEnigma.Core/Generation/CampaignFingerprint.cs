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
        foreach (var r in campaign.Regions.OrderBy(r => r.Id, StringComparer.Ordinal)) Row("region", r.Id, r.Theme, r.Tier);
        foreach (var l in campaign.Locations.OrderBy(l => l.Id, StringComparer.Ordinal)) Row("location", l.Id, l.RegionId, l.Tier, l.Kind, l.Required, l.Stage);
        foreach (var r in campaign.Routes.OrderBy(r => r.Id, StringComparer.Ordinal)) Row("route", r.Id, r.From, r.To, r.Requirement, r.Form, r.Required, r.IsProgressionBoundary);
        foreach (var s in campaign.Sources.OrderBy(s => s.Id, StringComparer.Ordinal)) Row("source", s.Id, s.LocationId, s.Capability, s.CompanionId, s.Guaranteed, s.Prerequisites);
        foreach (var c in campaign.Companions.OrderBy(c => c.Id, StringComparer.Ordinal)) Row("companion", c.Id, c.Capability);
        return output.ToString();
    }

    public static string Compute(Campaign campaign)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(CanonicalText(campaign)))
            .Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
