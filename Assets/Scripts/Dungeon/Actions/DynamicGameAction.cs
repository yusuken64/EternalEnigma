using System;
using System.Collections;
using System.Collections.Generic;

internal class DynamicGameAction : GameAction
{
	private Func<Character, List<GameAction>> executionImmediateFunc;
	private Func<IEnumerator> executeRoutineFunc;
	private Func<bool> isValidFunc;

	public DynamicGameAction(
		Func<Character, List<GameAction>> executionImmediateFunc, 
		Func<IEnumerator> executeRoutineFunc,
		Func<bool> isValidFunc)
	{
		this.executionImmediateFunc = executionImmediateFunc;
		this.executeRoutineFunc = executeRoutineFunc;
		this.isValidFunc = isValidFunc;
	}


	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		return executionImmediateFunc?.Invoke(character);
	}

	internal override IEnumerator ExecuteRoutine(Character character)
	{
		yield return executeRoutineFunc?.Invoke();
	}

	internal override bool IsValid(Character character)
	{
		return isValidFunc == null ? true : isValidFunc();
	}
}