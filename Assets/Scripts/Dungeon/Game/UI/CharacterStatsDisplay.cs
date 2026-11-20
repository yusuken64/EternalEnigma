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
	public TownAlly TownActor { get; private set; }
	private string baseName;

    private void Awake()
    {
        if (NameText == null) return;
        float maximum = NameText.enableAutoSizing ? NameText.fontSizeMax : NameText.fontSize;
        NameText.enableAutoSizing = true;
        NameText.fontSizeMin = Mathf.Min(18, maximum);
        NameText.fontSizeMax = maximum;
        NameText.textWrappingMode = TextWrappingModes.Normal;
    }

	internal void Setup(Character character)
	{
		TownActor = null;
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

	internal void UpdateTravelUI(TownAlly hero)
	{
		TownActor=hero;
		var stats=TownUtilityService.StatsFor(hero);
		int hp=RecoveryMath.SavedVital(hero.Hp,stats.HPMax);
		int mana=RecoveryMath.SavedVital(hero.Mana,stats.ManaMax);
		int food=hero.HasHunger?hero.Hunger:stats.HungerMax;
		NameText.text=GameplayText.Format($"{hero.Name} - Lv {hero.Level}"+(hp<=0?" (Downed)":""));
		PortraitImage.sprite=hero.Portrait;PortraitImage.enabled=hero.Portrait!=null;
		SetTravelBar(HpDisplay,"HP",hp,stats.HPMax);
		SetTravelBar(ManaDisplay,"Mana",mana,stats.ManaMax);
		SetTravelBar(HungerDisplay,"Food",food,stats.HungerMax);
		LevelDisplay.StatValueSlider.value=LevelSystem.Progress(hero.Level,hero.Experience);
	}

	private static void SetTravelBar(StatsDisplay display,string label,int value,int maximum)
	{
		display.ValueText.text=GameplayText.Format($"{label}  {value}/{maximum}");
		display.StatValueSlider.value=maximum>0?(float)value/maximum:0;
	}

	internal void UpdateUI()
	{
		if (Character is Ally ally && baseName != null)
		{
			string label = $"{baseName} - Lv {ally.DisplayedVitals.Level}" + (ally.IsDowned ? " (Downed)" : "");
            if (NameText.text != label) NameText.text = GameplayText.Format(label);
		}

		LevelDisplay.UpdateUI();
		HpDisplay.UpdateUI();
		ManaDisplay.UpdateUI();
		HungerDisplay.UpdateUI();
	}
}
