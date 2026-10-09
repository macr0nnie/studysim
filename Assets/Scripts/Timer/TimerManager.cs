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

    private const string LengthKey = "StudySessionSeconds";

    private void Start()
    {
        // the player's chosen session length survives restarts
        studyDuration = PlayerPrefs.GetFloat(LengthKey, studyDuration);
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
        // A finished study session rolls straight into its break; after the break, wait for the player.
        if (!isStudySession) StartTimer();
        OnTimerComplete?.Invoke();
    }

    public int MoneyReward => baseMoneyReward;
    public int ExperienceReward => baseExperienceReward;
    public float StudyMinutes => studyDuration / 60f;

    private void SaveLength()
    {
        PlayerPrefs.SetFloat(LengthKey, studyDuration);
        PlayerPrefs.Save();
    }

    public void GrantRewards()
    {
        if (playerCurrency != null) playerCurrency.AddCoins(baseMoneyReward);
        Experience experience = FindFirstObjectByType<Experience>();
        if (experience != null) experience.GainExperience(baseExperienceReward);
    }
    public void SetCustomDuration(float minutes)
    {
        if (!isTimerRunning)
        {
            studyDuration = Mathf.Clamp(minutes, 5f, 120f) * 60f;
            SaveLength();
            ResetTimer();
        }
    }
    public void AddFiveMinutes()
    {
        if (!isTimerRunning && studyDuration < 7200f) // 2 hours in seconds
        {
            studyDuration += 300f; // 5 minutes in seconds
            if (studyDuration > 7200f) studyDuration = 7200f; // Ensure it does not exceed 2 hours
            SaveLength();
            ResetTimer();
        }
    }
    public void RemoveFiveMinutes()
    {
        if (!isTimerRunning && studyDuration > 300f)
        {
            studyDuration -= 300f; // 5 minutes in seconds
            SaveLength();
            ResetTimer();
        }
    }
}