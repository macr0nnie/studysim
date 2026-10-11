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
    // For a 25-minute session; rewards scale with the session's length.
    [SerializeField] private int baseMoneyReward = 100;
    [SerializeField] private int baseExperienceReward = 50;
    [SerializeField] private float focusBonus = 0.2f; // extra coins when no distracting site was reported

    public event Action<float> OnTimerTick;
    public event Action OnTimerComplete;
    public UnityEvent OnStudySessionComplete;
    
    private float currentTime;
    private bool isTimerRunning;
    private bool isStudySession = true;
    private bool distracted;
    
    public float CurrentTime => currentTime;
    public bool IsTimerRunning => isTimerRunning;
    public bool IsStudySession => isStudySession;
    public PlayerCurrency playerCurrency;
    public Button plusButton;
    public Button minusButton;

    private const string LengthKey = "StudySessionSeconds", BreakKey = "BreakSessionSeconds";

    private void Start()
    {
        // the player's chosen session length survives restarts
        // Scenes and old saves can carry a test value (1 second); anything under the shortest preset counts as unset.
        studyDuration = studyDuration < 300f ? 1500f : studyDuration;
        breakDuration = breakDuration < 60f ? 300f : breakDuration;
        studyDuration = PlayerPrefs.GetFloat(LengthKey, studyDuration);
        breakDuration = PlayerPrefs.GetFloat(BreakKey, breakDuration);
        if (studyDuration < 300f) studyDuration = 1500f;
        if (breakDuration < 60f) breakDuration = 300f;
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
            if (isStudySession && Distraction.Reported) distracted = true;
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
        // Strict focus: a study session can't be paused, only given up (no rewards).
        if (GameSettings.StrictFocus && isStudySession && isTimerRunning) { ResetTimer(); return; }
        isTimerRunning = false;
    }
    public void ResetTimer()
    {
        currentTime = isStudySession ? studyDuration : breakDuration;
        if (isStudySession) distracted = false;
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
        // Each phase can roll straight into the next (Settings > Focus).
        if (isStudySession ? GameSettings.AutoStartStudy : GameSettings.AutoStartBreak) StartTimer();
        OnTimerComplete?.Invoke();
    }

    // What the last finished session paid, for the session card.
    public int MoneyReward { get; private set; }
    public int ExperienceReward { get; private set; }
    public bool FocusBonus { get; private set; }
    public int LastSessionMinutes { get; private set; }
    public float StudyMinutes => studyDuration / 60f;
    public float BreakMinutes => breakDuration / 60f;

    private void SaveLength()
    {
        PlayerPrefs.SetFloat(LengthKey, studyDuration);
        PlayerPrefs.Save();
    }

    public void GrantRewards()
    {
        // A flat reward made five-minute sessions the best way to farm coins.
        LastSessionMinutes = Mathf.RoundToInt(studyDuration / 60f);
        float scale = studyDuration / 1500f;
        FocusBonus = !distracted;
        MoneyReward = Mathf.RoundToInt(baseMoneyReward * scale * (FocusBonus ? 1f + focusBonus : 1f));
        ExperienceReward = Mathf.RoundToInt(baseExperienceReward * scale);
        StudyLog.Record(LastSessionMinutes);
        if (playerCurrency != null) playerCurrency.AddCoins(MoneyReward);
        Experience experience = FindAnyObjectByType<Experience>();
        if (experience != null) experience.GainExperience(ExperienceReward);
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
    public void SetBreakDuration(float minutes)
    {
        if (isTimerRunning) return;
        breakDuration = Mathf.Clamp(minutes, 1f, 30f) * 60f;
        PlayerPrefs.SetFloat(BreakKey, breakDuration);
        PlayerPrefs.Save();
        ResetTimer();
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