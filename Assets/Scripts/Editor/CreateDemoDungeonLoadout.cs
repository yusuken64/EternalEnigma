using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CreateDemoDungeonLoadout
{
    private const string Path = "Assets/Resources/DemoDungeon/Loadout.asset";

    [MenuItem("Tools/Eternal Enigma/Create Demo Loadout")]
    public static void Create()
    {
        var existing = AssetDatabase.LoadAssetAtPath<DemoDungeonLoadout>(Path);
        if (existing != null) { EnsureDemoWeapon(existing); return; }
        Directory.CreateDirectory("Assets/Resources/DemoDungeon");
        AssetDatabase.Refresh();
        var loadout = ScriptableObject.CreateInstance<DemoDungeonLoadout>();
        AssetDatabase.CreateAsset(loadout, Path);
        loadout.PracticeTargetPrefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Dungeon/Enemies/Enemy_Slime.prefab");
        var arrows = AssetDatabase.LoadAssetAtPath<UsableItemDefinition>("Assets/Prefabs/Dungeon/Items/Arrows_WoodenArrows.asset");

        void Add(string source, string label, SkillTargeting targeting, TargetTeam team, int radius = 0, bool weapons = false)
        {
            var skill = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Skill>($"Assets/Prefabs/Dungeon/Skills/SkillsData/{source}.asset"));
            skill.name = skill.SkillName = "Demo: " + label;
            skill.Targeting = targeting;
            skill.TargetSelector = new TargetSelector { Team = team, Area = TargetArea.Visible };
            if (targeting == SkillTargeting.Self) skill.TargetSelector.Area = TargetArea.Self;
            skill.AreaRadius = radius;
            skill.SPCost = 1;
            skill.MissileRange = 8;
            skill.MissileProjectilePrefab = arrows.MissileProjectilePrefab;
            skill.Description = targeting switch
            {
                SkillTargeting.InventoryItem => weapons ? "Choose a weapon to inspect its stats. Does not consume or enchant the selected weapon." :
                    "Choose any inventory item to inspect its stock or stats. Does not consume the selected item.",
                SkillTargeting.Missile => "Aim in eight directions. Hits the first enemy in the path, up to 8 tiles. Walls and allies block shots.",
                SkillTargeting.Self => "Cast immediately on yourself. Applies the Anger status.",
                SkillTargeting.AllTargets => $"Cast immediately on all visible {team.ToString().ToLowerInvariant()}.",
                _ => radius > 0 ? "Choose an enemy. Hits enemies within one tile of it; allies are excluded." :
                    $"Choose one visible {(team == TargetTeam.Allies ? "ally (including yourself) to heal" : "enemy to damage")}."
            };
            if (targeting == SkillTargeting.InventoryItem)
            {
                skill.InventoryTargetSelector = new InventoryTargetSelector { IncludeEquipped = true,
                    ItemType = weapons ? InventoryTargetType.Weapon : InventoryTargetType.AnyItem };
                skill.ActionEffects = new() { new InspectInventoryItemEffect() };
                skill.SkillAnimation = new SkillAnimation();
            }
            if (targeting == SkillTargeting.Missile) skill.SkillAnimation = new SkillAnimation();
            AssetDatabase.AddObjectToAsset(skill, loadout);
            loadout.Skills.Add(skill);

            var item = ScriptableObject.CreateInstance<UsableItemDefinition>();
            item.name = item.ItemName = "Demo Scroll: " + label;
            item.Description = skill.Description + " Use spends one scroll, no SP. Cancel spends nothing.";
            item.StackMax = 99; item.StackStartMin = item.StackStartMax = 20;
            item.Targeting = targeting; item.TargetSelector = skill.TargetSelector;
            item.AreaRadius = radius; item.MissileRange = 8;
            item.MissileProjectilePrefab = arrows.MissileProjectilePrefab;
            item.InventoryTargetSelector = skill.InventoryTargetSelector;
            item.DroppedItemVisual = arrows.DroppedItemVisual;
            if (targeting == SkillTargeting.InventoryItem) item.InventoryEffects = new() { new InspectInventoryItemEffect() };
            else
            {
                var effect = ScriptableObject.CreateInstance<SkillItemEffectDefinition>();
                effect.name = label + " Item Effect";
                effect.EffectSkill = skill;
                AssetDatabase.AddObjectToAsset(effect, loadout);
                item.ItemEffectDefinition = effect;
            }
            AssetDatabase.AddObjectToAsset(item, loadout);
            loadout.Items.Add(item);
        }

        Add("Damage", "Single Hit", SkillTargeting.SelectedTarget, TargetTeam.Enemies);
        Add("Healing", "Ally Mend", SkillTargeting.SelectedTarget, TargetTeam.Allies);
        Add("Damage", "Area Burst", SkillTargeting.SelectedTarget, TargetTeam.Enemies, 1);
        Add("Damage", "Enemy Pulse", SkillTargeting.AllTargets, TargetTeam.Enemies);
        Add("Healing", "Party Mend", SkillTargeting.AllTargets, TargetTeam.Allies);
        Add("Anger", "Self Focus", SkillTargeting.Self, TargetTeam.Self);
        Add("Damage", "Inspect Item", SkillTargeting.InventoryItem, TargetTeam.Self);
        Add("Damage", "Inspect Weapon", SkillTargeting.InventoryItem, TargetTeam.Self, weapons: true);
        Add("Damage", "Arrow Shot", SkillTargeting.Missile, TargetTeam.Enemies);
        loadout.Items.Add(arrows);
        var weapon = AssetDatabase.FindAssets("t:EquipmentItemDefinition", new[] { "Assets/Prefabs/Dungeon/Items" })
            .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .First(w => w.WeaponType == WeaponType.SingleSword && w.EquipmentSlot == EquipmentSlot.MainHand);
        loadout.Items.Add(weapon);
        EnsureDemoWeapon(loadout);
        EditorUtility.SetDirty(loadout);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(Path);
        Debug.Log("Created demo loadout: nine skills, nine matching scrolls, arrows, and a weapon.");
    }

    private static void EnsureDemoWeapon(DemoDungeonLoadout loadout)
    {
        var weapon = loadout.Items.OfType<EquipmentItemDefinition>().Single();
        if (weapon.ItemName == "Demo Training Sword") return;
        var copy = Object.Instantiate(weapon);
        copy.name = copy.ItemName = "Demo Training Sword";
        copy.Description = "Use Inspect Item or Inspect Weapon on this sword to try inventory targeting. It can also be equipped.";
        AssetDatabase.AddObjectToAsset(copy, loadout);
        loadout.Items[loadout.Items.IndexOf(weapon)] = copy;
        EditorUtility.SetDirty(loadout);
        AssetDatabase.SaveAssets();
    }
}
