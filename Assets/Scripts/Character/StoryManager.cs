using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryChapter
{
    public string title;
    [Min(1)] public int unlockLevel;
    [TextArea(2, 5)] public string[] lines;
}

public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    [SerializeField] private StoryChapter[] chapters;

    private const string SaveKey = "SeenStoryLevels";
    private readonly HashSet<int> seenLevels = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadProgress();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Returns the chapter unlocked at this level, or null if already seen / none exists.
    public StoryChapter GetChapterForLevel(int level)
    {
        if (seenLevels.Contains(level)) return null;
        foreach (StoryChapter chapter in chapters)
            if (chapter.unlockLevel == level) return chapter;
        return null;
    }

    public void MarkChapterSeen(int level)
    {
        seenLevels.Add(level);
        SaveProgress();
    }

    private void LoadProgress()
    {
        seenLevels.Clear();
        string raw = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(raw)) return;
        foreach (string s in raw.Split(','))
            if (int.TryParse(s, out int lvl))
                seenLevels.Add(lvl);
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetString(SaveKey, string.Join(",", seenLevels));
        PlayerPrefs.Save();
    }
}
