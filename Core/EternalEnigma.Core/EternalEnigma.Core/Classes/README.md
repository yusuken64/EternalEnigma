# Classes

Home for hero class skill tables and the skill learning rules. `ClassSkillTable`
lists a class's skills with their tier (1–3), maximum rank and kind (normal,
mastery, gathering or single-rank). `ClassKit` is a hero's fixed primary class
plus an optional secondary class. `SkillLearningRules` is the only evaluator for
what a hero may learn next: tier unlocks through the primary's mastery skills,
the rank level gate, rank cost and the secondary-class caps (tiers 1–2, rank 3,
no secondary masteries). `ClassKitValidator` reports authoring errors. Skill
effects, stats and assets belong to the Unity adapters; no Unity types here.
See `Docs/Classes.md` in the repository root for the design.
