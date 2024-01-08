using System.Collections.Generic;
using UnityEngine;

public abstract class Trap : Interactable
{
	public GameObject VisualObject;
	internal abstract List<GameAction> GetTrapSideEffects(Character character);
}
