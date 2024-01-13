using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverworldMenu : MonoBehaviour
{
	public StatueDialog StatueDialog;
	public ShopMenuDialog ShopDialog;

	private void Start()
	{
		StatueDialog.gameObject.SetActive(false);
		ShopDialog.gameObject.SetActive(false);
	}
}
