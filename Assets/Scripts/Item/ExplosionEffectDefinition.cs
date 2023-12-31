using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ExplosionEffectDefinition", menuName = "Game/ItemEffect/ExplosionEffectDefinition")]
public class ExplosionEffectDefinition : ItemEffectDefinition
{
	public int Damage;
	public GameObject ExplosionParticleEffectPrefab;

	public override List<GameAction> GetGameActions(Character attacker, Character target, InventoryItem item)
	{
		return new List<GameAction>()
		{
			new ExplosionAction(attacker, target, Damage, ExplosionParticleEffectPrefab)
		};
	}
}
