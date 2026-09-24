# Phase 7 Classes Verification Checklist

This document provides the exact steps to run and validate Phase 7 class content in Unity 6000.2.7f2.

## Run Once in Unity 6000.2.7f2 (In Order)

1. **Let scripts compile**
   - Open the project in Unity 6000.2.7f2
   - Wait for the Unity editor to finish importing all assets and compiling scripts
   - Check the Console tab for any red compilation errors
   - All scripts should compile without errors

2. **Repair Weapon Types (if not done in Phase 1)**
   - If this is your first time setting up the project after Phase 1:
   - Go to `Tools > Eternal Enigma > Classes > Repair Weapon Types`
   - Wait for the operation to complete
   - This ensures weapon type data is properly initialized

3. **Generate All Class Content**
   - Go to `Tools > Eternal Enigma > Classes > Generate All Class Content`
   - Wait for the generation to complete (this may take 10-30 seconds depending on your machine)
   - The operation will:
     - Create the class catalog if missing
     - Generate Phase 3 + Phase 7 status effect prefabs
     - Create shared skill assets
     - Build the 10 class kits (Warrior, Guardian, Archer, Elementalist, Healer, Bard, Occultist, Rogue, Commander, Scout)
     - Assign hero classes to character prefabs
     - Generate enemy resistances and boss configurations
     - Clear town trainer skill lists and regenerate them
   - Check the Console for red errors
   - The output should show asset paths being created/updated in `Assets/Resources/Classes/**`

4. **Verify Console Output**
   - Look for the message indicating successful generation (no red errors)
   - Expected output paths should include:
     - `Assets/Resources/Classes/ClassCatalog.asset`
     - `Assets/Resources/Classes/*/` (class-specific folders)
     - `Assets/Prefabs/Dungeon/StatusEffects/` (status effect prefabs)
     - `Assets/Scenes/DungeonScene.unity` (updated with prefabs)
     - `Assets/Resources/Towns/*.asset` (trainer configurations)

5. **Commit Generated Assets**
   - After successful generation, commit all generated files to version control
   - Generated paths to include:
     - `Assets/Resources/Classes/**`
     - `Assets/Prefabs/Dungeon/StatusEffects/*.prefab`
     - Hero and enemy character prefabs
     - `Assets/Scenes/DungeonScene.unity`
     - `Assets/Resources/Towns/*.asset`

## Harness Commands

Run the following test harness commands to validate all class content:

```bash
node Tools/unity-mcp.mjs harness EditMode     # ClassContentTests
node Tools/unity-mcp.mjs harness PlayMode     # Gameplay tests
node Tools/unity-mcp.mjs harness Skills       # ClassSkillSmokeTests
node Tools/unity-mcp.mjs harness Town         # Town gameplay tests
node Tools/unity-mcp.mjs harness Heroes       # Hero prefab tests
node Tools/unity-mcp.mjs harness Classes      # Class content integration tests
node Tools/unity-mcp.mjs harness Autoplay     # Automated gameplay simulation
```

**Expected Results:**
- All tests should pass with no failures
- If any test fails, check the error message in the harness output
- Common causes of failure:
  - Assets not generated yet (run Generate All Class Content first)
  - Script compilation errors (check Console in Unity)
  - Missing prefab references (regenerate class content)

## Known Risks to Watch

1. **Basic Melee/Bow Damage Changes**
   - Basic melee and bow attacks now include the +5% mastery bonus from class passives
   - Tests asserting exact basic-attack damage numbers may shift by rounding
   - Autoplay results may differ due to the damage multiplier changes
   - This is expected and not an error

2. **Dormant Enemies & New Passives**
   - New passive skills can affect character behavior and dungeon outcomes
   - Dormant enemies may wake with new passive effects
   - Autoplay simulations may produce different results on the same seed
   - This is expected behavior with Phase 7 content

3. **Rowan Default Class**
   - The protagonist (Rowan) now defaults to Warrior class
   - Rowan starts with "Warrior Novice Training" passive skill
   - Manual play will show Warrior tier-1 skills in the trainer on first playthrough
   - Changing the default requires regenerating class content

4. **Status Effect Stacking**
   - Some status effects use stack keys to merge instead of coexist (e.g., Frailty stacks)
   - Status duration bonuses from passives affect applied status effects
   - Duration calculations may differ from Phase 6 due to new passive effects

## Manual Play Checklist

Test the following gameplay scenarios to verify Phase 7 content works correctly:

1. **New Game + Class Selection**
   - Start a new game
   - Verify that you can select from all 10 classes
   - Each class should show its Novice Training mastery skill (1 skill to learn, no cost)
   - Confirm you can learn one skill per class

2. **Trainer - Skills Display**
   - After learning the first skill, enter a town
   - Speak to the Trainer
   - Verify the trainer shows only that class's tier-1 skills with correct costs
   - For Warrior: should show Double Strike, Vanguard, Power Boost, Iron Skin (5 cost each)
   - For Archer: should show Aimed Shot, Pinning Shot, Disarming Shot, Stunning Shot, Keen Eye (5 cost each)
   - Skill names and costs should match `ClassContentExpectations.Rows`

3. **Dungeon - Skill Usage**
   - Enter a dungeon
   - Use a learned active skill
   - Verify skill works as designed:
     - SP is consumed correctly
     - Damage calculations use the new mastery bonuses
     - Status effects apply as configured
   - For Archer skills: verify arrow consumption if applicable

4. **Multi-Class Heroes**
   - Recruit a combination hero (e.g., Blake: Warrior/Guardian, Elara: Healer/Elementalist)
   - Verify both classes' tier-1 and tier-2 skills appear
   - For secondary class: verify max rank is capped at 3 (not 5)
   - Verify passives from both classes apply

5. **Status Effects**
   - Test status effects introduced in Phase 7:
     - Apply follow-up marks (Fire Mark, Ice Mark, Lightning Mark)
     - Verify follow-up attacks trigger on party hits
     - Test parry, damage reduction, damage shield, endure, amplify, stealth
   - Verify status durations use passive bonuses correctly

## Balance Checklist (User)

After manual play verification, run balance tests:

1. **Autoplay Across Class Compositions**
   - Run Autoplay with different party compositions:
     - Physical Heavy: Warrior, Guardian, Healer, Elementalist
     - Ranged Heavy: Archer, Scout, Bard, Rogue
     - Mixed: Commander, Occultist, Healer, Warrior
   - Run each composition on multiple seeds (at least 3 different seeds)
   - Monitor dungeon progression and victory rates
   - Note if any class feels overpowered or underpowered

2. **Skill Cost Tuning**
   - If skills feel too cheap or expensive, adjust:
     - `Cost(tier)` in `ClassContent_*.cs` generators
     - Regenerate and re-run tests

3. **Damage Scaling Adjustments**
   - If damage feels imbalanced, tune:
     - `DamageBonus` passive `Percent` values
     - Strike multipliers in active skills
     - `PercentPerRank` scaling values
   - Test in Autoplay to see impact

4. **Class Growth Tuning**
   - If character growth feels slow or fast:
     - Adjust `ClassDefinition.GrowthPerLevel` values
     - Modify `ClassContent_*.cs` `.Save()` starting bonuses
   - Regenerate and re-run tests

5. **Enemy Resistance & Difficulty**
   - If enemies feel too easy or hard:
     - Check `TuneEnemyResistances` in `ClassContentBuilder`
     - Adjust enemy base stats and resistance values
   - Verify Autoplay win rates are in a reasonable range (40-80% depending on composition)

6. **Trainer Costs**
   - If learning progression feels too slow or fast:
     - Adjust `LearnCost` values in `ClassContentBuilder`
     - Regenerate and verify new costs in Trainer UI

## Validation Summary

This phase adds 180+ new skills (20 skills × 10 classes) and 8 status effects. The verification ensures:

- ✓ All class content generates correctly without errors
- ✓ Skill names, tiers, kinds, and max ranks match specifications
- ✓ Object initializers reference valid classes and fields
- ✓ No duplicate type names introduced
- ✓ No FinalStats reads inside ConditionalStats
- ✓ Editor code doesn't leak into runtime assemblies
- ✓ MonoBehaviour status classes one-per-file with matching names
- ✓ All tests pass (210 Core tests + Harness tests)
- ✓ Manual play works as designed
- ✓ Balance is reasonable across different party compositions

After this checklist is complete, Phase 7 implementation is verified and ready for gameplay testing.
