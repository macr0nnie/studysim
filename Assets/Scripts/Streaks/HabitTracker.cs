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
    public int currentStreak;
    public int bestStreak;
    public bool isCompletedToday;
}

public class HabitTracker : MonoBehaviour
{
    [Header("Habits")]
    public List<StudyHabit> habits = new List<StudyHabit>();

    private const string SaveKey = "StudyHabits";

    private void Start()
    {
        LoadHabits();
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
            isCompletedToday = false
        };
        habits.Add(habit);
        SaveHabits();
    }

    public void CompleteHabit(string name)
    {
        var habit = habits.Find(h => h.habitName == name);
        if (habit != null && !habit.isCompletedToday)
        {
            habit.isCompletedToday = true;
            habit.completionDays.Add(StudyHabit.Today);
            UpdateStreak(habit);
            SaveHabits();
        }
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

    private void UpdateStreak(StudyHabit habit)
    {
        if (habit.completionDays.Count == 0)
        {
            habit.currentStreak = 0;
            return;
        }
        habit.completionDays.Sort(); // ISO dates sort by text
        List<DateTime> dates = habit.completionDates;
        int streak = 1;
        for (int i = dates.Count - 1; i > 0; i--)
        {
            var today = dates[i].Date;
            var prev = dates[i - 1].Date;
            if ((today - prev).Days == 1)
                streak++;
            else if ((today - prev).Days > 1)
                break;
        }
        habit.currentStreak = streak;
        if (habit.currentStreak > habit.bestStreak)
            habit.bestStreak = habit.currentStreak;
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
