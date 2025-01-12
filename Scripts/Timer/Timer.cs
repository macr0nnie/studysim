using UnityEngine;
using UnityEngine.Events;
using System;

public class Timer : MonoBehaviour
{
    public float currentTime { get; private set; }
    public bool isRunning { get; private set; }
    public bool isPaused { get; private set; }

    public UnityEvent onTimerComplete;
    public UnityEvent<float> onTimeUpdated;

    private float targetTime;

    public void SetTimer(float minutes)
    {
        targetTime = minutes * 60;
        currentTime = targetTime;
        onTimeUpdated?.Invoke(currentTime);
    }

    public void StartTimer()
    {
        isRunning = true;
        isPaused = false;
    }

    public void PauseTimer()
    {
        isPaused = true;
        isRunning = false;
    }

    public void StopTimer()
    {
        isRunning = false;
        isPaused = false;
        currentTime = targetTime;
        onTimeUpdated?.Invoke(currentTime);
    }

    private void Update()
    {
        if (!isRunning || isPaused) return;

        currentTime -= Time.deltaTime;
        onTimeUpdated?.Invoke(currentTime);

        if (currentTime <= 0)
        {
            currentTime = 0;
            isRunning = false;
            onTimerComplete?.Invoke();
        }
    }

    public string GetFormattedTime()
    {
        TimeSpan time = TimeSpan.FromSeconds(currentTime);
        return string.Format("{0:D2}:{1:D2}", (int)time.TotalMinutes, time.Seconds);
    }
}