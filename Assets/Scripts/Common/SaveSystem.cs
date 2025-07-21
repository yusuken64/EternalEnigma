using UnityEngine;

public static class SaveSystem
{
    private const string SaveKey = "SaveData";

    public static GameSaveData LoadData()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            Debug.Log("No save data found. Creating new save.");
            return null;
        }

        string json = PlayerPrefs.GetString(SaveKey);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
        return data;
    }

    public static void SaveData(GameSaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    public static void ClearData()
    {
        PlayerPrefs.DeleteKey(SaveKey);
    }
}
