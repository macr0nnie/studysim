using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class HorrorStage
{
    [Tooltip("Activates when the player reaches this level")]
    public int triggerLevel = 1;

    [Header("Ambient Lighting")]
    public Color ambientColor = Color.white;
    [Range(0f, 2f)] public float ambientIntensity = 1f;

    [Header("Fog")]
    public bool fogEnabled = false;
    public Color fogColor = Color.grey;
    [Range(0f, 0.1f)] public float fogDensity = 0.01f;

    [Header("Post Processing (on the Horror Volume)")]
    [Range(0f, 1f)] public float colorGradingWeight = 0f;

    [Header("Scene Objects")]
    [Tooltip("GameObjects to enable at this stage (creepy props, etc.)")]
    public GameObject[] objectsToEnable;
    [Tooltip("GameObjects to disable at this stage (normal props, etc.)")]
    public GameObject[] objectsToDisable;

    [Header("Transition")]
    public float transitionDuration = 3f;
}

/// <summary>
/// Evolves the room atmosphere as the player levels up.
/// Stages are checked in order; the highest stage whose triggerLevel <= playerLevel is active.
/// Setup:
///   1. Add this component to a manager GameObject.
///   2. Assign the Experience reference.
///   3. Assign a second Volume (horrorVolume) for color grading that mixes in per stage.
///   4. Configure HorrorStage[] in the Inspector for each horror tier.
/// </summary>
public class RoomHorrorEvolution : MonoBehaviour
{
    [SerializeField] private Experience experience;
    [SerializeField] private Volume horrorVolume;
    [SerializeField] private HorrorStage[] stages;

    private int _currentStageIndex = -1;
    private Coroutine _transitionCoroutine;

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

    private void Start()
    {
        // Apply the correct stage for the player's current saved level silently
        int startStage = FindStageIndexForLevel(experience != null ? experience.Level : 1);
        if (startStage >= 0)
            ApplyStageImmediate(stages[startStage]);
    }

    private void OnLevelUp(int newLevel)
    {
        int newStageIndex = FindStageIndexForLevel(newLevel);
        if (newStageIndex == _currentStageIndex) return;

        _currentStageIndex = newStageIndex;
        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionToStage(stages[newStageIndex]));
    }

    private int FindStageIndexForLevel(int level)
    {
        int best = -1;
        for (int i = 0; i < stages.Length; i++)
        {
            if (stages[i].triggerLevel <= level)
                best = i;
        }
        return best;
    }

    private IEnumerator TransitionToStage(HorrorStage target)
    {
        Color startAmbient = RenderSettings.ambientLight;
        float startAmbientIntensity = RenderSettings.ambientIntensity;
        Color startFogColor = RenderSettings.fogColor;
        float startFogDensity = RenderSettings.fogDensity;
        float startVolumeWeight = horrorVolume != null ? horrorVolume.weight : 0f;

        float elapsed = 0f;
        while (elapsed < target.transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / target.transitionDuration);

            RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambientColor, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientIntensity, target.ambientIntensity, t);
            RenderSettings.fogColor = Color.Lerp(startFogColor, target.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, target.fogDensity, t);

            if (horrorVolume != null)
                horrorVolume.weight = Mathf.Lerp(startVolumeWeight, target.colorGradingWeight, t);

            yield return null;
        }

        ApplyStageImmediate(target);
        ToggleStageObjects(target);
    }

    private void ApplyStageImmediate(HorrorStage stage)
    {
        RenderSettings.ambientLight = stage.ambientColor;
        RenderSettings.ambientIntensity = stage.ambientIntensity;
        RenderSettings.fog = stage.fogEnabled;
        RenderSettings.fogColor = stage.fogColor;
        RenderSettings.fogDensity = stage.fogDensity;

        if (horrorVolume != null)
            horrorVolume.weight = stage.colorGradingWeight;

        ToggleStageObjects(stage);
    }

    private static void ToggleStageObjects(HorrorStage stage)
    {
        if (stage.objectsToEnable != null)
            foreach (GameObject go in stage.objectsToEnable)
                if (go != null) go.SetActive(true);

        if (stage.objectsToDisable != null)
            foreach (GameObject go in stage.objectsToDisable)
                if (go != null) go.SetActive(false);
    }
}
