using UnityEngine;
using TMPro;

/// <summary>
/// Manages the study timer functionality, including pomodoro sessions
/// Handles timer UI, controls, and session completion rewards
/// </summary>
public class Timer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI minutesText;
    [SerializeField] private GameObject startButton;
    [SerializeField] private GameObject pauseButton;
    
    [Header("Timer Settings")]
    [SerializeField] private float defaultStudyTime = 25f;
    [SerializeField] private float minStudyTime = 5f;
    [SerializeField] private float maxStudyTime = 120f;
    [SerializeField] private float timeStep = 5f;

    private float currentTime;
    private float setTime;
    private bool isRunning;
    private bool isPaused;

    private void Start()
    {
        SetTime(defaultStudyTime);
        SetupButtons();
    }

    /// <summary>
    /// Sets up initial button states
    /// </summary>
    private void SetupButtons()
    {
        if (startButton != null)
            startButton.SetActive(true);
            
        if (pauseButton != null)
            pauseButton.SetActive(false);
    }

    private void Update()
    {
        if (isRunning && !isPaused)
        {
            currentTime -= Time.deltaTime * 60f;  // Convert to minutes
            
            if (currentTime <= 0)
            {
                TimerComplete();
                return;
            }
            
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Updates the timer display text
    /// </summary>
    private void UpdateDisplay()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60f);
            int seconds = Mathf.FloorToInt(currentTime % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    /// <summary>
    /// Updates the minutes display text
    /// </summary>
    private void UpdateMinutesDisplay()
    {
        if (minutesText != null)
        {
            float minutes = setTime / 60f;
            minutesText.text = $"{minutes:0} min";
        }
    }

    /// <summary>
    /// Increases the timer duration by the time step
    /// </summary>
    public void IncreaseTime()
    {
        if (isRunning) return;
        
        float newTime = Mathf.Min(setTime + (timeStep * 60f), maxStudyTime * 60f);
        SetTime(newTime / 60f);
    }

    /// <summary>
    /// Decreases the timer duration by the time step
    /// </summary>
    public void DecreaseTime()
    {
        if (isRunning) return;
        
        float newTime = Mathf.Max(setTime - (timeStep * 60f), minStudyTime * 60f);
        SetTime(newTime / 60f);
    }

    /// <summary>
    /// Starts or resumes the timer
    /// </summary>
    public void StartTimer()
    {
        if (isPaused)
        {
            isPaused = false;
            GameEvents.OnSessionStarted.Invoke();
        }
        else if (!isRunning)
        {
            isRunning = true;
            GameEvents.OnSessionStarted.Invoke();
        }

        if (startButton != null)
            startButton.SetActive(false);
        if (pauseButton != null)
            pauseButton.SetActive(true);
    }

    /// <summary>
    /// Pauses the timer
    /// </summary>
    public void PauseTimer()
    {
        if (!isRunning) return;
        
        isPaused = true;
        GameEvents.OnSessionPaused.Invoke();
        
        if (startButton != null)
            startButton.SetActive(true);
        if (pauseButton != null)
            pauseButton.SetActive(false);
    }

    /// <summary>
    /// Resets the timer to its initial set duration
    /// </summary>
    public void ResetTimer()
    {
        isRunning = false;
        isPaused = false;
        currentTime = setTime;
        
        UpdateDisplay();
        
        if (startButton != null)
            startButton.SetActive(true);
        if (pauseButton != null)
            pauseButton.SetActive(false);
    }

    /// <summary>
    /// Called when the timer reaches zero
    /// </summary>
    private void TimerComplete()
    {
        isRunning = false;
        currentTime = 0;
        UpdateDisplay();

        if (startButton != null)
            startButton.SetActive(true);
        if (pauseButton != null)
            pauseButton.SetActive(false);

        // Award completion and notify listeners
        CurrencyManager.Instance.AwardSessionCompletion();
        GameEvents.OnSessionCompleted.Invoke();
    }

    /// <summary>
    /// Sets the timer duration in minutes
    /// </summary>
    /// <param name="minutes">Duration in minutes</param>
    public void SetTime(float minutes)
    {
        setTime = minutes * 60f;
        currentTime = setTime;
        UpdateDisplay();
        UpdateMinutesDisplay();
    }

    /// <summary>
    /// Gets the current time remaining in seconds
    /// </summary>
    /// <returns>Time remaining in seconds</returns>
    public float GetCurrentTime()
    {
        return currentTime;
    }

    /// <summary>
    /// Checks if the timer is currently running
    /// </summary>
    /// <returns>True if timer is running and not paused</returns>
    public bool IsRunning()
    {
        return isRunning && !isPaused;
    }
}