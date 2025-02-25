using UnityEngine;
using UnityEngine.UI;


public class TaskList : MonoBehaviour
{
    public Task[] tasks;
    public Text taskName;
    public Text taskID;
    public Text isComplete;

    //edit task
    //add task
    //delete task
    //display tasks
        
}

public class Task {
    public string taskName;
    public int taskID;
    public bool isComplete;
  
}
