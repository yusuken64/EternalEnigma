using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Presentation-only looping animation. Never drives gameplay or root motion.</summary>
public sealed class MenuSceneMotion : MonoBehaviour
{
    public AnimationClip Clip;
    public Animator Animator;
    public float Phase;
    public float Speed = 1;
    public float Hover;
    public float Wobble;
    private Vector3 restPosition, restScale;
    private PlayableGraph graph;

    private void OnEnable()
    {
        restPosition = transform.localPosition;
        restScale = transform.localScale;
        if (Animator == null || Clip == null) return;
        Animator.applyRootMotion = false;
        graph = PlayableGraph.Create("Menu idle");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var clip = AnimationClipPlayable.Create(graph, Clip);
        clip.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(graph, "Visual", Animator);
        output.SetSourcePlayable(clip);
        graph.Play();
    }

    private void LateUpdate() => Sample(Time.unscaledTime);

    public void Sample(float seconds)
    {
        float time = seconds * Speed + Phase;
        if (graph.IsValid())
        {
            graph.GetRootPlayable(0).SetTime(Mathf.Repeat(time, Clip.length));
            graph.Evaluate(0);
        }
        float wave = Mathf.Sin(time * 2);
        transform.localPosition = restPosition + Vector3.up * (wave * Hover);
        transform.localScale = Vector3.Scale(restScale, new Vector3(1 + wave * Wobble, 1 - wave * Wobble, 1 + wave * Wobble));
    }

    private void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
        transform.localPosition = restPosition;
        transform.localScale = restScale;
    }
}
