using UnityEngine;
using UnityEngine.Events;

public class Experience : MonoBehaviour
{
    int current_player_level = 1;
    int experiencePoints = 0;
    
    public UnityEvent<int> PlayerLevelUp;

    void Awake()
    {
        //load current player level and experience points from player prefs
        LoadPlayerLevel();
    }
    public void LevelUP(){
        current_player_level++;
        experiencePoints = 0;
    }
    public void GainExperience(int new_experience){
        experiencePoints += new_experience;
        if(experiencePoints >= 100){
            LevelUP();
        }
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
    //debugging method to manually set the player experience level.
    public void SetPlayerExperiencel(int new_experience){
        experiencePoints = new_experience;
        SavePlayerLevel();
    }

}




