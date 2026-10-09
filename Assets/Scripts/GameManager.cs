using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public event Action<GameState> OnGameStateChanged;
    private GameState currentGameState;
    
    // A cozy room doesn't need hundreds of fps: cap it, and idle further while the game sits behind the study browser.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CapFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        Application.focusChanged += focused => Application.targetFrameRate = focused ? 60 : 15;
    }

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
}
public enum GameState
{
    Study,
    Building,
    Shopping,
    Paused
}

