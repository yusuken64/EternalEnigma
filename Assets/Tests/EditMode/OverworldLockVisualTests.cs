using System.Linq;
using EternalEnigma.Core.Generation;
using EternalEnigma.Core.Progression;
using NUnit.Framework;

namespace EternalEnigma.Tests
{
    public sealed class OverworldLockVisualTests
    {
        [Test]
        public void EveryAuthoredLockSkinSelectsExistingVisualAssets()
        {
            var catalog=DioramaCatalog.Load();
            Assert.That(catalog,Is.Not.Null);
            foreach(var skin in LockSkinCatalog.Skins)
            {
                var route=new CampaignRoute("test","a","b",Requirement.Open,skin.Form,skinId:skin.Id);
                var models=OverworldLockVisual.Models(route);
                Assert.That(catalog.Get(models.Model)?.Prefab,Is.Not.Null,skin.Id);
                if(models.Detail!=null)Assert.That(catalog.Get(models.Detail)?.Prefab,Is.Not.Null,skin.Id);
            }
            Assert.That(LockSkinCatalog.Skins.Select(s=>OverworldLockVisual.Models(new CampaignRoute("test","a","b",Requirement.Open,s.Form,skinId:s.Id))).Distinct().Count(),Is.GreaterThan(12));
        }
    }
}
