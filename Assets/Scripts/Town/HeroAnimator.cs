using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class HeroAnimator : MonoBehaviour
{
	public Animator Animator;

	public List<StanceAnimation> StanceAnimations;

	public List<GameObject> RightHandObjects;
	public List<GameObject> LeftHandObjects;

	public Stance CurrentStance;

	public List<StanceAnimators> StanceAnimators; //used in setanimations
	private bool walking;
	private bool walkStep;
	private bool continueWalk;
	private Stance walkStance;

	internal void BeginWalk(bool continuous)
	{
		if (!continuous || !walking || !continueWalk || walkStance != CurrentStance)
			PlayWalkAnimation();
		walking = true;
		walkStep = true;
		continueWalk = continuous;
		walkStance = CurrentStance;
	}

	internal void CompleteWalk(bool retain)
	{
		walkStep = false;
		if (walking && (!retain || !continueWalk)) PlayIdleAnimation();
	}

	// Cancellation during a step finishes the square. Only retained walking may
	// be idled here; a hit, attack or death has already cleared this state.
	internal void StopWalkContinuation()
	{
		continueWalk = false;
		if (walking && !walkStep) PlayIdleAnimation();
	}

	internal void StopRetainedWalk()
	{
		// A concurrent swap can still be moving this hero during their wait.
		if (!walkStep) StopWalkContinuation();
	}

	private void OnDisable() => walking = walkStep = continueWalk = false;

	internal void PlayIdleAnimation()
	{
		PlayAnimation(AnimatedAction.Idle);
	}

	internal void PlayWalkAnimation()
	{
		PlayAnimation(AnimatedAction.MoveFWD);
	}

	internal void PlayAttackAnimation()
	{
		PlayAnimation(AnimatedAction.Attack);
	}

	internal void PlayTakeDamageAnimation()
	{
		PlayAnimation(AnimatedAction.GetHit);
	}

	internal void PlayDeathAnimation()
	{
		PlayAnimation(AnimatedAction.Die);
	}

	public void PlayAnimation(AnimatedAction animatedAction)
	{
		walking = walkStep = continueWalk = false;
		StanceAnimation stanceAnimation = StanceAnimations.First(x => x.Stance == CurrentStance);
		NamedAnimation namedAnimation = stanceAnimation.NamedAnimations.First(x => x.AnimationAction == animatedAction);
		AnimationClip animationClip = namedAnimation.Animations.Sample();
		Animator.runtimeAnimatorController = stanceAnimation.AnimatorController;
		Animator.Play(animationClip.name, 0);
	}

	internal void SetWeapon(EquipmentItemDefinition mainHandItemDefinition, EquipmentItemDefinition offHandItemDefinition)
	{
		StopWalkContinuation();
		// The bow model is equipped in the left hand in the shipped item catalog.
		if (mainHandItemDefinition?.WeaponType == WeaponType.BowAndArrow && !mainHandItemDefinition.IsAmmunition ||
			offHandItemDefinition?.WeaponType == WeaponType.BowAndArrow && !offHandItemDefinition.IsAmmunition)
		{
			CurrentStance = Stance.BowAndArrowStance;
		}
		else if (offHandItemDefinition?.WeaponType == WeaponType.OffhandShield)
		{
			CurrentStance = Stance.SwordAndShield;
		}
		else if (offHandItemDefinition?.IsOffhandMeleeWeapon == true)
		{
			CurrentStance = Stance.DoubleSwordStance;
		}
		else if (mainHandItemDefinition == null)
		{
			CurrentStance = Stance.NoWeapon;
		}
		else if (mainHandItemDefinition.WeaponType == WeaponType.TwoHandSword)
		{
			CurrentStance = Stance.TwoHandSword;
		}
		else if (mainHandItemDefinition.WeaponType == WeaponType.Spear)
		{
			CurrentStance = Stance.Spear;
		}
		else if (mainHandItemDefinition.WeaponType == WeaponType.MagicWand)
		{
			CurrentStance = Stance.MagicWand;
		}
		//else if (mainHandItemDefinition.WeaponType == WeaponType.SingleSword)
		else
		{
			CurrentStance = Stance.SingleSword;
		}

		bool bow = mainHandItemDefinition?.WeaponType == WeaponType.BowAndArrow && mainHandItemDefinition.IsAmmunition == false;
        RightHandObjects.ForEach(x => x.gameObject.SetActive(x.name == (bow ? offHandItemDefinition?.WeaponModelName : mainHandItemDefinition?.WeaponModelName)));
        LeftHandObjects.ForEach(x => x.gameObject.SetActive(x.name == (bow ? mainHandItemDefinition?.WeaponModelName : offHandItemDefinition?.WeaponModelName)));
	}


#if UNITY_EDITOR
	[ContextMenu("Find weapon Objects")]
	public void FindWeaponObjects()
	{
		RightHandObjects.Clear();
		LeftHandObjects.Clear();

		var rightHand = transform.FindChildRecursively("weapon_r");
		foreach(Transform child in rightHand)
		{
			RightHandObjects.Add(child.gameObject);
			Debug.Log(child.name, child);
		}
		var leftHand = transform.FindChildRecursively("weapon_l");
		foreach(Transform child in leftHand)
		{
			LeftHandObjects.Add(child.gameObject);
			Debug.Log(child.name, child);
		}

		SetWeapon(null, null);

		EditorUtility.SetDirty(this.gameObject);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
	}

	[ContextMenu("Set Animations")]
	public void SetAnimations()
	{
		StanceAnimations.Clear();
		var stances = EnumExtensions.GetEnumValues<Stance>();
		var animatedAction = EnumExtensions.GetEnumValues<AnimatedAction>();

		foreach (var stance in stances)
		{
			StanceAnimations.Add(new StanceAnimation()
			{
				Stance = stance,
				StanceName = stance.ToString(),
				AnimatorController = StanceAnimators.First(x => x.Stance == stance).AnimatorController,
				NamedAnimations = animatedAction.Select(x => new NamedAnimation()
				{
					AnimationAction = x,
					AnimationName = x.ToString(),
					Animations = new()
				}).ToList()
			});
		}

		foreach (StanceAnimation stanceAnimation in StanceAnimations)
		{
			AnimationClip[] clips = stanceAnimation.AnimatorController.animationClips;

			foreach (var clip in clips)
			{
				var namedAnimation = stanceAnimation.NamedAnimations.FirstOrDefault(x => clip.name.Contains(x.AnimationName));
				if (namedAnimation != null)
				{
					namedAnimation.Animations.Add(clip);
				}
			}
		}
	}
#endif
}

[Serializable]
public class StanceAnimators
{
	public Stance Stance;
	public RuntimeAnimatorController AnimatorController;
}

[Serializable]
public class StanceAnimation
{
	public string StanceName;
	public Stance Stance;
	public RuntimeAnimatorController AnimatorController;
	public List<NamedAnimation> NamedAnimations;
}

[Serializable]
public class NamedAnimation
{
	public string AnimationName;
	public AnimatedAction AnimationAction;
	public List<AnimationClip> Animations;
}

public enum AnimatedAction
{
	Idle,
	Attack,
	Combo,
	MoveFWD,
	Dash,
	Jump,
	Dance,
	LevelUp,
	Victory,
	Defend,
	GetHit,
	Die,
	Dizzy,
}

public enum Stance
{
	NoWeapon,
	SingleSword,
	SwordAndShield,
	Spear,
	DoubleSwordStance,
	BowAndArrowStance,
	TwoHandSword,
	MagicWand
}
