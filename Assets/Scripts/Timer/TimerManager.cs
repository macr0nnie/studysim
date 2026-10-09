using UnityEngine;
using System;
using UnityEngine.Events;
using UnityEngine.UI;
/// <summary>
/// Manages the Pomodoro timer functionality and reward system
/// Setup: Attach to an empty GameObject in the scene
/// Dependencies: Requires UI elements for timer display and settings
/// </summary>
public class TimerManager : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float studyDuration = 1500f; // 25 minutes in seconds
    [SerializeField] private float breakDuration = 300f;  // 5 minutes in seconds
    
    [Header("Rewards")]
    [SerializeField] private int baseMoneyReward = 100;
    [SerializeField] private int baseExperienceReward = 50;

    public event Action<float> OnTimerTick;
    public event Action OnTimerComplete;
    public UnityEvent OnStudySessionComplete;
    
    private float currentTime;
    private bool isTimerRunning;
    private bool isStudySession = true;
    
    public float CurrentTime => currentTime;
    public bool IsTimerRunning => isTimerRunning;
    public bool IsStudySession => isStudySession;
    public PlayerCurrency playerCurrency;
    public Button plusButton;
    public Button minusButton;

    private void Start()
    {
        ResetTimer();
        if (plusButton) plusButton.onClick.AddListener(AddFiveMinutes);
        if (minusButton) minusButton.onClick.AddListener(RemoveFiveMinutes);
    }

    private void Update()
    {
        if (!isTimerRunning) return;
        
        if (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            OnTimerTick?.Invoke(currentTime);
        }
        else
        {
            CompleteTimer();
        }
    }
    public void StartTimer()
    {
        isTimerRunning = true;
    }
    public void PauseTimer()
    {
        isTimerRunning = false;
    }
    public void ResetTimer()
    {
        currentTime = isStudySession ? studyDuration : breakDuration;
        isTimerRunning = false;
        OnTimerTick?.Invoke(currentTime);
    }
    private void CompleteTimer()
    {
        isTimerRunning = false;
        if (isStudySession)
        {
            GrantRewards();
            OnStudySessionComplete?.Invoke();
        }
        isStudySession = !isStudySession;
        ResetTimer();
        OnTimerComplete?.Invoke();
    }
    const int StudySessionExperience = 50; // two sessions is about a level early on

    public void GrantRewards()
    {
        if (playerCurrency != null) playerCurrency.AddCoins(baseMoneyReward);
        Experience experience = FindFirstObjectByType<Experience>();
        if (experience != null) experience.GainExperience(StudySessionExperience);
    }
    public void SetCustomDuration(float minutes)
    {
        if (!isTimerRunning)
        {
            studyDuration = minutes * 60f;
            ResetTimer();
        }
    }
    public void AddFiveMinutes()
    {
        if (!isTimerRunning && studyDuration < 7200f) // 2 hours in seconds
        {
            studyDuration += 300f; // 5 minutes in seconds
            if (studyDuration > 7200f) studyDuration = 7200f; // Ensure it does not exceed 2 hours
            ResetTimer();
        }
    }
    public void RemoveFiveMinutes()
    {
        if (!isTimerRunning && studyDuration > 300f)
        {
            studyDuration -= 300f; // 5 minutes in seconds
            ResetTimer();
        }
    }
}