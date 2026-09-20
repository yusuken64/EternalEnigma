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
                Is.EqualTo("2d0a5925c9428cc076f43cff650af5e91625592c71b5e07411c312385961a890"));
            var validation = CampaignValidator.Validate(campaign);
            Assert.That(validation.IsValid, Is.True, string.Join("\n", validation.Errors));
            Assert.That(validation.GuaranteedCriticalPath.ReachableLocations, Does.Contain(campaign.FinalLocationId));
            var session = new CampaignSession(campaign);
            Assert.That(session.LocationId, Is.EqualTo(campaign.StartLocationId));
            Assert.That(session.TrySetParty(), Is.True);
        }
    }
}
