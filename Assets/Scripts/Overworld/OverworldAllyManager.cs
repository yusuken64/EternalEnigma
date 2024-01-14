using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class OverworldAllyManager : MonoBehaviour
{
	public List<OverworldAlly> OverworldAllies;

	internal OverworldAlly GenerateRandomAlly()
	{
		var newAlly = Instantiate(OverworldAllies.Sample(), this.transform);
		return newAlly;
    }

    internal List<OverworldAlly> GenerateRandomAlly(int count)
    {
        var sample = OverworldAllies.Sample(count);
        return sample.Select(x => Instantiate(x, this.transform))
            .ToList();
    }

#if UNITY_EDITOR
    [ContextMenu("Assign Data")]
	public void AssignData()
	{
        AllyData[] allyDatas = new AllyData[]
        {
            new AllyData { Name = "Avery", Backstory = "Avery, a skilled mage, grew up in a secluded village known for its magical traditions." },
            new AllyData { Name = "Morgan", Backstory = "Morgan, a fearless warrior, hails from a distant land where martial arts are deeply revered." },
            new AllyData { Name = "Rowan", Backstory = "Rowan, a cunning rogue, spent their youth navigating the shadows of a bustling city." },
            new AllyData { Name = "Quinn", Backstory = "Quinn, a wise scholar, dedicated their life to uncovering ancient mysteries and lost knowledge." },
            new AllyData { Name = "Alex", Backstory = "Alex, a skilled archer, grew up in a nomadic tribe mastering the art of ranged combat." },
            new AllyData { Name = "Jordan", Backstory = "Jordan, a charismatic bard, traveled the lands collecting stories and spreading joy through music." },
            new AllyData { Name = "Riley", Backstory = "Riley, a mysterious druid, has a deep connection with nature and a penchant for shape-shifting." },
            new AllyData { Name = "Casey", Backstory = "Casey, a skilled engineer, spent years crafting intricate devices and constructs." },
            new AllyData { Name = "Taylor", Backstory = "Taylor, a diplomatic ambassador, honed their negotiation skills in the courts of a powerful kingdom." },
            new AllyData { Name = "Jamie", Backstory = "Jamie, a formidable tactician, rose through the military ranks with strategic brilliance." },
            new AllyData { Name = "Skylar", Backstory = "Skylar, a nomadic healer, wandered the lands offering aid to those in need with their medicinal expertise." },
            new AllyData { Name = "Dakota", Backstory = "Dakota, a skilled tracker, developed their senses in the wild, becoming an expert at hunting and survival." },
            new AllyData { Name = "Sage", Backstory = "Sage, an ancient mystic, possesses knowledge of forgotten spells and ancient rituals." },
            new AllyData { Name = "Cameron", Backstory = "Cameron, a master of illusions, honed their magical abilities in the arcane arts." },
            new AllyData { Name = "Finley", Backstory = "Finley, a quick-witted trickster, navigates the world with a mischievous sense of humor." },
            new AllyData { Name = "Harper", Backstory = "Harper, a skilled musician, uses the power of their melodies to weave magic and enchant audiences." },
            new AllyData { Name = "Reese", Backstory = "Reese, a seasoned blacksmith, forges powerful weapons and armor with unmatched craftsmanship." },
            new AllyData { Name = "Logan", Backstory = "Logan, a former gladiator, fought in the arenas and now seeks redemption through heroic deeds." },
            new AllyData { Name = "Kai", Backstory = "Kai, a stoic monk, follows the path of discipline and enlightenment, mastering both mind and body." },
            new AllyData { Name = "Sydney", Backstory = "Sydney, an agile acrobat, grew up in a traveling circus, mastering acrobatics and agility." },
            new AllyData { Name = "Blake", Backstory = "Blake, a skilled alchemist, experiments with potions and elixirs to unlock the secrets of transmutation." },
            new AllyData { Name = "Charlie", Backstory = "Charlie, a guardian of ancient relics, protects powerful artifacts from falling into the wrong hands." },
            new AllyData { Name = "Taylor", Backstory = "Taylor, a skilled archer, honed their marksmanship in the dense forests, becoming a master of ranged combat." },
            new AllyData { Name = "Alex", Backstory = "Alex, a charismatic diplomat, uses their silver tongue to navigate through political intrigue and diplomacy." },
            new AllyData { Name = "Casey", Backstory = "Casey, a skilled engineer, builds advanced machinery and gadgets with a keen intellect." },
            new AllyData { Name = "Morgan", Backstory = "Morgan, a fearless pirate, sailed the treacherous seas, seeking adventure and fortune." },
            new AllyData { Name = "Riley", Backstory = "Riley, a wise sage, has dedicated their life to the pursuit of knowledge, uncovering ancient secrets." },
            new AllyData { Name = "Quinn", Backstory = "Quinn, a master of disguise, infiltrated the ranks of powerful organizations to gather information." },
        };

		for (int i = 0; i < OverworldAllies.Count; i++)
		{
			OverworldAlly ally = OverworldAllies[i];
            var data = allyDatas[i];
            ally.Name = data.Name;
            ally.Description = data.Backstory;
        }

		OverworldAllies.ForEach(x =>
		{
			EditorUtility.SetDirty(x);
			AssetDatabase.SaveAssets();
		});
		AssetDatabase.Refresh();
	}
#endif
}

internal class AllyData
{
    public string Name;
    public string Backstory;
}