using System.Collections;
using System.Collections.Generic;

public abstract class GameActionResponse : GameAction
{
	//returns true if this gameaction response activates in response to the given action
	public abstract List<GameAction> GetResponseTo(Character character, GameAction gameAction);

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return new();
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		yield return null;
	}

	internal override bool IsValid(Character character)
	{
		//TODO: only if there are valid spawn position
		return true;
	}
}
