using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Drives URP post-processing distortion and camera shake when ChromeWebEx
/// detects the player is on a distracting site.
/// Setup:
///   1. Assign the scene's Global Volume that has ChromaticAberration,
///      LensDistortion, and Vignette overrides added (even at 0 intensity).
///   2. Assign Room Lights so they flicker during distraction.
///   3. Camera is resolved automatically from Camera.main if left blank.
/// </summary>
public class GlitchEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChromeWebEx chromeWebEx;
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private Light[] roomLights;

    [Header("Glitch Settings")]
    [SerializeField] private float maxChromaticAberration = 1f;
    [SerializeField] private float maxLensDistortion = -0.5f;
    [SerializeField] private float maxVignetteIntensity = 0.65f;
    [SerializeField] private float cameraShakeAmplitude = 0.04f;
    [SerializeField] private float rampUpDuration = 10f; // seconds to reach full intensity
    [SerializeField] private float fadeOutDuration = 2f;

    [Header("Light Flicker")]
    [SerializeField] private float flickerSpeed = 8f;
    [SerializeField] private float flickerMinIntensity = 0.2f;

    private ChromaticAberration _ca;
    private LensDistortion _ld;
    private Vignette _vignette;

    private float[] _originalLightIntensities;
    private Vector3 _cameraOrigin;
    private float _currentIntensity;

    private Coroutine _activeCoroutine;

    private void Awake()
    {
        if (cameraRoot == null)
            cameraRoot = Camera.main?.transform;

        if (postProcessVolume != null && postProcessVolume.profile != null)
        {
            postProcessVolume.profile.TryGet(out _ca);
            postProcessVolume.profile.TryGet(out _ld);
            postProcessVolume.profile.TryGet(out _vignette);
        }

        if (roomLights != null)
        {
            _originalLightIntensities = new float[roomLights.Length];
            for (int i = 0; i < roomLights.Length; i++)
                _originalLightIntensities[i] = roomLights[i] != null ? roomLights[i].intensity : 1f;
        }
    }

    private void OnEnable()
    {
        if (chromeWebEx == null) return;
        chromeWebEx.OnDistractionDetected += HandleDistractionStart;
        chromeWebEx.OnFocusRestored += HandleFocusRestored;
    }

    private void OnDisable()
    {
        if (chromeWebEx == null) return;
        chromeWebEx.OnDistractionDetected -= HandleDistractionStart;
        chromeWebEx.OnFocusRestored -= HandleFocusRestored;
    }

    private void HandleDistractionStart(DistractionData _)
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        if (cameraRoot != null) _cameraOrigin = cameraRoot.localPosition;
        _activeCoroutine = StartCoroutine(GlitchRamp());
    }

    private void HandleFocusRestored()
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(GlitchFadeOut());
    }

    private IEnumerator GlitchRamp()
    {
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            _currentIntensity = Mathf.Clamp01(elapsed / rampUpDuration);
            ApplyEffects(_currentIntensity);
            yield return null;
        }
    }

    private IEnumerator GlitchFadeOut()
    {
        float startIntensity = _currentIntensity;
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            _currentIntensity = startIntensity * t;
            ApplyEffects(_currentIntensity);
            yield return null;
        }
        ApplyEffects(0f);
        RestoreLights();
        if (cameraRoot != null)
            cameraRoot.localPosition = _cameraOrigin;
        _currentIntensity = 0f;
    }

    private void ApplyEffects(float intensity)
    {
        // Post processing
        if (_ca != null) _ca.intensity.Override(intensity * maxChromaticAberration);
        if (_ld != null) _ld.intensity.Override(intensity * maxLensDistortion);
        if (_vignette != null) _vignette.intensity.Override(0.3f + intensity * (maxVignetteIntensity - 0.3f));

        // Camera shake
        if (cameraRoot != null && intensity > 0f)
        {
            Vector3 shake = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                0f
            ) * cameraShakeAmplitude * intensity;
            cameraRoot.localPosition = _cameraOrigin + shake;
        }

        // Light flicker
        if (roomLights == null) return;
        for (int i = 0; i < roomLights.Length; i++)
        {
            if (roomLights[i] == null) continue;
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, i * 13.7f);
            float minI = Mathf.Lerp(_originalLightIntensities[i], flickerMinIntensity, intensity);
            roomLights[i].intensity = Mathf.Lerp(minI, _originalLightIntensities[i], noise);
        }
    }

    private void RestoreLights()
    {
        if (roomLights == null) return;
        for (int i = 0; i < roomLights.Length; i++)
        {
            if (roomLights[i] != null)
                roomLights[i].intensity = _originalLightIntensities[i];
        }
    }
}
