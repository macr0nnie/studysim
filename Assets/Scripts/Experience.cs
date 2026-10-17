using System;
using UnityEngine;
using UnityEngine.Events;

public class Experience : MonoBehaviour
{
    int current_player_level = 1;
    int experiencePoints = 0;

    public UnityEvent<int> PlayerLevelUp;
    public event Action OnExperienceChanged; // xp or level changed; the HUD redraws its bar

    void Awake()
    {
        //load current player level and experience points from player prefs
        LoadPlayerLevel();
    }

    // Each level needs a little more than the last: 100, 125, 150...
    public int ExperienceToNextLevel => 100 + 25 * (current_player_level - 1);
    public int GetExperience() => experiencePoints;
    // Coins paid on reaching a level, so levelling up buys something for the room.
    public static int LevelBonus(int level) => 25 * level;

    public void GainExperience(int new_experience){
        if (new_experience <= 0) return;
        experiencePoints += new_experience;
        // carry the overflow so a big reward can level up more than once
        while (experiencePoints >= ExperienceToNextLevel)
        {
            experiencePoints -= ExperienceToNextLevel;
            current_player_level++;
            PayLevelBonus();
            PlayerLevelUp?.Invoke(current_player_level);
        }
        SavePlayerLevel();
        OnExperienceChanged?.Invoke();
    }
    private void PayLevelBonus()
    {
        PlayerCurrency currency = FindAnyObjectByType<PlayerCurrency>();
        if (currency != null) currency.AddCoins(LevelBonus(current_player_level));
    }
    //get the current player level
    public int GetPlayerLevel(){
        return current_player_level;
    }
    //load the player level and experience points from player prefs
    public void LoadPlayerLevel(){
        current_player_level = PlayerPrefs.GetInt("PlayerLevel", 1);
        experiencePoints = PlayerPrefs.GetInt("PlayerExperience", 0);
    }
    public void SavePlayerLevel(){
        PlayerPrefs.SetInt("PlayerLevel", current_player_level);
        PlayerPrefs.SetInt("PlayerExperience", experiencePoints);
        PlayerPrefs.Save();
    }

}
