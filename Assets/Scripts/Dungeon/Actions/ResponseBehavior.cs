using UnityEngine;

public abstract class ResponseBehavior : MonoBehaviour
{
	public abstract GameActionResponse GetActionResponse(GameAction gameAction);
}
