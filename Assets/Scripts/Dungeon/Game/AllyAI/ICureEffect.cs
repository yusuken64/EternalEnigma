// Implemented by skill effects that remove status effects; the ally AI reads it to find cure skills.
public interface ICureEffect
{
	bool Cures(StatusEffect status);
}
