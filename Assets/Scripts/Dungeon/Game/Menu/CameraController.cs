using UnityEngine;

public class CameraController : MonoBehaviour
{
	public Camera Camera;
	// The dungeon lies in XY, with negative Z above the floor.
	// Keep the elevated viewpoint fixed while following the selected character.
	public Vector3 CameraOffset = new Vector3(0f, -10f, -14f);

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
