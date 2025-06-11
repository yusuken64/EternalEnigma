using UnityEngine;

public class CameraController : MonoBehaviour
{
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

	public Transform _followTarget;

	public void SetFollowTarget(Transform target)
	{
		_followTarget = target;
	}

	private void LateUpdate()
	{
		if (_followTarget != null)
		{
			Camera.transform.position = _followTarget.position + CameraOffset;
		}
	}
}