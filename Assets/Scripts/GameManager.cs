using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public event Action<GameState> OnGameStateChanged;
    private GameState currentGameState;
    
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
    //message to the other scripts that the game state has changed
    public void ChangeGameState(GameState newState)
    {
        if (currentGameState == newState) return;
        currentGameState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
    public int LoadSaveSlot()
    {
        return PlayerPrefs.GetInt("SaveSlot" + 0);
    }
}
public enum GameState
{
    Study,
    Building,
    Shopping,
    Paused
}

