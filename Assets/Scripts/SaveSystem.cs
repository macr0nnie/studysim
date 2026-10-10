using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

// Save slots. Every system already keeps its live state in PlayerPrefs, so a slot is a snapshot of those
// keys, written back when a slot is opened from the save-select screen (the only place slots are picked).
// Slot 0 is the Editor's slot (the room scene played directly) and is listed as "Autosave".
public static class SaveSystem
{
    public const int SlotCount = 4; // slot 0 + 3 menu slots
    public const string GameScene = "Protoype_2", MenuScene = "MainMenu", SelectScene = "SaveSelect";
    private static readonly string[] StringKeys = { "RoomLayout", "RoomPaint", "MenuColor", "TodoTasks", "StudyHabits", "iPodPlayerData" };
    private static readonly string[] IntKeys = { "PlayerLevel", "PlayerExperience", "PlayerCoins", "EmptyRoom" };
    private static readonly string[] FloatKeys = { "StudySessionSeconds", "BreakSessionSeconds" };

    [Serializable]
    public class Snapshot
    {
        public string savedAt;
        public int level, coins;
        public List<string> keys = new List<string>(), values = new List<string>();
        public DateTime SavedAt => DateTime.TryParse(savedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime t) ? t : DateTime.MinValue; // a hand-edited or old slot must not break the menu
    }

    public static event Action<int> Saved;

    private static string SlotKey(int slot) => "SaveGame" + slot;

    // The slot picked on the save-select screen; the game autosaves into it. Kept outside the game keys so a
    // fresh game doesn't clear it. 0 when the room scene is played directly (in the Editor).
    public static int CurrentSlot
    {
        get => PlayerPrefs.GetInt("CurrentSlot", 0);
        private set => PlayerPrefs.SetInt("CurrentSlot", value);
    }

    public static void Save(int slot)
    {
        var snapshot = new Snapshot
        {
            savedAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            level = PlayerPrefs.GetInt("PlayerLevel", 1),
            coins = PlayerPrefs.GetInt("PlayerCoins", 0),
        };
        void Add(string key, string value) { snapshot.keys.Add(key); snapshot.values.Add(value); }
        foreach (string key in StringKeys) if (PlayerPrefs.HasKey(key)) Add(key, PlayerPrefs.GetString(key));
        foreach (string key in IntKeys) if (PlayerPrefs.HasKey(key)) Add(key, PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture));
        foreach (string key in FloatKeys) if (PlayerPrefs.HasKey(key)) Add(key, PlayerPrefs.GetFloat(key).ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(SlotKey(slot), JsonUtility.ToJson(snapshot));
        PlayerPrefs.Save();
        Saved?.Invoke(slot);
    }

    public static Snapshot Peek(int slot)
    {
        string json = PlayerPrefs.GetString(SlotKey(slot), "");
        if (json.Length == 0) return null;
        try { return JsonUtility.FromJson<Snapshot>(json); }
        catch (ArgumentException) { return null; } // corrupted slot reads as empty rather than breaking the menu
    }

    public static void Delete(int slot)
    {
        PlayerPrefs.DeleteKey(SlotKey(slot));
        PlayerPrefs.Save();
    }

    // From the save-select screen: open a slot in the game scene. A fresh (or empty) slot is cleared and
    // written straight away so it shows up as a save.
    public static void Play(int slot, bool fresh)
    {
        if (fresh || !Apply(slot))
        {
            StartFresh();
            Save(slot);
        }
        CurrentSlot = slot;
        UIKit.LoadSavedTheme();
        SceneManager.LoadScene(GameScene);
    }

    // Writes a slot's snapshot back into the live keys. False when the slot is empty.
    private static bool Apply(int slot)
    {
        Snapshot snapshot = Peek(slot);
        if (snapshot == null) return false;
        ClearGame();
        for (int i = 0; i < snapshot.keys.Count && i < snapshot.values.Count; i++)
        {
            string key = snapshot.keys[i], value = snapshot.values[i];
            if (Array.IndexOf(IntKeys, key) >= 0 && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) PlayerPrefs.SetInt(key, n);
            else if (Array.IndexOf(FloatKeys, key) >= 0 && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) PlayerPrefs.SetFloat(key, f);
            else if (Array.IndexOf(StringKeys, key) >= 0) PlayerPrefs.SetString(key, value);
        }
        PlayerPrefs.Save();
        return true;
    }

    // A new game starts in an empty bedroom to decorate. The flag makes the room scene clear the designed
    // furniture once; saves made before this have a RoomLayout (or no flag) and keep their room.
    private static void StartFresh()
    {
        ClearGame();
        PlayerPrefs.SetInt("EmptyRoom", 1);
    }

    private static void ClearGame()
    {
        foreach (string[] keys in new[] { StringKeys, IntKeys, FloatKeys })
            foreach (string key in keys) PlayerPrefs.DeleteKey(key);
    }
}
