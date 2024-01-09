using System.Collections;
using UnityEngine;

public abstract class PolicyOverride : MonoBehaviour
{
	public int Priority;
	public abstract PolicyBase GetOverridePolicy(Game game, Character enemy);
}
