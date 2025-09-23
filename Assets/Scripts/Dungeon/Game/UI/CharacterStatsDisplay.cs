using System;
using UnityEngine;

public class CharacterStatsDisplay : MonoBehaviour
{
	public StatsDisplay LevelDisplay;
	public StatsDisplay HpDisplay;
	public StatsDisplay SpDisplay;
	public StatsDisplay HungerDisplay;

	public Character Character;

	internal void Setup(Character character)
	{
		Character = character;

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
		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		SpDisplay.UpdateUI();
		HungerDisplay.UpdateUI();
	}
}
