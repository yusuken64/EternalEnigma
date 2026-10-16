using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterStatsDisplay : MonoBehaviour
{
	public TextMeshProUGUI NameText;
	public Image PortraitImage;
	public StatsDisplay LevelDisplay;
	public StatsDisplay HpDisplay;
	public StatsDisplay ManaDisplay;
	public StatsDisplay HungerDisplay;

	public Character Character;
	private string baseName;

	internal void Setup(Character character)
	{
		GetComponent<DungeonPartyCard>().Bind(this);
		Character = character;
		if (PortraitImage != null)
		{
			PortraitImage.sprite = (character as Ally)?.Portrait;
			PortraitImage.enabled = PortraitImage.sprite != null;
		}

		if (character is Ally ally)
		{
			baseName = ally.CharacterName;
			NameText.text = GameplayText.Format(ally.CharacterName);
		}

		var game = FindObjectOfType<Game>();
		var levelSystem = game.LevelSystem;

		LevelDisplay.Setup("Lv",
			() => character.DisplayedVitals.Level.ToString(),
			() => { return levelSystem.GetPercentageToNextLevel(character.DisplayedVitals); });
		HpDisplay.Setup("HP",
			() => $"HP  {character.DisplayedVitals.HP}/{character.DisplayedStats.HPMax}",
			() => (float)character.DisplayedVitals.HP / character.DisplayedStats.HPMax);
		ManaDisplay.Setup("Mana",
			() => $"Mana  {character.DisplayedVitals.Mana}/{character.DisplayedStats.ManaMax}",
			() => (float)character.DisplayedVitals.Mana / character.DisplayedStats.ManaMax);
		HungerDisplay.Setup("Full",
			() => $"Food  {character.DisplayedVitals.Hunger}/{character.DisplayedStats.HungerMax}",
			() => (float)character.DisplayedVitals.Hunger / character.DisplayedStats.HungerMax);
	}

	internal void UpdateUI()
	{
		if (Character is Ally ally && baseName != null)
		{
			string label = ally.IsDowned ? $"{baseName} (Downed)" : $"{baseName} - Lv {ally.DisplayedVitals.Level}";
            if (NameText.text != label) NameText.text = GameplayText.Format(label);
		}

		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		ManaDisplay.UpdateUI();
		HungerDisplay.UpdateUI();
	}
}
