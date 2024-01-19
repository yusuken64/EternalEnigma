using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
	public List<Skill> SkillPrefabs;

	internal Skill GetSkillByName(string skillName)
	{
		return SkillPrefabs.FirstOrDefault(x => x.SkillName == skillName);
	}
}
