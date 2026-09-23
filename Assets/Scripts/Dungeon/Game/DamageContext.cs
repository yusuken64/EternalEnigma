public sealed class DamageContext
{
	public DamageContext(Character attacker, Character target, int damage, DamageElement element, bool missed, int responseDepth)
	{
		Attacker = attacker;
		OriginalTarget = target;
		Target = target;
		Damage = damage;
		Element = element;
		Missed = missed;
		ResponseDepth = responseDepth;
	}

	public Character Attacker { get; }
	public Character OriginalTarget { get; }
	public Character Target { get; set; }     // interceptors may redirect (e.g. Cover)
	public int Damage { get; set; }           // interceptors may reduce (e.g. barriers)
	public DamageElement Element { get; }
	public bool Missed { get; set; }          // interceptors may block (e.g. Parry)
	public int ResponseDepth { get; }
	public bool Redirected => Target != OriginalTarget;
}

public static class DamageResponses
{
	// Responses to a primary hit have depth 1; responses to responses are not allowed.
	public const int MaxResponseDepth = 1;
	public static bool CanRespond(TakeDamageAction action) => action != null && action.ResponseDepth < MaxResponseDepth;
}
