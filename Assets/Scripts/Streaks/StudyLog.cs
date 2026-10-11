using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Every finished study session, summed per day and subject, for the planner's Stats page, the session card
// and the character's lines. Kept in PlayerPrefs like the other game keys, so save slots carry it (SaveSystem).
// Read fresh each time instead of cached: opening another save slot swaps the keys underneath.
public static class StudyLog
{
    private const string Key = "StudyLog", SubjectKey = "StudySubject";
    public const string NoSubject = "General";

    [Serializable]
    public class Entry
    {
        public string day, subject;
        public int minutes, sessions;
    }

    [Serializable]
    private class Data { public List<Entry> entries = new List<Entry>(); }

    private static Data Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        Data data = null;
        try { if (json.Length > 0) data = JsonUtility.FromJson<Data>(json); }
        catch (ArgumentException) { } // a corrupted log starts over rather than breaking the session reward
        return data ?? new Data();
    }

    public static List<Entry> Entries => Load().entries;

    // What the next sessions count towards; set on the planner's Stats page.
    public static string Subject
    {
        get => PlayerPrefs.GetString(SubjectKey, NoSubject);
        set
        {
            string s = string.IsNullOrWhiteSpace(value) ? NoSubject : value.Trim();
            PlayerPrefs.SetString(SubjectKey, s.Length > 24 ? s.Substring(0, 24) : s);
            PlayerPrefs.Save();
        }
    }

    public static void Record(int minutes)
    {
        if (minutes <= 0) return;
        Data data = Load();
        string today = StudyHabit.Today, subject = Subject;
        Entry entry = data.entries.Find(e => e.day == today && e.subject == subject);
        if (entry == null) data.entries.Add(entry = new Entry { day = today, subject = subject });
        entry.minutes += minutes;
        entry.sessions++;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static int MinutesOn(DateTime day)
    {
        string key = day.ToString(StudyHabit.Day, CultureInfo.InvariantCulture);
        int total = 0;
        foreach (Entry e in Entries) if (e.day == key) total += e.minutes;
        return total;
    }

    // Today and the six days before it.
    public static int MinutesThisWeek()
    {
        int total = 0;
        for (int i = 0; i < 7; i++) total += MinutesOn(DateTime.Now.Date.AddDays(-i));
        return total;
    }

    public static int TotalMinutes()
    {
        int total = 0;
        foreach (Entry e in Entries) total += e.minutes;
        return total;
    }

    // Minutes per subject, most studied first.
    public static List<KeyValuePair<string, int>> BySubject()
    {
        var totals = new Dictionary<string, int>();
        foreach (Entry e in Entries) totals[e.subject] = (totals.TryGetValue(e.subject, out int m) ? m : 0) + e.minutes;
        var list = new List<KeyValuePair<string, int>>(totals);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    // Days in a row with any study, forgiving one missed day (same rule as habits).
    public static int DayStreak()
    {
        var days = new HashSet<string>();
        foreach (Entry e in Entries) days.Add(e.day);
        return StudyHabit.Streak(d => days.Contains(d.ToString(StudyHabit.Day, CultureInfo.InvariantCulture)), DateTime.Now.Date);
    }

    public static string Format(int minutes) => minutes >= 60 ? $"{minutes / 60}h {minutes % 60:00}m" : $"{minutes}m";
}
