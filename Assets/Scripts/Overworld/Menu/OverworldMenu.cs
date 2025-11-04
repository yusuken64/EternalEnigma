using JuicyChickenGames.Menu;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OverworldMenu : MonoBehaviour
{
	public StatueDialog StatueDialog;
	public ShopMenuDialog ShopDialog;
	public AllyRecruitDialog AllyRecruitDialog;
	public BallistaDialog BallistaDialog;
	public EntranceDialog EntranceDialog;

	public InventoryMenu InventoryMenu;
	public SkillDialog SkillDialog;

	private void Start()
	{
		StatueDialog.gameObject.SetActive(false);
		ShopDialog.gameObject.SetActive(false);
		AllyRecruitDialog.gameObject.SetActive(false);
		BallistaDialog.gameObject.SetActive(false);
		SkillDialog.gameObject.SetActive(false);
	}
}
