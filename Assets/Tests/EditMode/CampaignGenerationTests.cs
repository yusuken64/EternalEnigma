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
                Is.EqualTo("d6e39457d3b30daebf9489d43e97c03b291a82c08dd4b0e999e506f51b8bd356"));
            var validation = CampaignValidator.Validate(campaign);
            Assert.That(validation.IsValid, Is.True, string.Join("\n", validation.Errors));
            Assert.That(validation.GuaranteedCriticalPath.ReachableLocations, Does.Contain(campaign.FinalLocationId));
            var session = new CampaignSession(campaign);
            Assert.That(session.LocationId, Is.EqualTo(campaign.StartLocationId));
            Assert.That(session.TrySetParty(), Is.True);
        }
    }
}
