using System;
using System.Collections.Generic;
using UnityEngine;

// The player's to-do list, saved between sessions. The planner panel shows and edits it.
public class TaskList : MonoBehaviour
{
    public List<Task> tasks = new List<Task>();

    private const string SaveKey = "TodoTasks";

    private void Awake()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            var saved = JsonUtility.FromJson<TaskListWrapper>(PlayerPrefs.GetString(SaveKey));
            if (saved != null && saved.tasks != null) tasks = saved.tasks;
        }
    }

    public void AddTask(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        tasks.Add(new Task { taskName = name.Trim(), taskID = tasks.Count == 0 ? 1 : tasks[tasks.Count - 1].taskID + 1 });
        Save();
    }

    // Returns true the first time a task is ticked off, so it only rewards once.
    public bool ToggleTask(Task task)
    {
        task.isComplete = !task.isComplete;
        bool firstCompletion = task.isComplete && !task.rewarded;
        if (firstCompletion) task.rewarded = true;
        Save();
        return firstCompletion;
    }

    public void RemoveTask(Task task)
    {
        tasks.Remove(task);
        Save();
    }

    private void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new TaskListWrapper { tasks = tasks }));
        PlayerPrefs.Save();
    }

    [Serializable]
    private class TaskListWrapper
    {
        public List<Task> tasks = new List<Task>();
    }
}

[Serializable]
public class Task {
    public string taskName;
    public int taskID;
    public bool isComplete;
    public bool rewarded;
}
