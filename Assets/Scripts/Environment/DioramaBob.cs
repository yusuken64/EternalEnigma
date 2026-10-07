using UnityEngine;

/// <summary>Small visual motion on a TWC-owned service marker; no gameplay state.</summary>
public sealed class DioramaBob : MonoBehaviour
{
    public float Phase;
    Vector3 origin;
    void Start()=>origin=transform.localPosition;
    void Update()=>transform.localPosition=origin+Vector3.back*(Mathf.Sin(Time.unscaledTime*2+Phase)*.06f);
}
