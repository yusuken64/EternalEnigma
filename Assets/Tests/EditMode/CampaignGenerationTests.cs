using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using NUnit.Framework;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class CampaignGenerationTests
    {
        [Test]
        public void ImportedDllMatchesHeadlessCampaignAndStartsASession()
        {
            var campaign = CampaignGenerator.Generate(42);
            Assert.That(CampaignFingerprint.Compute(campaign),
                Is.EqualTo("76573a1fec60e44c6812f5cc818c8c7d2c067c9345f87970930db0999f53083f"));
            var validation = CampaignValidator.Validate(campaign);
            Assert.That(validation.IsValid, Is.True, string.Join("\n", validation.Errors));
            Assert.That(validation.GuaranteedCriticalPath.ReachableLocations, Does.Contain(campaign.FinalLocationId));
            var session = new CampaignSession(campaign);
            Assert.That(session.LocationId, Is.EqualTo(campaign.StartLocationId));
            Assert.That(session.TrySetParty(), Is.True);
        }
    }
}
