using System;
using UnityEngine;

public class Experience : MonoBehaviour
{
    public event Action<int, int> OnXPChanged; 
    public event Action<int> OnLevelUp;          

    private const int BaseXP = 100;
    public const int MaxLevel = 20;

    private int level;
    private int currentXP;

    public int Level => level;
    public int CurrentXP => currentXP;
    public int XPToNextLevel => XPRequiredForLevel(level);

    private void Awake()
    {
        Load();
    }

    public void GainExperience(int amount)
    {
        if (level >= MaxLevel) return;

        currentXP += amount;

        while (currentXP >= XPToNextLevel && level < MaxLevel)
        {
            currentXP -= XPToNextLevel;
            level++;
            Save();
            OnLevelUp?.Invoke(level);
        }

        Save();
        OnXPChanged?.Invoke(currentXP, XPToNextLevel);
    }

    public int XPRequiredForLevel(int lvl) =>
        Mathf.RoundToInt(BaseXP * Mathf.Pow(lvl, 1.5f));

    public int GetPlayerLevel() => level;

    private void Load()
    {
        level = PlayerPrefs.GetInt("PlayerLevel", 1);
        currentXP = PlayerPrefs.GetInt("PlayerExperience", 0);
    }

    public void Save()
    {
        PlayerPrefs.SetInt("PlayerLevel", level);
        PlayerPrefs.SetInt("PlayerExperience", currentXP);
        PlayerPrefs.Save();
    }
}
