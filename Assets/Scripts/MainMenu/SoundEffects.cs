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
	public AudioClip DoorOpen, GateUnlock, Purchase, Sell, Wait, Rest, Gather, ContainerOpen, Trap, Throw, Status, Revive;

    public AudioClip For(SoundCue cue) => cue switch
    {
        SoundCue.Navigate => Hover, SoundCue.Confirm => Confirm, SoundCue.Cancel => Decline,
        SoundCue.Denied => Denied, SoundCue.MenuOpen => Pause, SoundCue.MenuClose => Unpause,
        SoundCue.Attack => Slash, SoundCue.Damage => Impact_flesh, SoundCue.Heal => Impact_heal,
        SoundCue.Miss => Miss_Evade, SoundCue.Block => Block, SoundCue.Death => Enemy_death,
        SoundCue.UseItem => UseItem, SoundCue.Equip => Equip, SoundCue.Unequip => Unequip,
        SoundCue.Pickup => BuySell, SoundCue.Drop => Unequip, SoundCue.Purchase => Purchase,
        SoundCue.Sell => Sell, SoundCue.DoorOpen => DoorOpen, SoundCue.GateUnlock => GateUnlock,
        SoundCue.LevelUp => LevelUp, SoundCue.Wait => Wait, SoundCue.Rest => Rest,
        SoundCue.Gather => Gather, SoundCue.ContainerOpen => ContainerOpen, SoundCue.Trap => Trap,
        SoundCue.Throw => Throw, SoundCue.Status => Status, SoundCue.Revive => Revive,
        SoundCue.Teleport => Teleport, SoundCue.Retreat => Flee, SoundCue.AbilityStart => AbilityCast,
        _ => null
    };

    public static bool IsUI(SoundCue cue) => cue <= SoundCue.MenuClose;
    public static float Gain(SoundCue cue) => cue switch
    {
        SoundCue.Navigate => .12f, SoundCue.Wait => .10f, SoundCue.Rest => .22f,
        SoundCue.Denied => .28f, SoundCue.Status => .28f, SoundCue.AbilityStart => .32f,
        SoundCue.Confirm or SoundCue.Cancel or SoundCue.MenuOpen or SoundCue.MenuClose => .36f,
        SoundCue.Pickup or SoundCue.Drop or SoundCue.Equip or SoundCue.Unequip => .42f,
        SoundCue.GateUnlock or SoundCue.LevelUp or SoundCue.Revive => .72f,
        _ => .6f
    };
}
