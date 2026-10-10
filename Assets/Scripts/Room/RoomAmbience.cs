using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cozy room lighting, built at runtime so the scene stays untouched: a warm key light, a cool ambient fill,
// warm soft lamps, and a light URP Volume (bloom, vignette, grading). The room dims and warms while a study
// session runs or every lamp is off, and brightens on a break. Settings > Sound > Lighting picks Off / Cozy / Bright.
// Behind the room is a sky gradient that follows the clock (hidden while Paint > Background has a colour), and a few
// dust motes drift in the room.
// Mini mode owns shadows and post-processing while it is active, so this only turns the Volume off there.
public class RoomAmbience : MonoBehaviour
{
    public enum Style { Off, Cozy, Bright }

    // Per style: key colour/intensity, fill sky/ground, exposure shift, bloom, vignette (focus end / break end of the mood blend).
    private struct Look { public Color key, sky, ground; public float keyFocus, keyBreak, exposure, bloom, vignette, lampIntensity, lampRange; }

    private static readonly Look Cozy = new Look
    {
        key = new Color(1f, 0.82f, 0.62f), sky = new Color(0.55f, 0.62f, 0.8f), ground = new Color(0.22f, 0.17f, 0.14f),
        keyFocus = 0.75f, keyBreak = 1.0f, exposure = 0f, bloom = 0.1f, vignette = 0.32f, lampIntensity = 0.04f, lampRange = 0.5f,
    };
    private static readonly Look Bright = new Look
    {
        key = new Color(1f, 0.94f, 0.84f), sky = new Color(0.75f, 0.8f, 0.9f), ground = new Color(0.35f, 0.3f, 0.26f),
        keyFocus = 1.0f, keyBreak = 1.2f, exposure = 0.2f, bloom = 0.15f, vignette = 0.12f, lampIntensity = 0.1f, lampRange = 0.5f,
    };

    private const float LampScan = 2f, Fade = 1.5f, LampsOffDim = 0.65f;
    private static readonly Color LampColor = new Color(1f, 0.7f, 0.4f);

    // Sky by hour of the day: top and bottom of the gradient. Hours ascend from 0 to 24 so every hour has a pair.
    private static readonly (float hour, Color top, Color bottom)[] SkyKeys =
    {
        (0f, new Color(0.10f, 0.12f, 0.24f), new Color(0.24f, 0.21f, 0.36f)),
        (5f, new Color(0.10f, 0.12f, 0.24f), new Color(0.24f, 0.21f, 0.36f)),
        (7f, new Color(0.95f, 0.72f, 0.63f), new Color(0.98f, 0.87f, 0.77f)),
        (10f, new Color(0.97f, 0.84f, 0.74f), new Color(0.99f, 0.95f, 0.89f)),
        (16.5f, new Color(0.97f, 0.84f, 0.74f), new Color(0.99f, 0.95f, 0.89f)),
        (19f, new Color(0.42f, 0.35f, 0.56f), new Color(0.93f, 0.66f, 0.55f)),
        (21.5f, new Color(0.10f, 0.12f, 0.24f), new Color(0.24f, 0.21f, 0.36f)),
        (24f, new Color(0.10f, 0.12f, 0.24f), new Color(0.24f, 0.21f, 0.36f)),
    };
    public static bool SkyHidden; // set by PaintUI while a background colour is picked

    private static RoomAmbience instance;
    private RawImage skyImage, skyPhoto;
    private Texture2D skyTexture;
    private float skyClock;
    private ParticleSystem dust;
    private RoomManager room;
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
        room = FindAnyObjectByType<RoomManager>();
        BuildSky();
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
        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.12f);
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        volume.enabled = false;
    }

    private void OnDestroy()
    {
        if (volume != null) Destroy(volume.sharedProfile);
        if (skyTexture != null) Destroy(skyTexture);
        if (dust != null) { Destroy(dust.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture); Destroy(dust.GetComponent<ParticleSystemRenderer>().sharedMaterial); }
        if (instance == this) instance = null;
    }

    private void Update()
    {
        bool mini = MiniMode.Active;
        if (skyImage != null)
        {
            bool showSky = !SkyHidden && !mini; // mini mode's window needs the camera's own clear colour
            if (skyImage.enabled != showSky) { skyImage.enabled = showSky; if (skyPhoto != null) skyPhoto.enabled = showSky; }
            if (showSky && (skyClock -= Time.unscaledDeltaTime) <= 0) { skyClock = 30; PaintSky(); }
        }
        if (key == null) return;
        if ((scanClock -= Time.unscaledDeltaTime) <= 0) { scanClock = LampScan; ScanLamps(); if (dust == null) BuildDust(); }

        var style = GameSettings.Ambience;
        if (dust != null && dust.gameObject.activeSelf != (style != Style.Off && !mini)) dust.gameObject.SetActive(style != Style.Off && !mini);
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

    // A screen-space canvas at the far end of the camera, so the room draws in front of it and post-processing
    // (vignette, grain) covers it too. Not a raycast target, so it never blocks clicks on the room.
    private void BuildSky()
    {
        Debug.Assert(SkyKeys[0].hour == 0 && SkyKeys[SkyKeys.Length - 1].hour == 24, "SkyKeys must span 0 to 24");
        Camera cam = Camera.main;
        if (cam == null) return;
        var canvasGO = new GameObject("Sky", typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.farClipPlane * 0.95f;
        canvas.sortingOrder = -100;
        skyImage = new GameObject("Gradient", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        skyImage.transform.SetParent(canvasGO.transform, false);
        var rect = (RectTransform)skyImage.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        skyImage.raycastTarget = false;
        skyTexture = new Texture2D(1, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        skyImage.texture = skyTexture;
        skyImage.uvRect = new Rect(0, 0.25f, 1, 0.5f); // texel centre to texel centre: a full bottom-to-top blend

        // A cloudy daytime sky photo over the gradient, faded out towards night so the gradient's dusk and night show.
        // One static texture: the photo is never redrawn, only its tint and fade change every 30 seconds.
        var photo = Resources.Load<Texture2D>("RoomSky");
        if (photo != null)
        {
            skyPhoto = new GameObject("Clouds", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            skyPhoto.transform.SetParent(canvasGO.transform, false);
            rect = (RectTransform)skyPhoto.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            skyPhoto.raycastTarget = false;
            skyPhoto.texture = photo;
            skyPhoto.uvRect = new Rect(0.3f, 0.48f, 0.4f, 0.4f); // the wispy clouds just above the panorama's horizon
        }
        PaintSky();
    }

    private void PaintSky()
    {
        float hour = (float)DateTime.Now.TimeOfDay.TotalHours;
        int i = 0;
        while (i < SkyKeys.Length - 2 && hour > SkyKeys[i + 1].hour) i++;
        float t = Mathf.InverseLerp(SkyKeys[i].hour, SkyKeys[i + 1].hour, hour);
        Color bottom = Color.Lerp(SkyKeys[i].bottom, SkyKeys[i + 1].bottom, t), top = Color.Lerp(SkyKeys[i].top, SkyKeys[i + 1].top, t);
        skyTexture.SetPixel(0, 0, bottom);
        skyTexture.SetPixel(0, 1, top);
        skyTexture.Apply();
        if (skyPhoto != null)
        {
            Color tint = Color.Lerp(bottom, Color.white, 0.6f); // warm at dawn and dusk, plain by day
            tint.a = Mathf.InverseLerp(0.15f, 0.8f, top.grayscale); // gone at night, full by day
            skyPhoto.color = tint;
        }
    }

    // A few slow warm motes over the floor. Cheap: at most 40 particles, no lights or collisions.
    private void BuildDust()
    {
        if (room == null || room.FloorRenderers == null || room.FloorRenderers.Length == 0) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit"); // in Always Included Shaders, so builds have it
        if (shader == null) return;
        Bounds floor = new Bounds();
        bool any = false;
        foreach (Renderer r in room.FloorRenderers)
            if (r != null) { if (any) floor.Encapsulate(r.bounds); else floor = r.bounds; any = true; }
        if (!any) return;
        float size = Mathf.Max(floor.size.x, floor.size.z);

        var go = new GameObject("DustMotes");
        go.transform.SetParent(transform, false);
        go.transform.position = floor.center + Vector3.up * size * 0.3f;
        dust = go.AddComponent<ParticleSystem>();
        var main = dust.main;
        main.startLifetime = 10f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.004f, size * 0.009f);
        main.startColor = new Color(1f, 0.88f, 0.65f, 0.6f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        var emission = dust.emission;
        emission.rateOverTime = 4f;
        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(floor.size.x * 0.8f, size * 0.6f, floor.size.z * 0.8f);
        var noise = dust.noise;
        noise.enabled = true;
        noise.strength = size * 0.01f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.1f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var fade = dust.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;

        // Additive soft dots: they read as light catching dust and need no sorting.
        var material = new Material(shader);
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 2);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        var dot = new Texture2D(16, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8, 8)) / 8f;
                dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d)));
            }
        dot.Apply();
        material.mainTexture = dot; // _BaseMap on URP particle shaders
        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }
}
