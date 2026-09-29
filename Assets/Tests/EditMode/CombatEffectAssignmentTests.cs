using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CombatEffectAssignmentTests
{
    [Test] public void EverySkillHasAnExplicitProfileAndRpgPackIcon()
    {
        var skills = AssetDatabase.FindAssets("t:Skill").Select(g => AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null).ToArray();
        Assert.That(skills.Length, Is.GreaterThanOrEqualTo(202));
        foreach (var skill in skills)
        {
            Assert.That(skill.VisualProfile, Is.Not.Null, skill.name);
            Assert.That(skill.Icon, Is.Not.Null, skill.name);
            Assert.That(AssetDatabase.GetAssetPath(skill.Icon), Does.StartWith("Assets/RPG_skills_and_abilities/"), skill.name);
            Assert.That(skill.VisualProfile.Impact.Prefab, Is.Not.Null, skill.name);
        }
    }

    [Test] public void EveryStatusTypeAndCharacterPrefabHasAssignments()
    {
        var catalog = CombatVisualCatalog.Instance;
        Assert.That(catalog, Is.Not.Null);
        foreach (var type in TypeCache.GetTypesDerivedFrom<StatusEffect>().Where(t => !t.IsAbstract))
        {
            var entry = catalog.Statuses.SingleOrDefault(e => e.TypeName == type.FullName);
            Assert.That(entry?.Profile?.Aura.Prefab, Is.Not.Null, type.Name);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var character in root.GetComponentsInChildren<Character>(true))
            {
                var binding = character.GetComponent<CharacterCombatEffects>();
                Assert.That(binding, Is.Not.Null, root.name);
                foreach (var profile in new[] { binding.Melee, binding.Ranged, binding.Root, binding.Confusion, binding.Steal }) Assert.That(profile, Is.Not.Null, root.name);
            }
            foreach (var status in root.GetComponentsInChildren<StatusEffect>(true))
            {
                Assert.That(status.VisualProfile, Is.Not.Null, root.name);
                Assert.That(status.GetComponentsInChildren<ParticleSystem>(true), Is.Empty, root.name + " still contains legacy status particles");
            }
        }
    }

    [Test] public void ProfilesUseBothPacksAndSafeOwnedPrefabs()
    {
        bool arsenal = false, pack52 = false;
        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile"))
        {
            var p = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.That(p.Impact.FitToTarget, Is.True, p.name + " must adapt its hit impact to the target.");
            Assert.That(p.GroundCircle.FitToTarget || p.Area.FitToTarget || p.Projectile.FitToTarget, Is.False, p.name + " only its impact should fit to the target.");
            foreach (var stage in new[] { p.Muzzle, p.GroundCircle, p.Projectile, p.Impact, p.Area })
            {
                if (stage.Prefab == null) continue;
                Assert.That(stage.Lifetime, Is.GreaterThan(0)); Assert.That(stage.Scale, Is.GreaterThan(0));
                Assert.That(AssetDatabase.GetAssetPath(stage.Prefab), Does.StartWith("Assets/Prefabs/CombatEffects/"));
                string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(stage.Prefab));
                arsenal |= source.Contains("MagicArsenal"); pack52 |= source.Contains("52SpecialEffectPack");
                foreach (var script in stage.Prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    Assert.That(new[] { "csAnimationSpin", "MagicRotation" }, Does.Contain(script.GetType().Name));
            }
        }
        Assert.That(arsenal && pack52, Is.True, "Both imported packs must be used by assigned profiles.");
    }

    [Test] public void CirclesAndAurasCoverAtLeastThreeTilesAndFlatParticlesUseTheDungeonPlane()
    {
        var catalog = CombatVisualCatalog.Instance;
        var profiles = AssetDatabase.FindAssets("t:CombatEffectProfile").Select(g => AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(g)));
        var ground = profiles.SelectMany(p => new[] { p.GroundCircle, p.Area }).Concat(catalog.Statuses.Select(s => s.Profile.Aura));
        foreach (var stage in ground.Where(s => s.Prefab != null))
        {
            Assert.That(stage.MinimumDiameterCells, Is.GreaterThanOrEqualTo(3), stage.Prefab.name);
            foreach (float cellSize in new[] { 1f, 2.5f, 4f })
                Assert.That(CombatEffectPlayer.StageScale(stage, 1, cellSize) * stage.ReferenceDiameter, Is.GreaterThanOrEqualTo(cellSize * 3 - .001f), stage.Prefab.name);
            foreach (var renderer in stage.Prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                Assert.That(renderer.renderMode, Is.Not.EqualTo(ParticleSystemRenderMode.HorizontalBillboard), stage.Prefab.name);
            Assert.That(Vector3.Distance(Quaternion.Euler(stage.Rotation) * Vector3.up, Vector3.back), Is.LessThan(.001f), stage.Prefab.name);
        }
    }
}
