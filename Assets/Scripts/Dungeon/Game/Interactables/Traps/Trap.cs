using System.Collections.Generic;
using UnityEngine;

public abstract class Trap : Interactable
{
	public GameObject VisualObject;
	// Revelation is gameplay state, independent of fog/camera visibility of the parent.
	internal bool CanTrigger(Character character) => character != null &&
		(character is not Enemy || (VisualObject != null && VisualObject.activeSelf));
	internal abstract List<GameAction> GetTrapSideEffects(Character character);
	internal override List<GameAction> GetInteractionSideEffects(Character character)
	{
		return GetTrapSideEffects(character);
	}
}
