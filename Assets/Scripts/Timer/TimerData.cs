using System;

/// <summary>
/// Data structure for saving/loading timer state
/// </summary>
[Serializable]
public class TimerData
{
    public float setTime;
    public float currentTime;
    public bool isRunning;
    public bool isPaused;

    public TimerData(float setTime, float currentTime, bool isRunning, bool isPaused)
    {
        this.setTime = setTime;
        this.currentTime = currentTime;
        this.isRunning = isRunning;
        this.isPaused = isPaused;
    }
}