using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Cozy room lighting, built at runtime so the scene stays untouched: a warm key light, a cool ambient fill,
// warm soft lamps, and a light URP Volume (bloom, vignette, grading). The room dims and warms while a study
// session runs or every lamp is off, and brightens on a break. Settings > Sound > Lighting picks Off / Cozy / Bright.
// Mini mode owns shadows and post-processing while it is active, so this only turns the Volume off there.
public class RoomAmbience : MonoBehaviour
{
    public enum Style { Off, Cozy, Bright }

    // Per style: key colour/intensity, fill sky/ground, exposure shift, bloom, vignette (focus end / break end of the mood blend).
    private struct Look { public Color key, sky, ground; public float keyFocus, keyBreak, exposure, bloom, vignette, lampIntensity, lampRange; }

    private static readonly Look Cozy = new Look
    {
        key = new Color(1f, 0.82f, 0.62f), sky = new Color(0.55f, 0.62f, 0.8f), ground = new Color(0.22f, 0.17f, 0.14f),
        keyFocus = 0.75f, keyBreak = 1.0f, exposure = 0f, bloom = 0.1f, vignette = 0.28f, lampIntensity = 0.05f, lampRange = 0.4f,
    };
    private static readonly Look Bright = new Look
    {
        key = new Color(1f, 0.94f, 0.84f), sky = new Color(0.75f, 0.8f, 0.9f), ground = new Color(0.35f, 0.3f, 0.26f),
        keyFocus = 1.0f, keyBreak = 1.2f, exposure = 0.2f, bloom = 0.15f, vignette = 0.12f, lampIntensity = 0.1f, lampRange = 0.5f,
    };

    private const float LampScan = 2f, Fade = 1.5f, LampsOffDim = 0.65f;
    private static readonly Color LampColor = new Color(1f, 0.7f, 0.4f);

    private static RoomAmbience instance;
    private readonly List<Light> lamps = new List<Light>();
    private readonly Dictionary<Light, Vector2> lampBase = new Dictionary<Light, Vector2>(); // authored (intensity, range)
    private Style lampStyle = (Style)(-1);
    private Light key;
    private TimerManager timer;
    private Volume volume;
    private Bloom bloom;
    private Vignette vignette;
    private ColorAdjustments grade;
    private float scanClock, focus = 1, lampsLit = 1, strength;
    private float focusVel, litVel, strengthVel;

    // Original scene values, so Off puts everything back.
    private Color keyColor, sky, equator, ground;
    private float keyIntensity;
    private LightShadows keyShadows;
    private AmbientMode ambientMode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => Ensure();
        Ensure();
    }

    private static void Ensure()
    {
        if (FindAnyObjectByType<RoomManager>() != null && instance == null)
            instance = new GameObject("RoomAmbience").AddComponent<RoomAmbience>();
    }

    public static void Refresh() { if (instance != null) instance.scanClock = 0; }

    private void Start()
    {
        timer = FindAnyObjectByType<TimerManager>();
        foreach (Light light in FindObjectsByType<Light>())
            if (light.type == LightType.Directional && (key == null || light.intensity > key.intensity)) key = light;
        if (key != null) { keyColor = key.color; keyIntensity = key.intensity; keyShadows = key.shadows; }
        ambientMode = RenderSettings.ambientMode;
        sky = RenderSettings.ambientSkyColor; equator = RenderSettings.ambientEquatorColor; ground = RenderSettings.ambientGroundColor;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.1f);
        bloom.scatter.Override(0.85f);
        vignette = profile.Add<Vignette>(true);
        vignette.smoothness.Override(0.6f);
        grade = profile.Add<ColorAdjustments>(true);
        grade.saturation.Override(8f);
        grade.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        volume.enabled = false;
    }

    private void OnDestroy()
    {
        if (volume != null) Destroy(volume.sharedProfile);
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (key == null) return;
        if ((scanClock -= Time.unscaledDeltaTime) <= 0) { scanClock = LampScan; ScanLamps(); }

        var style = GameSettings.Ambience;
        bool mini = MiniMode.Active;
        bool study = timer != null && timer.IsTimerRunning && timer.IsStudySession;
        int lit = 0;
        foreach (Light lamp in lamps) if (lamp != null && lamp.enabled && lamp.gameObject.activeInHierarchy) lit++;

        float dt = Time.unscaledDeltaTime;
        strength = Mathf.SmoothDamp(strength, style == Style.Off ? 0 : 1, ref strengthVel, Fade, Mathf.Infinity, dt);
        focus = Mathf.SmoothDamp(focus, study ? 1 : 0, ref focusVel, Fade * 2, Mathf.Infinity, dt);
        lampsLit = Mathf.SmoothDamp(lampsLit, lamps.Count == 0 || lit > 0 ? 1 : 0, ref litVel, Fade, Mathf.Infinity, dt);

        Look look = style == Style.Bright ? Bright : Cozy;
        float mood = Mathf.Lerp(look.keyBreak, look.keyFocus, focus) * Mathf.Lerp(LampsOffDim, 1f, lampsLit);
        float warm = Mathf.Clamp01(focus * 0.5f + (1 - lampsLit) * 0.5f);

        // strength 0 is the scene as it was authored.
        key.color = Color.Lerp(keyColor, Color.Lerp(look.key, look.key * new Color(1f, 0.9f, 0.8f), warm), strength);
        key.intensity = Mathf.Lerp(keyIntensity, keyIntensity * mood, strength);
        key.shadows = mini ? LightShadows.None : (strength > 0.5f ? LightShadows.Soft : keyShadows);
        key.shadowStrength = Mathf.Lerp(1f, 0.7f, strength);
        if (ambientMode == AmbientMode.Trilight)
        {
            RenderSettings.ambientSkyColor = Color.Lerp(sky, look.sky, strength);
            RenderSettings.ambientEquatorColor = Color.Lerp(equator, Color.Lerp(look.sky, look.ground, 0.5f) * 0.6f, strength);
            RenderSettings.ambientGroundColor = Color.Lerp(ground, look.ground, strength);
        }

        bool post = strength > 0.01f && !mini;
        volume.enabled = post;
        if (!post) return;
        volume.weight = strength;
        bloom.intensity.Override(look.bloom * Mathf.Lerp(0.3f, 1f, lampsLit));
        vignette.intensity.Override(look.vignette * Mathf.Lerp(0.8f, 1.2f, focus));
        grade.postExposure.Override(look.exposure);
        Camera cam = Camera.main;
        if (cam != null) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
    }

    // Lamps come and go as furniture is placed or deleted, so rescan now and then. A lamp is dimmed and shortened
    // from its authored values only when it is new or the style changed (the distraction effect flickers intensity
    // in between), and gets a warm tint and no shadows once.
    private void ScanLamps()
    {
        lamps.Clear();
        Look look = GameSettings.Ambience == Style.Bright ? Bright : Cozy;
        bool restyle = GameSettings.Ambience != lampStyle;
        lampStyle = GameSettings.Ambience;
        foreach (Light light in FindObjectsByType<Light>())
        {
            if (light.type == LightType.Directional || light.GetComponentInParent<Furniture>() == null) continue;
            lamps.Add(light);
            bool fresh = !lampBase.ContainsKey(light);
            if (fresh)
            {
                lampBase[light] = new Vector2(light.intensity, light.range);
                light.color = Color.Lerp(light.color, LampColor, 0.8f);
                light.shadows = LightShadows.None;
            }
            if (fresh || restyle)
            {
                Vector2 b = lampBase[light];
                bool off = lampStyle == Style.Off;
                light.intensity = b.x * (off ? 1 : look.lampIntensity);
                light.range = b.y * (off ? 1 : look.lampRange);
            }
        }
    }
}
