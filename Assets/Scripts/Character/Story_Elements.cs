using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class AmbientHorrorEvent
{
    [Tooltip("Level at which this event fires (fires once, ever)")]
    public int triggerLevel;
    [Tooltip("Short description for the Inspector")]
    public string description;
    [Tooltip("Objects that appear when this event fires")]
    public GameObject[] appear;
    [Tooltip("Objects that disappear when this event fires")]
    public GameObject[] disappear;
    [Tooltip("Optional one-shot sound")]
    public AudioClip sound;
    [Tooltip("How long to wait before applying the changes (dramatic pause)")]
    public float delaySeconds = 0f;
}

/// <summary>
/// Fires small ambient horror events at specific player levels.
/// Works alongside RoomHorrorEvolution (which handles broad atmosphere) and
/// StoryManager (which handles cutscene dialogue). This handles in-room
/// environmental storytelling: objects appearing, disappearing, sounds playing.
///
/// Setup:
///   1. Attach to a manager GameObject in the scene.
///   2. Assign Experience reference.
///   3. Assign an AudioSource for one-shot sounds.
///   4. Populate the AmbientHorrorEvent[] array in the Inspector.
///      Each event fires once and is persisted via PlayerPrefs.
/// </summary>
public class StoryElements : MonoBehaviour
{
    [SerializeField] private Experience experience;
    [SerializeField] private AudioSource ambientAudioSource;
    [SerializeField] private AmbientHorrorEvent[] events;

    private const string SaveKey = "SeenHorrorEvents";

    private void OnEnable()
    {
        if (experience != null)
            experience.OnLevelUp += OnLevelUp;
    }

    private void OnDisable()
    {
        if (experience != null)
            experience.OnLevelUp -= OnLevelUp;
    }

    private void OnLevelUp(int newLevel)
    {
        if (events == null) return;
        foreach (AmbientHorrorEvent evt in events)
        {
            if (evt.triggerLevel == newLevel && !HasFired(evt.triggerLevel))
                StartCoroutine(FireEvent(evt));
        }
    }

    private IEnumerator FireEvent(AmbientHorrorEvent evt)
    {
        if (evt.delaySeconds > 0f)
            yield return new WaitForSeconds(evt.delaySeconds);

        if (evt.appear != null)
            foreach (GameObject go in evt.appear)
                if (go != null) go.SetActive(true);

        if (evt.disappear != null)
            foreach (GameObject go in evt.disappear)
                if (go != null) go.SetActive(false);

        if (evt.sound != null && ambientAudioSource != null)
            ambientAudioSource.PlayOneShot(evt.sound);

        MarkFired(evt.triggerLevel);
    }

    // Called by other systems (e.g. StoryManager) to manually trigger a named event
    public void TriggerSideQuest(string questName)
    {
        Debug.Log($"[StoryElements] Side quest triggered: {questName}");
    }

    public void CompleteStoryElement(string elementName)
    {
        Debug.Log($"[StoryElements] Story element completed: {elementName}");
    }

    public void StartMainStory()
    {
        if (experience != null)
            OnLevelUp(experience.Level);
    }

    private bool HasFired(int level)
    {
        string saved = PlayerPrefs.GetString(SaveKey, "");
        foreach (string s in saved.Split(','))
            if (s == level.ToString()) return true;
        return false;
    }

    private void MarkFired(int level)
    {
        string saved = PlayerPrefs.GetString(SaveKey, "");
        string updated = string.IsNullOrEmpty(saved) ? level.ToString() : saved + "," + level;
        PlayerPrefs.SetString(SaveKey, updated);
        PlayerPrefs.Save();
    }
}
