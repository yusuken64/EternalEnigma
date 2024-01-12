using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverworldMenu : MonoBehaviour
{
	public StatueDialog StatueDialog;

	private void Start()
	{
		StatueDialog.gameObject.SetActive(false);
	}
}
