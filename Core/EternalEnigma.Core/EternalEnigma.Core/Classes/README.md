# Classes

`ClassSkillTable`, `ClassKit` and `LearnedSkill` describe fixed primary/secondary kits.
`SkillLearningRules` evaluates tier/mastery prerequisites, rank/level caps and skill-point
costs. Two points are earned per highest level; rank n costs n points. Secondary skills stop
at tier 2/rank 3 and exclude masteries. `ClassKitValidator` checks content, while
`SkillTreeDocument`/`SkillTreeFormat` support text editing. Effects and Unity assets remain
outside Core. See [current classes](../../../../Docs/Classes.md).
