using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillAnimation
{
    public List<ActionStep> Steps = new();

    internal IEnumerator ExecuteRoutine(Character caster)
    {
        var originalRotation = caster.VisualParent.transform.rotation;

        foreach (var step in Steps)
        {
            switch (step.StepType)
            {
                case ActionStepType.Rotate:
                    caster.VisualParent.transform
                        .DORotate(step.Vector, step.Duration, RotateMode.FastBeyond360)
                        .SetRelative(step.Relative);
                    break;

                case ActionStepType.PunchScale:
                    caster.VisualParent.transform
                        .DOPunchScale(step.Vector, step.Duration, 50);
                    break;

                case ActionStepType.PlayAnimation:
                    //caster.PlayAnimation(step.AnimationName);
                    break;

                case ActionStepType.SpawnParticles:
                    var particle = GameObject.Instantiate(step.ParticlePrefab, caster.transform);
                    particle.transform.position = Game.Instance.CurrentDungeon.CellToWorld(caster.TilemapPosition);
                    GameObject.Destroy(particle, step.Duration);
                    break;

                case ActionStepType.Wait:
                    yield return new WaitForSecondsRealtime(step.Duration);
                    break;

                case ActionStepType.ResetRotation:
                    caster.VisualParent.transform.rotation = originalRotation;
                    break;
            }
        }
    }
}

[System.Serializable]
public class ActionStep
{
    public ActionStepType StepType;

    // common / optional parameters
    public Vector3 Vector;        // e.g. rotation amount, punch scale
    public float Duration;        // how long to animate/wait
    public string AnimationName;  // "Attack", "Idle"
    public GameObject ParticlePrefab;
    public bool Relative;         // rotate relative?
}

public enum ActionStepType
{
    Rotate,
    PunchScale,
    PlayAnimation,
    SpawnParticles,
    Wait,
    ResetRotation
}
