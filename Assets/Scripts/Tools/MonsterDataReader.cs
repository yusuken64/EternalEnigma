#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

public class MonsterData
{
    public string ThisName { get; set; }
    public string Name { get; set; }
    public int HP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int EXP { get; set; }
    public string Drop { get; set; }
    public string Behavior { get; set; }
    public string SpecialAbilities { get; set; }
    public string AdditionalInfo { get; set; }
}

public class MonsterDataReader
{
    public List<MonsterData> ReadMonstersFromCSV(string filePath)
    {
        List<MonsterData> monsters = new List<MonsterData>();

        try
        {
            string[] lines = File.ReadAllLines(filePath);

            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                string[] fields = lines[i].Split(',');

                MonsterData monster = new MonsterData
                {
                    ThisName = fields[0],
                    Name = fields[1],
                    HP = int.Parse(fields[2]),
                    Attack = int.Parse(fields[3]),
                    Defense = int.Parse(fields[4]),
                    EXP = int.Parse(fields[5]),
                    Drop = fields[6],
                    //Behavior = fields[6],
                    //SpecialAbilities = fields[7],
                    //AdditionalInfo = fields[8]
                };

                monsters.Add(monster);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading CSV: {ex.Message}");
        }

        return monsters;
    }
}
#endif