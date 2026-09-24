using System;
using TMPro;
using UnityEngine;

public class CharacterStatsDisplay : MonoBehaviour
{
	public TextMeshProUGUI NameText;
	public StatsDisplay LevelDisplay;
	public StatsDisplay HpDisplay;
	public StatsDisplay SpDisplay;
	public StatsDisplay HungerDisplay;

	public Character Character;
	private string baseName;

	internal void Setup(Character character)
	{
		Character = character;

		if (character is Ally ally)
		{
			baseName = ally.CharacterName;
			NameText.text = ally.CharacterName;
		}

		var game = FindObjectOfType<Game>();
		var levelSystem = game.LevelSystem;

		LevelDisplay.Setup("Lv",
			() => character.DisplayedVitals.Level.ToString(),
			() => { return levelSystem.GetPercentageToNextLevel(character.DisplayedVitals); });
		HpDisplay.Setup("HP",
			() => $"{character.DisplayedVitals.HP}/{character.DisplayedStats.HPMax}",
			() => (float)character.DisplayedVitals.HP / character.DisplayedStats.HPMax);
		SpDisplay.Setup("SP",
			() => $"{character.DisplayedVitals.SP}/{character.DisplayedStats.SPMax}",
			() => (float)character.DisplayedVitals.SP / character.DisplayedStats.SPMax);
		HungerDisplay.Setup("Full",
			() => $"{character.DisplayedVitals.Hunger}/{character.DisplayedStats.HungerMax}",
			() => (float)character.DisplayedVitals.Hunger / character.DisplayedStats.HungerMax);
	}

	internal void UpdateUI()
	{
		if (Character is Ally ally && baseName != null)
		{
			string label = ally.IsDowned ? $"{baseName} (Downed)" : baseName;
			if (NameText.text != label) NameText.text = label;
		}

		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		SpDisplay.UpdateUI();
		HungerDisplay.UpdateUI();
	}
}
