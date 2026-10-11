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
                Is.EqualTo("1676456bccafdc864d6bc4801eb2ad7418331992216c3c200ed0c5f6d69dd5c3"));
            var validation = CampaignValidator.Validate(campaign);
            Assert.That(validation.IsValid, Is.True, string.Join("\n", validation.Errors));
            Assert.That(validation.GuaranteedCriticalPath.ReachableLocations, Does.Contain(campaign.FinalLocationId));
            var session = new CampaignSession(campaign);
            Assert.That(session.LocationId, Is.EqualTo(campaign.StartLocationId));
            Assert.That(session.TrySetParty(), Is.True);
        }
    }
}
