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

	public void SetFollowTarget(Transform target)
	{
		_followTarget = target;
	}
	private void LateUpdate()
	{
		SnapToFollowTarget();
	}

	internal void SnapToFollowTarget()
	{
		if (_followTarget != null && Camera != null)
		{
			Camera.transform.position = _followTarget.position + CameraOffset;
			Camera.transform.LookAt(_followTarget);
		}
	}
}
