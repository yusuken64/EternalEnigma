using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AloofPolicyOverride : PolicyOverride
{
	public override PolicyBase GetOverridePolicy(Game game, Enemy enemy)
	{
		return new AloofPolicy(game, enemy, Priority);
	}
}
