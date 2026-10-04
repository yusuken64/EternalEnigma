using UnityEngine;

/// <summary>Deterministic phase offsets on authored loops; placement transforms never animate.</summary>
public sealed class TownAmbientAnimation : MonoBehaviour
{
    public float Phase;
    private void Start()
    {
        var animator=GetComponent<Animator>();
        if(animator!=null) { animator.Play("Idle",0,Phase); animator.Update(0); }
    }
}
