using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public event Action<GameState> OnGameStateChanged;
    
    private GameState currentGameState;

    //
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeGameState(GameState newState)
    {
        currentGameState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
    public void LoadSave(int saveSlot)
    {
       //PlayerPrefs.SetInt("SaveSlot", saveSlot);
       //PlayerPrefs.GetString("SaveSlot", "0");
    }


    
}

public enum GameState
{
    Study,
    Building,
    Shopping,
    Paused
}