using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TownAlly : TownCharacter
{
	public string Name;
	public string Id;
	public string Description;

	public GameObject AnimatedModel;

	public List<string> Skills;
	// Fixed per hero prefab (assigned in Phase 7). The protagonist's instance is overwritten from the save.
	public ClassDefinition PrimaryClass;
	public ClassDefinition SecondaryClass;
	public int RecruitCost { get; internal set; }
	private void Awake()
	{
		if (HeroAnimator?.Animator != null) HeroAnimator.Animator.applyRootMotion = false;
		Skills ??= new();
	}
	public void RefreshEquipmentVisuals() => HeroAnimator?.SetWeapon(
		Equipment.EquippedWeapon?.EquipmentItemDefinition, Equipment.EquippedShield?.EquipmentItemDefinition);

	public SpriteRenderer CirlcleRenderer;
	public Color AllyColor;
	public Color PlayerColor;
	internal void SetToCPU()
	{
		CirlcleRenderer.color = AllyColor;
	}

	internal void SetToPlayer()
	{
		CirlcleRenderer.color = PlayerColor;
	}
}
