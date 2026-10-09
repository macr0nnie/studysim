using UnityEngine;

public enum Mood { Content, Happy, Sad, Stressed }

// Story hooks. Everything the character says comes from Line(); the story later swaps lines per Stage
// (0 = ordinary study buddy ... higher = more aware that it is in a game) without touching the behaviour code.
public static class CharacterDialogue
{
    private const string StageKey = "story.stage";

    // How aware the character is. Raise it from story code; saved with PlayerPrefs.
    public static int Stage
    {
        get => PlayerPrefs.GetInt(StageKey, 0);
        set => PlayerPrefs.SetInt(StageKey, value);
    }

    private static readonly string[][] Stage0 =
    {
        /* Content  */ new[] { "Keep going, you've got this.", "Focus mode: on.", "Another page down.", "Hmm, what was that formula again?" },
        /* Happy    */ new[] { "This room feels great!", "I love what you've done with the place.", "Best study session ever." },
        /* Sad      */ new[] { "It's a bit empty in here...", "Could we get something nice for the room?", "I'm tired." },
        /* Stressed */ new[] { "Is the room shaking?!", "Please close that tab...", "I can't concentrate!" },
    };

    // shortcut: stage 0 only; later stages add rows here, and the username-reading line goes in a stage above 0 (Environment.UserName)
    public static string Line(Mood mood, int stage)
    {
        string[] lines = Stage0[(int)mood];
        return lines[Random.Range(0, lines.Length)];
    }
}
