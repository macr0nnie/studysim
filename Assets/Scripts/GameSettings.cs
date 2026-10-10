using UnityEngine;

// Player settings, stored in PlayerPrefs (not in save slots: they belong to the player, not the room).
public static class GameSettings
{
    public enum FocusMode { Pomodoro, DeepFocus, Sprint, Custom }

    // (study, break) minutes for each preset; Custom keeps whatever the sliders set.
    public static readonly (string name, string detail, float study, float rest)[] Modes =
    {
        ("Pomodoro", "25 min focus, 5 min break", 25, 5),
        ("Deep focus", "50 min focus, 10 min break", 50, 10),
        ("Sprint", "15 min focus, 3 min break", 15, 3),
        ("Custom", "Your own lengths", 0, 0),
    };

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat("MasterVolume", 1f);
        set { PlayerPrefs.SetFloat("MasterVolume", value); AudioListener.volume = value; }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat("MusicVolume", 1f);
        set
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            MusicPlayer music = Object.FindAnyObjectByType<MusicPlayer>();
            if (music != null) music.SetVolume(value);
        }
    }

    public static float EffectsVolume { get => PlayerPrefs.GetFloat("EffectsVolume", 1f); set => PlayerPrefs.SetFloat("EffectsVolume", value); }

    public static FocusMode Mode { get => (FocusMode)Mathf.Clamp(PlayerPrefs.GetInt("FocusMode", 0), 0, Modes.Length - 1); set => PlayerPrefs.SetInt("FocusMode", (int)value); }
    public static bool StrictFocus { get => Get("StrictFocus", false); set => Set("StrictFocus", value); }
    public static bool AutoStartBreak { get => Get("AutoStartBreak", true); set => Set("AutoStartBreak", value); }
    public static bool AutoStartStudy { get => Get("AutoStartStudy", false); set => Set("AutoStartStudy", value); }

    public static bool SessionToasts { get => Get("NotifySession", true); set => Set("NotifySession", value); }
    public static bool SessionChime { get => Get("NotifyChime", true); set => Set("NotifyChime", value); }
    public static bool LevelUpToasts { get => Get("NotifyLevelUp", true); set => Set("NotifyLevelUp", value); }
    public static bool AutosaveBadge { get => Get("NotifyAutosave", true); set => Set("NotifyAutosave", value); }

    public static RoomAmbience.Style Ambience { get => (RoomAmbience.Style)PlayerPrefs.GetInt("Ambience", 1); set => PlayerPrefs.SetInt("Ambience", (int)value); }
    public static bool UISounds { get => Get("UISounds", true); set => Set("UISounds", value); }
    public static bool DistractionPenalty { get => Get("DistractionPenalty", true); set => Set("DistractionPenalty", value); }
    public static int DistractionCoinsPerMinute { get => PlayerPrefs.GetInt("DistractionCoins", 6); set => PlayerPrefs.SetInt("DistractionCoins", value); }

    private static bool Get(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
    private static void Set(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAudio() => AudioListener.volume = MasterVolume;
}
