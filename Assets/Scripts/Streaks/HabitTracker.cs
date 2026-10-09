using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

[Serializable]
public class StudyHabit
{
    public string habitName;
    public string description;
    public int targetMinutes; // Target study time in minutes
    // Stored as "yyyy-MM-dd" text: JsonUtility can't save DateTime, so streaks used to reset every launch.
    public const string Day = "yyyy-MM-dd";
    public static string Today => DateTime.Now.ToString(Day, CultureInfo.InvariantCulture);
    public List<string> completionDays = new List<string>();
    public List<DateTime> completionDates => completionDays.ConvertAll(d => DateTime.ParseExact(d, Day, CultureInfo.InvariantCulture));
    public int currentStreak; // unbroken run ending today, or yesterday while today isn't ticked yet
    public int bestStreak;
    public bool isCompletedToday;
    public string lastRewardDay = ""; // XP once a day, even if today is unticked and ticked again

    public string color = ""; // hex RGB; empty in older saves, filled in on load

    // Pleasant on white and on the dark theme; each habit takes the next one nobody uses.
    public static readonly string[] Palette = { "E86A6A", "F2A33A", "D9B52B", "5BBF7A", "39B5A8", "4A90D9", "8E7CD6", "D870B0", "8A9A5B", "C98A5E" };
    public Color Tint => ColorUtility.TryParseHtmlString("#" + color, out Color c) ? c : Color.gray;

    public bool DoneOn(DateTime day) => completionDays.Contains(day.ToString(Day, CultureInfo.InvariantCulture));
}

public class HabitTracker : MonoBehaviour
{
    [Header("Habits")]
    public List<StudyHabit> habits = new List<StudyHabit>();

    private const string SaveKey = "StudyHabits";

    private void Start()
    {
        LoadHabits();
        if (habits.FindAll(x => string.IsNullOrEmpty(x.color)).Count > 0) { foreach (var x in habits) if (string.IsNullOrEmpty(x.color)) x.color = FreeColor(); SaveHabits(); }
        ResetDailyCompletion();
        UpdateAllStreaks();
    }

    public void AddHabit(string name, string description, int targetMinutes)
    {
        var habit = new StudyHabit
        {
            habitName = name,
            description = description,
            targetMinutes = targetMinutes,
            color = FreeColor(),
            isCompletedToday = false
        };
        habits.Add(habit);
        SaveHabits();
    }

    // First palette colour no habit has yet, or the next in turn once they are all taken.
    private string FreeColor()
    {
        foreach (string hex in StudyHabit.Palette)
            if (!habits.Exists(x => x.color == hex)) return hex;
        return StudyHabit.Palette[habits.Count % StudyHabit.Palette.Length];
    }

    // The colour dot on a habit card: next colour in the palette.
    public void CycleColor(StudyHabit habit)
    {
        habit.color = StudyHabit.Palette[(Array.IndexOf(StudyHabit.Palette, habit.color) + 1) % StudyHabit.Palette.Length];
        SaveHabits();
    }

    // Ticks or unticks today. True the first time today it's ticked, so XP is given once a day.
    public bool ToggleToday(StudyHabit habit)
    {
        string today = StudyHabit.Today;
        bool reward = false;
        if (habit.completionDays.Remove(today)) habit.isCompletedToday = false;
        else
        {
            habit.completionDays.Add(today);
            habit.isCompletedToday = true;
            reward = habit.lastRewardDay != today;
            habit.lastRewardDay = today;
        }
        UpdateStreak(habit);
        SaveHabits();
        return reward;
    }

    public void RemoveHabit(string name)
    {
        habits.RemoveAll(h => h.habitName == name);
        SaveHabits();
    }

    private void UpdateAllStreaks()
    {
        foreach (var habit in habits)
        {
            UpdateStreak(habit);
        }
    }

    // The old version counted back from the last tick, so a streak never broke however many days were missed.
    private static void UpdateStreak(StudyHabit habit)
    {
        habit.completionDays.Sort(); // ISO dates sort by text
        DateTime day = DateTime.Now.Date;
        if (!habit.DoneOn(day)) day = day.AddDays(-1); // not ticked yet today: still alive from yesterday
        int streak = 0;
        while (habit.DoneOn(day)) { streak++; day = day.AddDays(-1); }
        habit.currentStreak = streak;

        int run = 0, best = 0;
        List<DateTime> dates = habit.completionDates;
        for (int i = 0; i < dates.Count; i++)
        {
            run = i > 0 && (dates[i] - dates[i - 1]).Days == 1 ? run + 1 : i > 0 && dates[i] == dates[i - 1] ? run : 1;
            best = Math.Max(best, run);
        }
        habit.bestStreak = Math.Max(habit.bestStreak, best);
    }

    private void ResetDailyCompletion()
    {
        foreach (var habit in habits)
        {
            if (habit.completionDays.Count == 0 || habit.completionDays[habit.completionDays.Count - 1] != StudyHabit.Today)
            {
                habit.isCompletedToday = false;
            }
        }
    }

    private void SaveHabits()
    {
        string json = JsonUtility.ToJson(new HabitListWrapper { habits = habits });
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void LoadHabits()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            string json = PlayerPrefs.GetString(SaveKey);
            var wrapper = JsonUtility.FromJson<HabitListWrapper>(json);
            if (wrapper != null && wrapper.habits != null)
                habits = wrapper.habits;
        }
    }

    [Serializable]
    private class HabitListWrapper
    {
        public List<StudyHabit> habits = new List<StudyHabit>();
    }
}
