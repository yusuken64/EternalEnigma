using JuicyChickenGames.Menu;
using UnityEngine;

public class CameraController : MonoBehaviour
{
	public Camera Camera;
	public Vector3 CameraOffset = new Vector3(1, -7.23999977f, -11.0200005f);

	[Header("Controls")]
	public float verticalSpeed = 5f;
	public float horizontalSpeed = 5f;
	public float minZ = -20f;
	public float maxZ = -5f;

	public float minX = -0.1f;
	public float maxX = 0.1f;

	public Transform _followTarget;

	public void SetFollowTarget(Transform target)
	{
		_followTarget = target;
	}
	private void Update()
	{
		float inputX = PlayerInputHandler.Instance.lookInput.x;
		float inputY = PlayerInputHandler.Instance.lookInput.y;

		CameraOffset.z = Mathf.Clamp(CameraOffset.z + inputY * verticalSpeed * Time.deltaTime, minZ, maxZ);
		CameraOffset.x = Mathf.Clamp(CameraOffset.x + inputX * horizontalSpeed * Time.deltaTime, minX, maxX);
	}

	private void LateUpdate()
	{
		if (_followTarget != null)
		{
			Camera.transform.position = _followTarget.position + CameraOffset;
			Camera.transform.LookAt(_followTarget);
		}
	}
}