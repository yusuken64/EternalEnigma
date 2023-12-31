using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class MonsterSpawnRateReader
{
    public List<MonsterSpawnData> ParseMonsterData(string filePath)
    {
        List<MonsterSpawnData> monsterList = new List<MonsterSpawnData>();

        string data = File.ReadAllText(filePath);

        // Split the input data by lines
        string[] lines = data.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines.Skip(1))
        {
            // Split each line by tabs to get floor and monster data
            string[] parts = line.Split('\t');

            // Extract floor number using regex
            int floor = int.Parse(Regex.Match(parts[0], @"\d+").Value);

            // Extract monster and appearance rates
            string[] monsterAppearances = parts[1].Split(',');
            List<string> monsters = new List<string>();
            List<int> appearances = new List<int>();

            foreach (var entry in monsterAppearances)
            {
                // Extract monster name and appearance rate using regex
                string monster = Regex.Match(entry, @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]+(?=\()").Value;

                int rate = int.Parse(Regex.Match(entry, @"\d+").Value);

                monsters.Add(monster);
                appearances.Add(rate);
            }

            // Create MonsterData object and add to the list
            MonsterSpawnData monsterData = new MonsterSpawnData
            {
                Floor = floor,
                Monsters = monsters.ToArray(),
                Appearances = appearances.ToArray()
            };

            monsterList.Add(monsterData);
        }

        return monsterList;
    }
}
public class MonsterSpawnData
{
    public int Floor { get; set; }
    public string[] Monsters { get; set; }
    public int[] Appearances { get; set; }
}