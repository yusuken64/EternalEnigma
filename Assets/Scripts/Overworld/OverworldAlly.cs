using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverworldAlly : OverworldCharacter
{
	public string Name;
	public string Description;

	public GameObject AnimatedModel;

	public List<string> Skills;

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
