using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class ExplosionAction : GameAction
{
	private Character attacker;
	private Character target;
	private int damage;
	private GameObject explosionParticleEffectPrefab;

	public ExplosionAction(Character attacker, Character target, int damage, GameObject explosionParticleEffectPrefab)
	{
		this.attacker = attacker;
		this.target = target;
		this.damage = damage;
		this.explosionParticleEffectPrefab = explosionParticleEffectPrefab;
	}

	internal override List<GameAction> ExecuteImmediate(Character character)
	{
		var game = Game.Instance;

		var inRange = game.AllCharacters.Where(other =>
			other.TilemapPosition.x >= attacker.TilemapPosition.x - 10 &&
			other.TilemapPosition.x <= attacker.TilemapPosition.x + 10 &&
			other.TilemapPosition.y >= attacker.TilemapPosition.y - 10 &&
			other.TilemapPosition.y <= attacker.TilemapPosition.y + 10);

		var explosionDamage = inRange.Select(x => new TakeDamageAction(character, x, this.damage, true, false))
			.ToList();

		return new(explosionDamage);
	}

	internal override IEnumerator ExecuteRoutine(Character character, bool skipAnimation = false)
    {
		var explosion = UnityEngine.Object.Instantiate(explosionParticleEffectPrefab);
		explosion.transform.position = attacker.transform.position;
		UnityEngine.Object.Destroy(explosion.gameObject, 5f);

		yield return new WaitForSecondsRealtime(0.5f);
	}

	internal override bool IsValid(Character character)
	{
		return true;
	}
}