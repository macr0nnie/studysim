using System;
using System.Globalization;
using UnityEngine;
using Random = UnityEngine.Random;

public enum Mood { Content, Happy, Sad, Stressed }

// Story hooks. Everything the character says comes from Line(); the story later swaps lines per Stage
// (0 = ordinary study buddy ... higher = more aware that it is in a game) without touching the behaviour code.
public static class CharacterDialogue
{
    private const string StageKey = "story.stage", NameKey = "PlayerName", LastSeenKey = "LastSeenDay";

    // Set in Settings > Focus. Empty until the player gives one.
    public static string PlayerName
    {
        get => PlayerPrefs.GetString(NameKey, "");
        set { PlayerPrefs.SetString(NameKey, (value ?? "").Trim()); PlayerPrefs.Save(); }
    }

    private static string Comma => PlayerName.Length > 0 ? ", " + PlayerName : "";

    // The first thing said when the room opens: depends on the time of day and how long you were away.
    public static string Greeting()
    {
        string last = PlayerPrefs.GetString(LastSeenKey, "");
        PlayerPrefs.SetString(LastSeenKey, StudyHabit.Today);
        int away = DateTime.TryParseExact(last, StudyHabit.Day, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime seen)
            ? (DateTime.Now.Date - seen).Days : -1;
        int hour = DateTime.Now.Hour;
        if (away < 0) return $"Hi{Comma}! Welcome to our room.";
        if (away >= 3) return $"You're back{Comma}! I missed you. Let's ease in with a short session.";
        if (hour < 5) return $"Still up{Comma}? Let's keep it short. Sleep helps you remember.";
        if (hour < 12) return $"Good morning{Comma}!";
        if (hour < 18) return $"Good afternoon{Comma}! Ready to focus?";
        return $"Evening{Comma}. One more session?";
    }

    public static string SessionDone(int minutes, string subject)
    {
        string what = subject == StudyLog.NoSubject ? "studying" : subject;
        string[] lines =
        {
            $"{minutes} minutes of {what}! Nice work{Comma}.",
            $"Done! Stretch with me, then grab some water.",
            $"That's {StudyLog.Format(StudyLog.MinutesOn(DateTime.Now.Date))} today. I'm proud of you{Comma}.",
        };
        return lines[Random.Range(0, lines.Length)];
    }

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
        // Now and then something about you instead of the room.
        if (mood != Mood.Stressed && Random.value < 0.25f)
        {
            if (DateTime.Now.Hour < 5) return $"It's really late{Comma}. Bed soon?";
            if (StudyLog.Subject != StudyLog.NoSubject) return $"How's the {StudyLog.Subject} going?";
            if (PlayerName.Length > 0) return $"You're doing great, {PlayerName}.";
        }
        string[] lines = Stage0[(int)mood];
        return lines[Random.Range(0, lines.Length)];
    }
}
