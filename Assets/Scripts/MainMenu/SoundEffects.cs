using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EternalEnigma.Core.World;

public class SoundEffects : MonoBehaviour
{
	//Battle_SFX
	public AudioClip Claw;
	public AudioClip Bite;
	public AudioClip Impact_flesh;
	public AudioClip Impact_heal;
	public AudioClip Slash;
	public AudioClip Miss_Evade;
	public AudioClip Block;
	public AudioClip Flee;
	public AudioClip Encounter;
	public AudioClip Enemy_death;
	public AudioClip flesh;

	//UI_Menu_SFX
	public AudioClip Hover;
	public AudioClip Confirm;
	public AudioClip Decline;
	public AudioClip Denied;
	public AudioClip UseItem;
	public AudioClip Equip;
	public AudioClip Unequip;
	public AudioClip BuySell;
	public AudioClip Pause;
	public AudioClip Unpause;

	//Player_Movement_SFX
	public AudioClip StepGrass, StepRock, StepWood, StepWater, Jump, Landing, Ambush;
	public AudioClip StepFor(OverworldBiome biome) => biome switch
	{
		OverworldBiome.Water or OverworldBiome.Marsh => StepWater,
		OverworldBiome.Mountain or OverworldBiome.Volcanic or OverworldBiome.Tundra or OverworldBiome.Desert => StepRock,
		_ => StepGrass
	};
	//Atk_Magic_SFX
	//Buffs_Heals_SFX
	public AudioClip Sleep;
	public AudioClip Debuff;
	public AudioClip Teleport;
	public AudioClip AbilityCast;
	public AudioClip LevelUp;
}
