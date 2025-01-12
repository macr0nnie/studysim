using UnityEngine;
using System;

/// <summary>
/// Core GameManager that handles game state and initialization
/// Setup: Attach to an empty GameObject named "GameManager" in your scene
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private UIManager uiManager;
    [SerializeField] private PomodoroManager pomodoroManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private AudioManager audioManager;
    
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

    public void ChangeGameState(GameState newState)
    {
        currentGameState = newState;
        OnGameStateChanged?.Invoke(newState);
    }
}

public enum GameState
{
    Study,
    Building,
    Shopping,
    Paused
}