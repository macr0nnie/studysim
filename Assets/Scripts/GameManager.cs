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
        ApplyGraphics();
        Application.focusChanged += focused => { if (!MiniMode.Active) Application.targetFrameRate = focused ? ActiveFps : 15; };
#if UNITY_EDITOR
        int editorLevel = QualitySettings.GetQualityLevel();
        Application.quitting += () => QualitySettings.SetQualityLevel(editorLevel, true); // don't leave the Editor on Low
#endif
    }

    private static int ActiveFps => GameSettings.LowGraphics ? 30 : 60;

    // Low graphics switches to the "Low" quality level (no ambient occlusion, one cheap shadow map, 80% render scale) and 30 fps.
    public static void ApplyGraphics()
    {
        int level = Array.IndexOf(QualitySettings.names, GameSettings.LowGraphics ? "Low" : "PC");
        if (level >= 0 && level != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(level, true);
        if (!MiniMode.Active) Application.targetFrameRate = Application.isFocused ? ActiveFps : 15;
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

