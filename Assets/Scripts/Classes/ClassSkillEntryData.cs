using System;
using EternalEnigma.Core.Classes;
using UnityEngine;

// One skill in a class's kit. Tier/MaxRank/Kind live here, not on the Skill asset,
// so a skill shared by two classes can sit at different tiers.
[Serializable]
public class ClassSkillEntryData
{
	public Skill Skill;
	[Range(1, 3)] public int Tier = 1;
	[Range(1, 5)] public int MaxRank = 5;
	public SkillKind Kind;
}
