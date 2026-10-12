using System.Linq;
using EternalEnigma.Core.Capabilities;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using EternalEnigma.Core.Validation;
using NUnit.Framework;

namespace EternalEnigma.Tests.CoreIntegration
{
    public class CampaignGenerationTests
    {
        [Test]
        public void NewKeyGateSkinsAllHaveAuthoredVisualAssets()
        {
            var catalog = DioramaCatalog.Load();
            foreach (var skin in LockSkinCatalog.Skins.Where(s => s.Capability >= Capability.MillwrightTraining))
            {
                var route = new CampaignRoute("test", "a", "b", new Requirement(CapabilitySet.Of(skin.Capability)),
                    LockForm.Interaction, skinId: skin.Id);
                var models = OverworldLockVisual.Models(route);
                Assert.That(catalog.Get(models.Model)?.Prefab, Is.Not.Null, skin.Id);
                Assert.That(catalog.Get(models.Detail)?.Prefab, Is.Not.Null, skin.Id);
            }
        }

        [Test]
        public void ImportedDllMatchesHeadlessCampaignAndStartsASession()
        {
            var campaign = CampaignGenerator.Generate(42);
            Assert.That(CampaignFingerprint.Compute(campaign),
                Is.EqualTo("9b4a3a6ef33bc932c0559010713209696e5a102a7c5133cbd30568b17ba4e9ec"));
            var validation = CampaignValidator.Validate(campaign);
            Assert.That(validation.IsValid, Is.True, string.Join("\n", validation.Errors));
            Assert.That(validation.GuaranteedCriticalPath.ReachableLocations, Does.Contain(campaign.FinalLocationId));
            var session = new CampaignSession(campaign);
            Assert.That(session.LocationId, Is.EqualTo(campaign.StartLocationId));
            Assert.That(session.TrySetParty(), Is.True);
        }
    }
}
