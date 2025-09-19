using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillAnimation
{
    [SerializeReference]
    public List<ActionStep> Steps = new();

    internal IEnumerator ExecuteRoutine(Character caster, Character target)
    {
        foreach (var step in Steps)
        {
            if (step != null)
            {
                // Run each step in sequence
                yield return step.Execute(caster, target);
            }
        }
    }
}

[System.Serializable]
public abstract class ActionStep
{
    public abstract IEnumerator Execute(Character caster, Character target);
}

public class ParticleActionStep : ActionStep
{
    public GameObject MuzzlePrefab;
    public GameObject ProjectilePrefab;
    public GameObject ImpactPrefab;

    public float delay;
    public float duration;

	public override IEnumerator Execute(Character caster, Character target)
	{
        var originPos = caster.transform.position;
        var targetPos = target.transform.position;

        if (MuzzlePrefab != null)
        {
            var muzzle = Object.Instantiate(MuzzlePrefab, originPos, Quaternion.identity);
            Object.Destroy(muzzle, delay);
        }

        if (ProjectilePrefab != null)
        {
            var proj = Object.Instantiate(ProjectilePrefab, originPos, Quaternion.identity);

            // Tween to target
            yield return proj.transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear)
                .WaitForCompletion();

            Object.Destroy(proj);
        }

        if (ImpactPrefab != null)
        {
            var impact = Object.Instantiate(ImpactPrefab, targetPos, Quaternion.identity);
            Object.Destroy(impact, delay);
        }
    }
}

public class WaitActionStep : ActionStep
{
    [Min(0f)]
    public float delaySeconds;
	public override IEnumerator Execute(Character caster, Character target)
	{
        yield return new WaitForSeconds(delaySeconds);
	}
}

public class PlayAnimationStep : ActionStep
{
    public string AnimationName;
    public override IEnumerator Execute(Character caster, Character target)
    {
        caster.PlayAttackAnimation(); //placeholder for now
        yield return null;

        //if (string.IsNullOrEmpty(AnimationName))
        //yield break;

        //caster.PlayAnimation(AnimationName);

        //if (WaitForCompletion)
        //{
        //    // naive approach: wait until current state's length
        //    var stateInfo = caster.Animator.GetCurrentAnimatorStateInfo(0);
        //    yield return new WaitForSeconds(stateInfo.length);
        //}
    }
}