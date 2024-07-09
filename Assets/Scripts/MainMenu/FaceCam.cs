using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaceCam : MonoBehaviour
{
	public GameObject ObjectToFollow;
	public Vector3 LookAtOffset;
	public Vector3 Offset;
	public GameObject FaceLight;

	private void Start()
	{
		FaceLight.gameObject.SetActive(false);
	}

	void LateUpdate()
	{
		if (ObjectToFollow == null) { return; }

		// Calculate the desired position based on the character's position and the offset
		Vector3 desiredPosition = ObjectToFollow.transform.position + ObjectToFollow.transform.TransformDirection(Offset);
		transform.position = desiredPosition;
		var transformLookAtOffset = ObjectToFollow.transform.TransformDirection(LookAtOffset);
		Vector3 upVector = ObjectToFollow.transform.up; // Use the character's local up direction
		transform.LookAt(ObjectToFollow.transform.position + transformLookAtOffset, upVector);
	}

	internal void SetFollow(GameObject newObjectToFollow)
	{
		ObjectToFollow = newObjectToFollow;
		FaceLight.gameObject.SetActive(true);
	}

	internal void Unfollow()
	{
		FaceLight.gameObject.SetActive(false);
	}
}
