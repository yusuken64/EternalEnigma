using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class TargetingAndArrowTests
{
    private List<GameObject> gameObjects = new();
    private List<ScriptableObject> scriptableObjects = new();

    [SetUp]
    public void SetUp()
    {
        gameObjects.Clear();
        scriptableObjects.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in gameObjects)
            Object.DestroyImmediate(go);
        gameObjects.Clear();

        foreach (var so in scriptableObjects)
            Object.DestroyImmediate(so);
        scriptableObjects.Clear();
    }

    private Ally CreateAlly()
    {
        var go = new GameObject();
        gameObjects.Add(go);
        var ally = go.AddComponent<Ally>();
        return ally;
    }

    private UsableItemDefinition CreateArrowDefinition(string name, int stackMax, int stackStartMin = 1, int stackStartMax = 1)
    {
        var def = ScriptableObject.CreateInstance<UsableItemDefinition>();
        scriptableObjects.Add(def);
        def.ItemName = name;
        def.StackMax = stackMax;
        def.StackStartMin = stackStartMin;
        def.StackStartMax = stackStartMax;
        return def;
    }

    private EquipmentItemDefinition CreateEquipmentDefinition(WeaponType weaponType, EquipmentSlot slot)
    {
        var def = ScriptableObject.CreateInstance<EquipmentItemDefinition>();
        scriptableObjects.Add(def);
        def.WeaponType = weaponType;
        def.EquipmentSlot = slot;
        return def;
    }

    private Inventory CreateInventory(GameObject go)
    {
        return go.AddComponent<Inventory>();
    }

    [Test]
    public void NearestFirstWithoutTaunt()
    {
        var enemy = CreateAlly();
        enemy.Vitals = new Vitals { HP = 10 };
        enemy.StatusEffects = new List<StatusEffect>();

        var candidateA = CreateAlly();
        candidateA.Vitals = new Vitals { HP = 10 };
        candidateA.StatusEffects = new List<StatusEffect>();

        var candidateB = CreateAlly();
        candidateB.Vitals = new Vitals { HP = 10 };
        candidateB.StatusEffects = new List<StatusEffect>();

        var candidates = new List<Character> { candidateA, candidateB };
        var target = EnemyTargeting.SelectTarget(enemy, candidates);

        Assert.That(target, Is.SameAs(candidateA), "Without taunt, nearest first (A) is selected");
    }

    [Test]
    public void TauntPullsToTaunterWhenVisible()
    {
        var enemy = CreateAlly();
        enemy.Vitals = new Vitals { HP = 10 };
        enemy.StatusEffects = new List<StatusEffect>();

        var candidateA = CreateAlly();
        candidateA.Vitals = new Vitals { HP = 10 };
        candidateA.StatusEffects = new List<StatusEffect>();

        var candidateB = CreateAlly();
        candidateB.Vitals = new Vitals { HP = 10 };
        candidateB.StatusEffects = new List<StatusEffect>();

        // Add Taunt to enemy with B as taunter
        var taunt = enemy.gameObject.AddComponent<TauntStatusEffect>();
        taunt.Taunter = candidateB;
        taunt.TurnsLeft = 5;
        enemy.StatusEffects.Add(taunt);

        var candidates = new List<Character> { candidateA, candidateB };
        var target = EnemyTargeting.SelectTarget(enemy, candidates);

        Assert.That(target, Is.SameAs(candidateB), "Taunt pulls to taunter when visible");

        // Test: if B isn't in candidates, A is selected
        var candidatesWithoutB = new List<Character> { candidateA };
        target = EnemyTargeting.SelectTarget(enemy, candidatesWithoutB);

        Assert.That(target, Is.SameAs(candidateA), "If taunter not in candidates, A is selected");
    }

    [Test]
    public void ExpiredTauntIgnored()
    {
        var enemy = CreateAlly();
        enemy.Vitals = new Vitals { HP = 10 };
        enemy.StatusEffects = new List<StatusEffect>();

        var candidateA = CreateAlly();
        candidateA.Vitals = new Vitals { HP = 10 };
        candidateA.StatusEffects = new List<StatusEffect>();

        var candidateB = CreateAlly();
        candidateB.Vitals = new Vitals { HP = 10 };
        candidateB.StatusEffects = new List<StatusEffect>();

        // Add expired Taunt to enemy
        var taunt = enemy.gameObject.AddComponent<TauntStatusEffect>();
        taunt.Taunter = candidateB;
        taunt.TurnsLeft = 0; // Expired
        enemy.StatusEffects.Add(taunt);

        var candidates = new List<Character> { candidateA, candidateB };
        var target = EnemyTargeting.SelectTarget(enemy, candidates);

        Assert.That(target, Is.SameAs(candidateA), "Expired taunt is ignored, nearest first is selected");
    }

    [Test]
    public void StealthedAndDeadAreSkipped()
    {
        var enemy = CreateAlly();
        enemy.Vitals = new Vitals { HP = 10 };
        enemy.StatusEffects = new List<StatusEffect>();

        var candidateA = CreateAlly();
        candidateA.Vitals = new Vitals { HP = 10 };
        candidateA.StatusEffects = new List<StatusEffect>();

        var candidateB = CreateAlly();
        candidateB.Vitals = new Vitals { HP = 10 };
        candidateB.StatusEffects = new List<StatusEffect>();

        // Test 1: A is stealthed, B should be selected
        var stealth = candidateA.gameObject.AddComponent<StealthStatusEffect>();
        stealth.TurnsLeft = 5;
        candidateA.StatusEffects.Add(stealth);

        var candidates = new List<Character> { candidateA, candidateB };
        var target = EnemyTargeting.SelectTarget(enemy, candidates);

        Assert.That(target, Is.SameAs(candidateB), "Stealthed candidate A is skipped, B is selected");

        // Clean up stealth for next test
        candidateA.StatusEffects.Clear();

        // Test 2: A has HP 0, B should be selected
        candidateA.Vitals = new Vitals { HP = 0 };

        target = EnemyTargeting.SelectTarget(enemy, candidates);
        Assert.That(target, Is.SameAs(candidateB), "Dead candidate A is skipped, B is selected");

        // Test 3: All candidates excluded, null is returned
        candidateB.Vitals = new Vitals { HP = 0 };
        var stealth2 = candidateA.gameObject.AddComponent<StealthStatusEffect>();
        stealth2.TurnsLeft = 5;
        candidateA.StatusEffects.Add(stealth2);

        target = EnemyTargeting.SelectTarget(enemy, candidates);
        Assert.That(target, Is.Null, "When all candidates excluded, null is returned");
    }

    [Test]
    public void ArrowsCountedByNameOnly()
    {
        var character = CreateAlly();
        var inventory = CreateInventory(character.gameObject);

        // Create arrow definition
        var arrowDef = CreateArrowDefinition("Wooden Arrows", stackMax: 10);
        var arrows = new UsableInventoryItem(arrowDef, 5);
        inventory.InventoryItems.Add(arrows);

        // Create another stackable item that is NOT an arrow
        var boltDef = CreateArrowDefinition("Spell: Bolt", stackMax: 10);
        var bolts = new UsableInventoryItem(boltDef, 3);
        inventory.InventoryItems.Add(bolts);

        int count = ArrowSupply.Count(inventory);
        Assert.That(count, Is.EqualTo(5), "Only arrows named 'Wooden Arrows' are counted");
    }

    [Test]
    public void ConsumeRemovesEmptyStacks()
    {
        var character = CreateAlly();
        var inventory = CreateInventory(character.gameObject);

        // Create arrow definition
        var arrowDef = CreateArrowDefinition("Wooden Arrows", stackMax: 10);
        var arrows = new UsableInventoryItem(arrowDef, 5);
        inventory.InventoryItems.Add(arrows);

        // Consume 3 arrows
        int consumed = ArrowSupply.Consume(inventory, 3);
        Assert.That(consumed, Is.EqualTo(3), "3 arrows consumed");
        Assert.That(arrows.StackStock, Is.EqualTo(2), "Stack reduced to 2");
        Assert.That(inventory.InventoryItems.Contains(arrows), Is.True, "Stack still in inventory");

        // Consume 10 more arrows (only 2 available)
        consumed = ArrowSupply.Consume(inventory, 10);
        Assert.That(consumed, Is.EqualTo(2), "2 arrows consumed");
        Assert.That(inventory.InventoryItems.Contains(arrows), Is.False, "Empty stack removed from inventory");
    }

    [Test]
    public void RecoveryKeepsArrows()
    {
        var character = CreateAlly();
        var inventory = CreateInventory(character.gameObject);

        // Create arrow definition
        var arrowDef = CreateArrowDefinition("Wooden Arrows", stackMax: 10);
        var arrows = new UsableInventoryItem(arrowDef, 5);
        inventory.InventoryItems.Add(arrows);

        // Consume 3 arrows with 100% recovery chance
        int consumed = ArrowSupply.Consume(inventory, 3, 1f);
        Assert.That(consumed, Is.EqualTo(3), "3 arrows consumed");
        Assert.That(arrows.StackStock, Is.EqualTo(5), "Stock unchanged with 100% recovery");
    }

    [Test]
    public void RequiredToCastByMode()
    {
        // Test 1: ArrowCost 0 → 0
        var skill0 = ScriptableObject.CreateInstance<Skill>();
        scriptableObjects.Add(skill0);
        skill0.ArrowCost = 0;
        skill0.ArrowCostMode = ArrowCostMode.Fixed;

        int required = ArrowSupply.RequiredToCast(skill0);
        Assert.That(required, Is.EqualTo(0), "ArrowCost 0 requires 0 arrows");

        // Test 2: Fixed 2 → 2
        var skillFixed = ScriptableObject.CreateInstance<Skill>();
        scriptableObjects.Add(skillFixed);
        skillFixed.ArrowCost = 2;
        skillFixed.ArrowCostMode = ArrowCostMode.Fixed;

        required = ArrowSupply.RequiredToCast(skillFixed);
        Assert.That(required, Is.EqualTo(2), "Fixed mode with ArrowCost 2 requires 2 arrows");

        // Test 3: PerTarget 1 → 1
        var skillPerTarget = ScriptableObject.CreateInstance<Skill>();
        scriptableObjects.Add(skillPerTarget);
        skillPerTarget.ArrowCost = 1;
        skillPerTarget.ArrowCostMode = ArrowCostMode.PerTarget;

        required = ArrowSupply.RequiredToCast(skillPerTarget);
        Assert.That(required, Is.EqualTo(1), "PerTarget mode requires 1 arrow");
    }

    [Test]
    public void HasBowChecksWeaponType()
    {
        var character = CreateAlly();
        var equipment = character.gameObject.AddComponent<Equipment>();
        character.Equipment = equipment;

        // Test 1: No equipment → false
        bool hasBow = ArrowSupply.HasBow(character);
        Assert.That(hasBow, Is.False, "Character with no equipment has no bow");

        // Test 2: With bow in OffHand → true
        var bowDef = CreateEquipmentDefinition(WeaponType.BowAndArrow, EquipmentSlot.OffHand);
        var bow = new EquipableInventoryItem(bowDef);
        equipment.EquippedShield = bow;

        hasBow = ArrowSupply.HasBow(character);
        Assert.That(hasBow, Is.True, "Character with bow equipped has bow");

        // Test 3: With sword instead → false
        var swordDef = CreateEquipmentDefinition(WeaponType.SingleSword, EquipmentSlot.MainHand);
        var sword = new EquipableInventoryItem(swordDef);
        equipment.EquippedWeapon = sword;
        equipment.EquippedShield = null;

        hasBow = ArrowSupply.HasBow(character);
        Assert.That(hasBow, Is.False, "Character with sword but no bow has no bow");
    }
}
