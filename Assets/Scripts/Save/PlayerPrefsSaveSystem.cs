using UnityEngine;
using System;

public class PlayerPrefsSaveSystem : ISaveSystem
{
    public void SaveData(string key, object data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    public T LoadData<T>(string key, T defaultValue)
    {
        if (!PlayerPrefs.HasKey(key))
            return defaultValue;

        string json = PlayerPrefs.GetString(key);
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }
}