using UnityEngine;

public class CameraController : MonoBehaviour
{
	public Camera Camera;
	// The dungeon lies in XY, with negative Z above the floor.
	// Keep the elevated viewpoint fixed while following the selected character.
	public Vector3 CameraOffset = new Vector3(0f, -12f, -14f);

    private void Start()
    {
        // Preserve the old vertical XY ground span for serialized scene offsets too.
        var next = new Vector3(0,-12,-14);
        if (Camera != null && Camera.orthographic && CameraOffset.sqrMagnitude > .01f && Mathf.Abs(CameraOffset.z) > .01f)
            Camera.orthographicSize *= (Mathf.Abs(next.z)/next.magnitude)/(Mathf.Abs(CameraOffset.z)/CameraOffset.magnitude);
        CameraOffset = next;
    }

	public Transform _followTarget;
    private Transform presentationTarget;
    private Vector2 presentationViewport = new(.5f, .5f);

    internal void FramePresentation(Transform target, Vector2 viewport)
    {
        presentationTarget = target;
        presentationViewport = viewport;
        SnapToFollowTarget();
    }

    internal void ClearPresentation(Transform target)
    {
        if (presentationTarget != target) return;
        presentationTarget = null;
        SnapToFollowTarget();
    }

	public void SetFollowTarget(Transform target)
	{
		_followTarget = target;
	}
	private void LateUpdate()
	{
		if (_followTarget != null && presentationTarget == null) CameraZoom.Update(Camera);
		SnapToFollowTarget();
	}

	internal void SnapToFollowTarget()
	{
		if (ScreenTransition.ControlsCamera(Camera)) return;
		var target = presentationTarget != null ? presentationTarget : _followTarget;
        if (target != null && Camera != null)
		{
			var rotation = Quaternion.LookRotation(-CameraOffset, Vector3.up);
            float height = Camera.orthographic ? Camera.orthographicSize * 2 :
                2 * CameraOffset.magnitude * Mathf.Tan(Camera.fieldOfView * Mathf.Deg2Rad * .5f);
            var viewport = presentationTarget != null ? presentationViewport - new Vector2(.5f, .5f) : Vector2.zero;
            var shift = rotation * new Vector3(viewport.x * height * Camera.aspect, viewport.y * height, 0);
            Camera.transform.SetPositionAndRotation(target.position + CameraOffset - shift, rotation);
		}
	}
}
