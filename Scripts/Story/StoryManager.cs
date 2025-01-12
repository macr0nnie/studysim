using UnityEngine;
using System.Collections.Generic;

public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    [System.Serializable]
    public class StoryEvent
    {
        public string eventId;
        public int requiredFocusMinutes;
        public bool hasTriggered;
        public UnityEngine.Events.UnityEvent onEventTriggered;
    }

    [SerializeField] private bool isStoryModeEnabled;
    [SerializeField] private List<StoryEvent> storyEvents;
    
    private int totalFocusMinutes;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        LoadProgress();
    }

    public void EnableStoryMode(bool enable)
    {
        isStoryModeEnabled = enable;
    }

    public void AddFocusTime(float focusTimeInSeconds)
    {
        if (!isStoryModeEnabled) return;

        int minutes = Mathf.RoundToInt(focusTimeInSeconds / 60f);
        totalFocusMinutes += minutes;
        CheckStoryProgression();
        SaveProgress();
    }

    private void CheckStoryProgression()
    {
        foreach (var storyEvent in storyEvents)
        {
            if (!storyEvent.hasTriggered && totalFocusMinutes >= storyEvent.requiredFocusMinutes)
            {
                TriggerStoryEvent(storyEvent);
            }
        }
    }

    private void TriggerStoryEvent(StoryEvent storyEvent)
    {
        storyEvent.hasTriggered = true;
        storyEvent.onEventTriggered?.Invoke();
        SaveProgress();
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt("TotalFocusMinutes", totalFocusMinutes);
        foreach (var storyEvent in storyEvents)
        {
            PlayerPrefs.SetInt("StoryEvent_" + storyEvent.eventId, storyEvent.hasTriggered ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    private void LoadProgress()
    {
        totalFocusMinutes = PlayerPrefs.GetInt("TotalFocusMinutes", 0);
        foreach (var storyEvent in storyEvents)
        {
            storyEvent.hasTriggered = PlayerPrefs.GetInt("StoryEvent_" + storyEvent.eventId, 0) == 1;
        }
    }

    public bool IsStoryModeEnabled()
    {
        return isStoryModeEnabled;
    }
}