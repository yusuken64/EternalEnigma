using System.Collections.Generic;
using UnityEngine;

public class LevelSystem : MonoBehaviour
{
	public List<LevelInfo> LevelData;
}

[System.Serializable]
public class LevelInfo
{
	public int Level;
	public int Experience;
	public int BaseAttack;
}