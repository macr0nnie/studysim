using System;
using System.Collections.Generic;
using UnityEngine;

public class TaskList : MonoBehaviour
{
    public event Action OnTasksChanged;

    public IReadOnlyList<StudyTask> Tasks => tasks;

    private const string SaveKey = "StudyTaskList";
    private List<StudyTask> tasks = new();

    private void Start()
    {
        Load();
    }

    public void AddTask(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;
        tasks.Add(new StudyTask { taskName = taskName.Trim() });
        Save();
        OnTasksChanged?.Invoke();
    }

    public void SetComplete(int index, bool complete)
    {
        if (index < 0 || index >= tasks.Count) return;
        tasks[index].isComplete = complete;
        Save();
        OnTasksChanged?.Invoke();
    }

    public void RemoveTask(int index)
    {
        if (index < 0 || index >= tasks.Count) return;
        tasks.RemoveAt(index);
        Save();
        OnTasksChanged?.Invoke();
    }

    public void ClearCompleted()
    {
        tasks.RemoveAll(t => t.isComplete);
        Save();
        OnTasksChanged?.Invoke();
    }

    private void Save()
    {
        string json = JsonUtility.ToJson(new TaskListWrapper { tasks = tasks });
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return;
        string json = PlayerPrefs.GetString(SaveKey);
        var wrapper = JsonUtility.FromJson<TaskListWrapper>(json);
        if (wrapper?.tasks != null)
            tasks = wrapper.tasks;
        OnTasksChanged?.Invoke();
    }

    [Serializable]
    private class TaskListWrapper
    {
        public List<StudyTask> tasks = new();
    }
}

[Serializable]
public class StudyTask
{
    public string taskName;
    public bool isComplete;
}
