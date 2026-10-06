using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CombatEffectAuthoring
{
    public const string Root = "Assets/Resources/CombatEffects";
    static readonly Dictionary<string, string> families = new();
    static readonly Dictionary<string, string> icons = new();
    static Dictionary<string, string> prefabs;

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Refresh Status Presentation")]
    public static void RefreshStatusPresentation()
    {
        Initialize();
        var catalog = AssetDatabase.LoadAssetAtPath<CombatVisualCatalog>(Root + "/Catalog.asset");
        var used = new HashSet<Sprite>(AssetDatabase.FindAssets("t:Skill").Select(g =>
            AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(g))?.Icon).Where(s => s != null));
        var icons = Directory.GetFiles("Assets/RPG_skills_and_abilities/red", "*.png")
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
            .Where(s => s != null && !used.Contains(s)).ToArray();
        int index = 0;
        foreach (var entry in catalog.Statuses.OrderBy(e => e.TypeName, StringComparer.Ordinal))
        {
            var profile = entry.Profile;
            if (profile == null) continue;
            if (index >= icons.Length) throw new InvalidOperationException("Not enough unused status icons.");
            profile.Icon = icons[index++];
            string name = entry.TypeName;
            profile.HasLoop = name.Contains("Sleep") || name.Contains("Stun") || name.Contains("Paralysis") ||
                name.Contains("Confusion") || name.Contains("Parry") || name.Contains("DamageReduction") || name.Contains("Barrier");
            profile.Loop = name.Contains("Sleep") ? AnimatedAction.Sleeping :
                name.Contains("Stun") || name.Contains("Paralysis") || name.Contains("Confusion") ? AnimatedAction.Dizzy : AnimatedAction.Defend;
            string effect = name.Contains("Curse") || name.Contains("Weaken") || name.Contains("Frailty") ? "CurseShadow" :
                name.Contains("Burn") ? "DamageOverTimeFire" : name.Contains("Dot") ? "DamageOverTimeShadow" :
                name.Contains("Barrier") || name.Contains("DamageShield") ? "LightDome" :
                name.Contains("Song") ? "LightOrbitSphere" :
                name.Contains("Sleep") || name.Contains("Stun") || name.Contains("Confusion") ? "ArcaneOrbitSphere" : null;
            profile.Overhead = name.Contains("Sleep") || name.Contains("Stun") || name.Contains("Confusion");
            if (effect != null) profile.Aura = Stage(effect, .3f, !profile.Overhead);
            EditorUtility.SetDirty(profile);
        }
        EditorUtility.SetDirty(catalog);
        foreach (var guid in AssetDatabase.FindAssets("t:Skill"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill?.VisualProfile == null) continue;
            string effect = skill.SkillName switch
            {
                "Sanctuary Wall" => "LightWallCircle", "Flame Barrier" => "FireWallCircle",
                "Thunderclap" => "LightningPillarBlast", "Tempest" => "StormPillarBlast",
                "Siphon" => "Shadow Beam", "Double Strike" or "Whirlwind" or "Lunge" => "ArcaneSlash",
                _ => null
            };
            if (effect == null) continue;
            string path = Root + "/Profiles/Skill " + skill.SkillName.Replace(':', '-') + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(path);
            if (profile == null)
            {
                profile = UnityEngine.Object.Instantiate(skill.VisualProfile);
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.Impact = Stage(effect, .5f, effect.Contains("Wall") || effect.Contains("Pillar"));
            profile.Impact.FitToTarget = true;
            if (effect.Contains("Wall")) profile.Area = Stage(effect, .4f, true);
            skill.VisualProfile = profile;
            EditorUtility.SetDirty(profile); EditorUtility.SetDirty(skill);
        }
        NormalizeGroundStages();
        AssetDatabase.SaveAssets();
    }

    static void Group(string family, string names) { foreach (var n in names.Split('|')) families[n] = family; }
    static void Icons(string folder, string pairs)
    {
        foreach (var pair in pairs.Split('|')) { var p = pair.Split('='); icons[p[0]] = folder + "/" + p[1]; }
    }
    static void Initialize()
    {
        families.Clear(); icons.Clear();
        Group("Fire", "Fire Bolt|Fireball|Inferno|Ember Scroll|Flame Follow-up|Blazing Arms|Flame Barrier");
        Group("Frost", "Ice Bolt|Blizzard|Glacier|Frost Follow-up|Frost Arms|Frost Barrier");
        Group("Lightning", "Lightning Bolt|Thunderclap|Tempest|Shock Follow-up|Storm Arms|Storm Barrier|Stunning Shot|Paralysis Hex");
        Group("Poison", "Venom Hex|Venom Arrow|Plague|Blinding Mist");
        Group("Shadow", "Curse|Dominate|Enfeeble|Exploit|Silence Hex|Siphon|Slumber Hex|Terror|Discord|Sleep Arrow");
        Group("Life", "Heal|Full Heal|Group Heal|Cure|Mass Cure|Renew|Revive|Mass Revive|Unbind|Rally|Rally Cry|Soothing Melody|Rousing Chorus|Field Kitchen");
        Group("Protection", "Bulwark|Parry Stance|Sanctuary Wall|Ballad of Stone|Command: Guard|Command: Endure|Command: Hold|Safe Passage");
        Group("Portal", "Shadow Step|Shadow Dance|Vanish|Smoke Bomb|Retreat");
        Group("Summon", "Clone");
        Group("Song", "Battle Hymn|Blade Dance|Encore|Evasive Rhythm|Grand Finale|Quickstep|War Drums");
        Group("Command", "Command: Advance|Command: Attack|Decisive Order|Inspire|Provoke");
        Group("Arrow", "Aimed Shot|Disarming Shot|Multishot|Piercing Arrow|Pinning Shot|Rain of Arrows|Retreat Shot|Volley|Grapple Line");
        Group("Melee", "Cleave|Double Strike|Lunge|Piercing Thrust|Rattle|Sunder|Whirlwind|Shield Bash|Shield Smite|Heavy Strike|Assassinate|Hamstring|Throat Strike|Caltrops");
        Icons("orange", "Fire Bolt=205|Fireball=fs_907|Inferno=fs_991|Ember Scroll=0993|Flame Follow-up=9989|Blazing Arms=9991|Flame Barrier=fsy_065|Power Boost=0984|Heavy Strike=psk_030|Sunder=fsy_078|Cleave=psk_059|Whirlwind=9987|War Drums=fsy_090|Battle Hymn=psk_036");
        Icons("blue", "Ice Bolt=9927|Blizzard=9936|Glacier=9938|Frost Follow-up=269|Frost Arms=292|Frost Barrier=9930|Lightning Bolt=0980|Thunderclap=9952|Tempest=fs_936|Shock Follow-up=rpg03_024|Storm Arms=psk_052|Storm Barrier=fs_911|Stunning Shot=247|Paralysis Hex=rpg03_005|Shield Bash=245|Shield Smite=fsy_067|Defense Boost=psk_019|Aegis=245|Mana Flow=9933|SP Up=fsy_068|Spell Echo=9955|Elemental Mastery=9954|Focus=9946");
        Icons("green", "Heal=rpg03_052|Full Heal=rpg03_076|Group Heal=9978|Cure=sabl_008|Mass Cure=9961|Renew=9965|Revive=240|Mass Revive=rpg03_007|Unbind=fs_950|Rally=9978|Rally Cry=rpg03_052|Soothing Melody=261|Rousing Chorus=295|Field Kitchen=fs_909|Venom Hex=9966|Venom Arrow=rpg03_086|Plague=296|Blinding Mist=275|Healing Touch=rpg03_032|Field Medicine=sabl_010|Triage=fs_950|Vitality=9965|HP Regen=rpg03_076|Vigor=9965|Foraging=218|Harvesting=psk_098|Safe Passage=0965|Resourceful=sfd_013|Scavenger=psk_094|Farsight=9967|Floor Sense=0918|Survey=274|Trap Sense=fs_928|Soft Step=9972");
        Icons("violet", "Curse=fs_920|Dominate=9907|Enfeeble=9909|Exploit=9915|Silence Hex=rpg03_083|Siphon=9910|Slumber Hex=sgh_89|Terror=rpg03_020|Discord=sgh_36|Sleep Arrow=0955|Shadow Step=fsy_005|Shadow Dance=9918|Vanish=0895|Smoke Bomb=0946|Clone=0957|Blinding Flash=270|Amplify=rpg03_042|Expose=9901|Malice=sgh_12|Grim Harvest=sfd_096|Lingering Hex=sgh_89|Twin Shadows=0957|Ambush=fs_904|Escape Artist=fsy_005");
        Icons("grey", "Aimed Shot=0905|Disarming Shot=sk_r_028|Multishot=psk_078|Piercing Arrow=psk_043|Pinning Shot=psk_099|Rain of Arrows=psk_095|Retreat Shot=sk_r_028|Volley=psk_078|Grapple Line=sk_r_089|Double Strike=psk_033|Lunge=fs_980|Piercing Thrust=sabl_001|Rattle=sk_r_040|Assassinate=fsy_020|Hamstring=sfd_042|Throat Strike=sk_r_072|Caltrops=psk_057|Disarm=sk_r_038|Mining=sk_r_032|Iron Skin=sfd_010|Cover=sabl_098|Arrow Recovery=psk_078|Double Shot=psk_033|Longshot=psk_043|Throwing Arm=psk_023|Take Point=sk_r_083|Pathfinder=sk_r_083|Nimble=sk_r_094|Swiftness=fs_908|Evasion=0925");
        Icons("yellow", "Bulwark=273|Parry Stance=273|Sanctuary Wall=273|Ballad of Stone=fs_992|Command: Guard=sgh_71|Command: Endure=psk_008|Command: Hold=sfd_060|Command: Advance=sfd_081|Command: Attack=sts_051|Decisive Order=strg_098|Inspire=fs_949|Provoke=sabl_022|Encore=fsy_001|Evasive Rhythm=0912|Grand Finale=rpg03_090|Quickstep=sfd_081|Retreat=sfd_081|Keen Eye=rpg03_019|Deadeye=rpg03_019|Treasure Hunter=sts_047|Leadership=fs_949|Royal Bearing=strg_098|Aura of Command=fs_934|Reinforce=273|Stage Presence=fs_934|Harmony=fsy_032|Crescendo=sabl_024|Meditation=spn_012|Second Wind=fs_934|Last Stand=sfd_049|Initiative=rpg03_013|Vanguard=273|Follow-up Mastery=sts_051|Improved Follow-ups=sts_051");
        prefabs = new Dictionary<string, string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/52SpecialEffectPack", "Assets/MagicArsenal/Effects" }))
        { var path = AssetDatabase.GUIDToAssetPath(guid); prefabs.TryAdd(Path.GetFileNameWithoutExtension(path), path); }
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Assign Missing Profiles And Icons")]
    public static void Author()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Author combat assets in Edit mode.");
        Initialize();
        Directory.CreateDirectory(Root + "/Profiles"); Directory.CreateDirectory(Root + "/Statuses");
        Directory.CreateDirectory("Assets/Prefabs/CombatEffects"); AssetDatabase.Refresh();
        var catalog = Asset<CombatVisualCatalog>(Root + "/Catalog.asset");
        catalog.Melee ??= Profile("Melee", false, false);
        catalog.Ranged ??= Profile("Arrow", true, false);
        catalog.Confusion ??= Profile("Shadow", true, false);
        catalog.Root ??= Profile("Poison", false, false);
        catalog.Steal ??= Profile("Portal", false, false);
        catalog.Heal ??= Profile("Life", false, false);
        catalog.Utility ??= Profile("Utility", false, false);
        catalog.Fire ??= Profile("Fire", false, false); catalog.Ice ??= Profile("Frost", false, false); catalog.Lightning ??= Profile("Lightning", false, false);
        foreach (var type in TypeCache.GetTypesDerivedFrom<StatusEffect>().Where(t => !t.IsAbstract))
        {
            if (catalog.Statuses.Any(e => e.TypeName == type.FullName)) continue;
            string family = StatusFamily(type.Name);
            var status = Asset<StatusVisualProfile>(Root + "/Statuses/" + type.Name + ".asset");
            if (status.Aura.Prefab == null)
            {
                status.Aura = Stage(AuraPrefab(family), .3f, true);
                status.Priority = family == "Shadow" || family == "Poison" ? 30 : family == "Protection" ? 20 : 10;
                EditorUtility.SetDirty(status);
            }
            catalog.Statuses.Add(new CombatVisualCatalog.StatusEntry { TypeName = type.FullName, Profile = status });
        }
        EditorUtility.SetDirty(catalog);
        int skillCount = 0, characterCount = 0, statusCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Skill", new[] { "Assets" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null) continue;
            string family = families.TryGetValue(skill.SkillName, out var found) ? found : "Utility";
            bool projectile = skill.Targeting == SkillTargeting.Missile || skill.UsesArrows ||
                (skill.Targeting == SkillTargeting.SelectedTarget && new[] { "Fire", "Frost", "Lightning", "Poison", "Shadow" }.Contains(family));
            bool area = skill.AreaRadius > 0 || skill.Targeting == SkillTargeting.AllTargets || new[] { "Rain of Arrows", "Whirlwind", "Grand Finale", "Inferno", "Tempest", "Blizzard" }.Contains(skill.SkillName);
            skill.VisualProfile ??= Profile(family, projectile, area);
            if (skill.Icon == null) skill.Icon = IconFor(skill, AssetDatabase.GetAssetPath(skill));
            EditorUtility.SetDirty(skill); skillCount++;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponentInChildren<Character>(true) == null && prefab.GetComponentInChildren<StatusEffect>(true) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var character in root.GetComponentsInChildren<Character>(true))
                {
                    var binding = character.GetComponent<CharacterCombatEffects>() ?? character.gameObject.AddComponent<CharacterCombatEffects>();
                    binding.Melee ??= catalog.Melee;
                    binding.Ranged ??= path.Contains("RatAssassin") ? Profile("Kunai", true, false) : catalog.Ranged;
                    binding.Confusion ??= catalog.Confusion; binding.Root ??= catalog.Root; binding.Steal ??= catalog.Steal;
                    characterCount++;
                }
                foreach (var status in root.GetComponentsInChildren<StatusEffect>(true))
                {
                    status.VisualProfile ??= catalog.ForStatus(status);
                    foreach (var oldParticles in status.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var renderer = oldParticles.GetComponent<ParticleSystemRenderer>();
                        if (renderer != null) Object.DestroyImmediate(renderer);
                        Object.DestroyImmediate(oldParticles);
                    }
                    foreach (var trail in status.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(trail);
                    statusCount++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        NormalizeGroundStages();
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log($"Combat effects assigned: {skillCount} skills with icons, {characterCount} characters, {statusCount} status components, {catalog.Statuses.Count} runtime status types. Both effect packs are in the catalog.");
    }

    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
    }

    static string StatusFamily(string name)
    {
        if (name.Contains("Burn")) return "Fire";
        if (name.Contains("Hot")) return "Life";
        if (name.Contains("Dot") || name.Contains("Root")) return "Poison";
        if (name.Contains("Song")) return "Song";
        if (name.Contains("Command") || name.Contains("Strength")) return "Command";
        if (name.Contains("Barrier") || name.Contains("Shield") || name.Contains("Reduction") || name.Contains("Parry") || name.Contains("Endure") || name.Contains("SafePassage")) return "Protection";
        return "Shadow";
    }
    static string AuraPrefab(string family) => family switch
    { "Fire" => "AuraSimpleFire", "Life" => "AuraSimpleLife", "Protection" => "AuraRingLight", "Poison" => "Swamp", "Song" => "RuneOfMagic", "Command" => "AuraSimpleLight", _ => "AuraSimpleShadow" };

    static CombatEffectProfile Profile(string family, bool projectile, bool area)
    {
        string name = family + (projectile ? " Flight" : " Direct") + (area ? " Area" : "");
        string path = Root + "/Profiles/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(path);
        if (existing != null) return existing;
        var profile = Asset<CombatEffectProfile>(path);
        string element = family switch { "Fire" => "Fire", "Frost" => "Frost", "Lightning" => "Lightning", "Life" => "Life", "Shadow" => "Shadow", "Protection" => "Light", "Command" => "Light", _ => "Arcane" };
        bool physical = family == "Melee" || family == "Arrow" || family == "Kunai";
        if (!physical)
        {
            profile.Muzzle = Stage(element + "MuzzleNormal", .6f);
            profile.GroundCircle = Stage(family == "Summon" ? "SummonMagicCircle2" : "SummonMagicCircle", .45f, true);
        }
        string missile = family switch
        { "Fire" => "FireBall", "Frost" => "IceBall", "Lightning" => "LightningBall", "Poison" => "SwampBall", "Shadow" => "DeadBall", "Arrow" => "ElementalArrow", "Kunai" => "Kunai", _ => element + "MissileSmall" };
        if (projectile) profile.Projectile = Stage(missile, physical ? .35f : .45f);
        profile.Impact = Stage(family switch
        { "Melee" => "LightSlashHit", "Arrow" => "LightExplosionTiny", "Kunai" => "ShadowSlashHit", "Poison" => "PoisonExplode", "Portal" => "Portal", "Summon" => "SummonMagicCircle3", "Song" => "RainbowExplode", "Utility" => "MagicCircleRelease", _ => element + "ExplosionSmall" }, physical ? .45f : .65f, true);
        profile.Impact.FitToTarget = true;
        if (family == "Melee")
        {
            profile.Impact.Scale = 1.5f;
        }
        if (area) profile.Area = Stage(family switch
        { "Fire" => "AreaDamageFire", "Frost" => "IceCloud", "Lightning" => "StormCloud", "Poison" => "Swamp", "Melee" => "LightCleave", "Arrow" => "RainLight", _ => "Nova" + element }, .4f, true);
        profile.CastSeconds = physical ? .25f : .45f;
        EditorUtility.SetDirty(profile); return profile;
    }

    static CombatEffectStage Stage(string name, float scale, bool ground = false)
    {
        if (!prefabs.TryGetValue(name, out var source)) throw new InvalidOperationException("Missing imported effect: " + name);
        string path = "Assets/Prefabs/CombatEffects/" + name + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
            try
            {
                foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script != null && script.GetType().Name != "csAnimationSpin" && script.GetType().Name != "MagicRotation") Object.DestroyImmediate(script);
                foreach (var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var light in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.stopAction = ParticleSystemStopAction.None; main.playOnAwake = false; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
                ConvertHorizontalBillboards(go);
                asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
        }
        return new CombatEffectStage { Prefab = asset, Scale = scale, Rotation = ground ? new Vector3(-90, 0, 0) : source.Contains("52SpecialEffectPack") ? new Vector3(0, 180, 0) : Vector3.zero, Lifetime = 2 };
    }

    static Sprite IconFor(Skill skill, string path)
    {
        if (!icons.TryGetValue(skill.SkillName, out string key))
        {
            key = path.Contains("Archer") ? "grey/0905" : path.Contains("Guardian") ? "yellow/273" :
                path.Contains("Warrior") ? "grey/sabl_006" : path.Contains("Healer") ? "green/rpg03_052" :
                path.Contains("Bard") ? "orange/psk_036" : path.Contains("Commander") ? "yellow/strg_098" :
                path.Contains("Rogue") ? "violet/0895" : path.Contains("Scout") ? "green/274" :
                path.Contains("Occultist") ? "violet/rpg03_042" : "blue/9954";
        }
        var file = Directory.GetFiles("Assets/RPG_skills_and_abilities/" + key.Split('/')[0])
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == key.Split('/')[1] && Path.GetExtension(p).Equals(".png", StringComparison.OrdinalIgnoreCase));
        if (file == null) throw new InvalidOperationException("Missing skill icon " + key);
        return AssetDatabase.LoadAssetAtPath<Sprite>(file.Replace('\\', '/'));
    }

    internal static float ReferenceDiameter(GameObject prefab)
    {
        var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
        var planar = particles.Where(p => p.name.IndexOf("circle", StringComparison.OrdinalIgnoreCase) >= 0 || p.name.IndexOf("ring", StringComparison.OrdinalIgnoreCase) >= 0 || p.name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        return Mathf.Max(.1f, (planar.Length > 0 ? planar : particles).Select(p =>
        {
            var renderer = p.GetComponent<ParticleSystemRenderer>();
            float mesh = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null ? Mathf.Max(renderer.mesh.bounds.size.x, renderer.mesh.bounds.size.y, renderer.mesh.bounds.size.z) : 1;
            var scale = p.transform.lossyScale;
            return Mathf.Max(.1f, p.main.startSize.constantMax) * Mathf.Max(scale.x, scale.y, scale.z) * mesh;
        }).DefaultIfEmpty(1).Max());
    }

    static void GroundFootprint(CombatEffectStage stage)
    {
        if (stage?.Prefab == null) return;
        stage.MinimumDiameterCells = Mathf.Max(3, stage.MinimumDiameterCells);
        stage.ReferenceDiameter = ReferenceDiameter(stage.Prefab);
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Enforce Three Tile Ground Footprints")]
    public static void NormalizeGroundStages()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile", new[] { Root }))
        {
            var p = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            GroundFootprint(p.GroundCircle); GroundFootprint(p.Area);
            if (p.Impact.Prefab != null && (p.Impact.Prefab.name.Contains("Circle") || p.Impact.Prefab.name.Contains("Portal") || p.Impact.Prefab.name.Contains("Rune"))) GroundFootprint(p.Impact);
            EditorUtility.SetDirty(p);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:StatusVisualProfile", new[] { Root }))
        {
            var p = AssetDatabase.LoadAssetAtPath<StatusVisualProfile>(AssetDatabase.GUIDToAssetPath(guid));
            GroundFootprint(p.Aura); EditorUtility.SetDirty(p);
        }
        AssetDatabase.SaveAssets();
    }

    // Explicit repair command; normal assignment does not overwrite hand-tuned rotations.
    [MenuItem("Tools/Eternal Enigma/Combat Effects/Apply Dungeon Ground Orientation")]
    public static void OrientGroundStages()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/CombatEffects" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { ConvertHorizontalBillboards(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile", new[] { Root }))
        {
            var p = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var stage in new[] { p.GroundCircle, p.Impact, p.Area }) if (stage.Prefab != null) stage.Rotation = new Vector3(-90, 0, 0);
            EditorUtility.SetDirty(p);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:StatusVisualProfile", new[] { Root }))
        {
            var p = AssetDatabase.LoadAssetAtPath<StatusVisualProfile>(AssetDatabase.GUIDToAssetPath(guid));
            p.Aura.Rotation = new Vector3(-90, 0, 0); EditorUtility.SetDirty(p);
        }
        AssetDatabase.SaveAssets();
    }

    static void ConvertHorizontalBillboards(GameObject root)
    {
        // HorizontalBillboard is fixed to Unity's XZ plane and ignores root rotation.
        // World-aligned billboards with zero X/Y tilt lie on this game's XY ground.
        foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard) continue;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            var main = renderer.GetComponent<ParticleSystem>().main;
            main.startRotation3D = true; main.startRotationX = 0; main.startRotationY = 0;
        }
    }

    [MenuItem("Tools/Eternal Enigma/Combat Effects/Validate Assignments")]
    public static void Validate()
    {
        var errors = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Skill"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<Skill>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null && (skill.Icon == null || skill.VisualProfile == null)) errors.Add(skill.name + " is missing its icon or profile");
        }
        foreach (var guid in AssetDatabase.FindAssets("t:CombatEffectProfile"))
        {
            var p = AssetDatabase.LoadAssetAtPath<CombatEffectProfile>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var stage in new[] { p.Muzzle, p.GroundCircle, p.Projectile, p.Impact, p.Area })
                if (stage.Prefab != null && (stage.Lifetime <= 0 || stage.Scale <= 0)) errors.Add(p.name + " has invalid lifetime/scale");
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("Combat visual and skill icon assignments validated.");
    }
}

