using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverworldMenu : MonoBehaviour
{
	public StatueDialog StatueDialog;
	public ShopMenuDialog ShopDialog;
	public AllyRecruitDialog AllyRecruitDialog;
	public OverworldHelpDialog OverworldHelpDialog;

	private void Start()
	{
		StatueDialog.gameObject.SetActive(false);
		ShopDialog.gameObject.SetActive(false);
		AllyRecruitDialog.gameObject.SetActive(false);
		OverworldHelpDialog.gameObject.SetActive(false);
	}
}
