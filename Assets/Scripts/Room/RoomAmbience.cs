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
// Behind the room is a dark plum backdrop that follows the clock (a rose haze, stars at night, weather from Settings > Display; hidden
// while Paint > Background has a colour), and a few dust motes drift in the room, with a soft shadow and pixel sparkles under it.
// Each window on a wall gets a soft spot light that shines into the room: daylight by day, moonlight at night, dimmer in bad weather.
// Mini mode owns shadows and post-processing while it is active, so this only turns the Volume off there.
public class RoomAmbience : MonoBehaviour
{
    public enum Style { Off, Cozy, Bright }
    public enum Weather { Clear, Drizzle, Rain, Snow, Changing }

    // Per style: key colour/intensity, fill sky/ground, exposure shift, bloom, vignette (focus end / break end of the mood blend).
    private struct Look { public Color key, sky, equator, ground; public float keyFocus, keyBreak, exposure, bloom, vignette, lampIntensity, lampRange; }

    private static readonly Look Cozy = new Look
    {
        // Soft and even: a warm fill on the walls and floor carries the room, the key light only adds a gentle direction.
        key = new Color(1f, 0.82f, 0.62f), sky = new Color(0.66f, 0.64f, 0.72f), equator = new Color(0.5f, 0.43f, 0.38f), ground = new Color(0.34f, 0.28f, 0.24f),
        keyFocus = 0.55f, keyBreak = 0.75f, exposure = 0f, bloom = 0.1f, vignette = 0.32f, lampIntensity = 0.04f, lampRange = 0.5f,
    };
    private static readonly Look Bright = new Look
    {
        key = new Color(1f, 0.94f, 0.84f), sky = new Color(0.82f, 0.84f, 0.9f), equator = new Color(0.62f, 0.58f, 0.54f), ground = new Color(0.45f, 0.4f, 0.36f),
        keyFocus = 0.85f, keyBreak = 1.05f, exposure = 0.2f, bloom = 0.15f, vignette = 0.12f, lampIntensity = 0.1f, lampRange = 0.5f,
    };

    private const float LampScan = 2f, Fade = 1.5f, LampsOffDim = 0.65f;
    private static readonly Color LampColor = new Color(1f, 0.7f, 0.4f);

    // Sky by hour of the day: top and bottom of the gradient. Hours ascend from 0 to 24 so every hour has a pair.
    // A dark plum backdrop at every hour (ronnie's reference, 2026-10-11): the room is the bright thing on screen, so
    // the day only lifts it a little and dusk warms it.
    private static readonly (float hour, Color top, Color bottom)[] SkyKeys =
    {
        (0f, new Color(0.10f, 0.07f, 0.12f), new Color(0.16f, 0.11f, 0.15f)),
        (5f, new Color(0.10f, 0.07f, 0.12f), new Color(0.16f, 0.11f, 0.15f)),
        (7f, new Color(0.22f, 0.13f, 0.20f), new Color(0.26f, 0.17f, 0.20f)),
        (10f, new Color(0.24f, 0.15f, 0.23f), new Color(0.27f, 0.20f, 0.24f)),
        (16.5f, new Color(0.24f, 0.15f, 0.23f), new Color(0.27f, 0.20f, 0.24f)),
        (19f, new Color(0.22f, 0.12f, 0.19f), new Color(0.30f, 0.17f, 0.19f)),
        (21.5f, new Color(0.10f, 0.07f, 0.12f), new Color(0.16f, 0.11f, 0.15f)),
        (24f, new Color(0.10f, 0.07f, 0.12f), new Color(0.16f, 0.11f, 0.15f)),
    };
    private static readonly Color SunColor = new Color(1f, 0.86f, 0.72f);
    public static bool SkyHidden; // set by PaintUI while a background colour is picked

    private static RoomAmbience instance;
    private Canvas skyCanvas;
    private RawImage skyImage, skyGlow, stars, rain, snow;
    private Texture2D skyTexture, starTexture, rainTexture, snowTexture, haloTexture;
    private float skyClock;
    private Weather weather;
    private const string WindowLightName = "WindowLight";
    private readonly List<Light> windowLights = new List<Light>();
    private Color windowColor = Color.white;
    private float windowIntensity = 0.5f;
    private AudioSource rainSound;
    private ParticleSystem dust;
    private Material shadowMaterial, sparkleMaterial;
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
        if (haloTexture != null) Destroy(haloTexture);
        if (starTexture != null) Destroy(starTexture);
        if (rainTexture != null) Destroy(rainTexture);
        if (snowTexture != null) Destroy(snowTexture);
        if (rainSound != null) Destroy(rainSound.clip);
        if (shadowMaterial != null) Destroy(shadowMaterial);
        if (sparkleMaterial != null) { Destroy(sparkleMaterial.mainTexture); Destroy(sparkleMaterial); }
        if (dust != null) { Destroy(dust.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture); Destroy(dust.GetComponent<ParticleSystemRenderer>().sharedMaterial); }
        if (instance == this) instance = null;
    }

    private void Update()
    {
        bool mini = MiniMode.Active;
        Weather now = CurrentWeather();
        if (now != weather) SetWeather(now);
        if (rainSound != null) rainSound.volume = GameSettings.EffectsVolume * (weather == Weather.Rain ? 0.2f : 0.1f);
        if (skyCanvas != null)
        {
            bool showSky = !SkyHidden && !mini; // mini mode's window needs the camera's own clear colour
            if (skyCanvas.enabled != showSky) skyCanvas.enabled = showSky;
            if (showSky && (skyClock -= Time.unscaledDeltaTime) <= 0) { skyClock = 30; PaintSky(); }
            if (showSky && (weather == Weather.Drizzle || weather == Weather.Rain))
            {
                Rect r = rain.uvRect;
                r.y = Mathf.Repeat(r.y + Time.unscaledDeltaTime * (weather == Weather.Rain ? 1.1f : 0.8f), 1f); // scrolling one small texture is the whole effect
                rain.uvRect = r;
            }
            if (showSky && weather == Weather.Snow)
            {
                Rect r = snow.uvRect;
                r.y = Mathf.Repeat(r.y + Time.unscaledDeltaTime * 0.06f, 1f);
                r.x = Mathf.Repeat(r.x + Mathf.Sin(Time.unscaledTime * 0.4f) * Time.unscaledDeltaTime * 0.03f, 1f); // a slow sideways drift
                snow.uvRect = r;
            }
        }
        if (key == null) return;
        if ((scanClock -= Time.unscaledDeltaTime) <= 0) { scanClock = LampScan; ScanLamps(); ScanWindows(); if (dust == null) BuildDust(); }

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
        key.shadowStrength = Mathf.Lerp(1f, 0.5f, strength); // soft shadows that ground pieces without dark patches
        if (ambientMode == AmbientMode.Trilight)
        {
            // The fill leans a little towards the window light, so the room follows the time of day.
            RenderSettings.ambientSkyColor = Color.Lerp(sky, Color.Lerp(look.sky, windowColor * look.sky.maxColorComponent, 0.25f), strength);
            RenderSettings.ambientEquatorColor = Color.Lerp(equator, look.equator, strength);
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
            if (light.type == LightType.Directional || light.name == WindowLightName || light.GetComponentInParent<Furniture>() == null) continue;
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

    // Windows come and go like lamps. Each gets one spot light from its middle towards the middle of the floor, re-aimed
    // every scan so a moved window still points into the room. Soft shadows; mini mode turns them off.
    private void ScanWindows()
    {
        windowLights.RemoveAll(l => l == null);
        if (!FloorBounds(out Bounds floor)) return;
        bool on = GameSettings.Ambience != Style.Off;
        foreach (Furniture piece in FindObjectsByType<Furniture>())
        {
            GameObject g = piece.gameObject;
            if (!FurnitureRole.NameHas(g, "window") || FurnitureRole.NameHas(g, "blind")) continue;
            Renderer[] renderers = g.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            Transform t = g.transform.Find(WindowLightName);
            Light light = t != null ? t.GetComponent<Light>() : null;
            if (light == null)
            {
                var go = new GameObject(WindowLightName);
                go.transform.SetParent(g.transform, false);
                light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 70f;
                light.innerSpotAngle = 40f; // a clear patch of sunlight on the floor, not a wash
                light.shadows = LightShadows.Soft; // the frame and furniture cast the shapes you see in the patch
                windowLights.Add(light);
            }
            light.transform.position = b.center;
            light.transform.rotation = Quaternion.LookRotation(floor.center - b.center);
            light.range = Vector3.Distance(b.center, floor.center) * 2f;
            light.color = windowColor;
            light.intensity = windowIntensity;
            light.enabled = on;
            light.shadows = MiniMode.Active ? LightShadows.None : LightShadows.Soft;
        }
    }

    private bool FloorBounds(out Bounds floor)
    {
        floor = new Bounds();
        bool any = false;
        if (room == null || room.FloorRenderers == null) return false;
        foreach (Renderer r in room.FloorRenderers)
            if (r != null) { if (any) floor.Encapsulate(r.bounds); else floor = r.bounds; any = true; }
        return any;
    }

    // Settings > Display picks the weather; Changing rolls new weather every three hours, mostly clear.
    // shortcut: winter (snow) is December to February, northern hemisphere only; add a hemisphere setting if asked.
    private static Weather CurrentWeather()
    {
        Weather chosen = GameSettings.Weather;
        if (chosen != Weather.Changing) return chosen;
        DateTime now = DateTime.Now;
        float roll = Hash(now.DayOfYear, now.Year % 100 * 8 + now.Hour / 3);
        bool winter = now.Month == 12 || now.Month <= 2;
        if (roll < 0.6f) return Weather.Clear;
        if (roll < 0.82f) return Weather.Drizzle;
        return winter ? Weather.Snow : roll < 0.92f ? Weather.Rain : Weather.Drizzle;
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
        skyCanvas = canvasGO.GetComponent<Canvas>();
        skyCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        skyCanvas.worldCamera = cam;
        skyCanvas.planeDistance = cam.farClipPlane * 0.95f;
        skyCanvas.sortingOrder = -100;
        skyTexture = new Texture2D(1, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        skyImage = SkyLayer("Gradient", skyTexture, new Rect(0, 0.25f, 1, 0.5f)); // texel centre to texel centre: a full bottom-to-top blend

        // Stars fade in at night: one small generated texture, tiled.
        starTexture = Dots(256, 256, 90, 1, (x, y) => new Color(1f, 1f, 1f, 0.5f + Hash(x, y) * 0.5f));
        stars = SkyLayer("Stars", starTexture, new Rect(0, 0, 2f * cam.aspect, 2f));

        // A soft glow behind the room instead of a sky photo (ronnie found the photo looked unpolished): the room
        // floats in a warm pool of light that follows the time-of-day colours.
        haloTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var glow = new Color32[128 * 128];
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float d = Mathf.Clamp01(new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f);
                glow[y * 128 + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, d)); // smooth falloff, no hard edge
            }
        haloTexture.SetPixels32(glow);
        haloTexture.Apply();
        skyGlow = SkyLayer("Glow", haloTexture, new Rect(0, 0, 1, 1));
        var glowRect = (RectTransform)skyGlow.transform;
        glowRect.anchorMin = new Vector2(-0.1f, -0.35f); glowRect.anchorMax = new Vector2(1.1f, 1.35f); // wider than the screen, centred on the room

        // Rain: a few short slanted streaks on one small tiled texture, scrolled down while it rains. It used to be twice as
        // dense, longer and brighter, which read as a storm; drizzle is the same layer, fainter.
        rainTexture = Dots(64, 128, 8, 12, (x, y) => new Color(0.85f, 0.9f, 1f, 0.12f + Hash(x, y) * 0.18f));
        rain = SkyLayer("Rain", rainTexture, new Rect(0, 0, 5f * cam.aspect, 2.5f));
        rain.gameObject.SetActive(false);
        // Snow: soft dots drifting slowly.
        snowTexture = Dots(128, 128, 30, 2, (x, y) => new Color(1f, 1f, 1f, 0.5f + Hash(x, y) * 0.4f));
        snow = SkyLayer("Snow", snowTexture, new Rect(0, 0, 3f * cam.aspect, 2f));
        snow.gameObject.SetActive(false);
        PaintSky();
    }

    private RawImage SkyLayer(string name, Texture texture, Rect uv)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(skyCanvas.transform, false);
        var rect = (RectTransform)image.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        image.texture = texture;
        image.uvRect = uv;
        return image;
    }

    // A transparent, tiling texture with `count` marks at seeded random spots, each `length` pixels tall and leaning
    // one pixel right every six down (1 = a single dot).
    private static Texture2D Dots(int width, int height, int count, int length, Func<int, int, Color> color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[width * height];
        var rng = new System.Random(7);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(width), y = rng.Next(height);
            Color c = color(x, y);
            for (int j = 0; j < length; j++)
                pixels[(y - j + height) % height * width + (x + j / 6) % width] = c;
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static float Hash(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);

    private void SetWeather(Weather now)
    {
        weather = now;
        bool on = now == Weather.Drizzle || now == Weather.Rain;
        skyClock = 0; // repaint: weather greys the sky and hides the stars
        if (rain != null)
        {
            rain.gameObject.SetActive(on);
            rain.color = new Color(1f, 1f, 1f, now == Weather.Rain ? 0.45f : 0.22f);
        }
        if (snow != null) snow.gameObject.SetActive(now == Weather.Snow);
        if (on && rainSound == null)
        {
            rainSound = gameObject.AddComponent<AudioSource>();
            rainSound.clip = RainLoop();
            rainSound.loop = true;
            rainSound.spatialBlend = 0;
        }
        if (rainSound == null) return;
        if (!on) rainSound.Stop();
        else if (!rainSound.isPlaying) rainSound.Play();
    }

    // Three seconds of softened noise, made once in code: the patter of steady rain, no audio file needed.
    private static AudioClip RainLoop()
    {
        const int rate = 22050, length = rate * 3, fade = rate / 4;
        var data = new float[length + fade];
        var rng = new System.Random(3);
        float low = 0, slow = 0;
        for (int i = 0; i < data.Length; i++)
        {
            low += ((float)rng.NextDouble() * 2 - 1 - low) * 0.2f; // take the edge off the hiss
            slow += (low - slow) * 0.01f; // and the rumble out
            data[i] = (low - slow) * 0.8f;
        }
        // Fade the extra tail into the start, so the loop point is seamless.
        var loop = new float[length];
        Array.Copy(data, loop, length);
        for (int i = 0; i < fade; i++)
        {
            float t = (float)i / fade;
            loop[i] = data[i] * t + data[length + i] * (1 - t);
        }
        AudioClip clip = AudioClip.Create("Rain", length, 1, rate, false);
        clip.SetData(loop, 0);
        return clip;
    }

    private void PaintSky()
    {
        float hour = (float)DateTime.Now.TimeOfDay.TotalHours;
        int i = 0;
        while (i < SkyKeys.Length - 2 && hour > SkyKeys[i + 1].hour) i++;
        float t = Mathf.InverseLerp(SkyKeys[i].hour, SkyKeys[i + 1].hour, hour);
        Color bottom = Color.Lerp(SkyKeys[i].bottom, SkyKeys[i + 1].bottom, t), top = Color.Lerp(SkyKeys[i].top, SkyKeys[i + 1].top, t);
        float day = Mathf.Clamp01(Mathf.Min(hour - 6f, 20f - hour) / 2f); // 0 at night, 1 from 8:00 to 18:00
        bool clear = weather == Weather.Clear;
        float grey = weather == Weather.Rain ? 0.65f : weather == Weather.Clear ? 0f : 0.45f;
        bottom = Overcast(bottom, grey); top = Overcast(top, grey);
        skyTexture.SetPixel(0, 0, bottom);
        skyTexture.SetPixel(0, 1, top);
        skyTexture.Apply();
        stars.color = new Color(1f, 1f, 1f, clear ? 1f - day : 0f);

        // Window light: warm sunlight by day, a cool moonlight at night, dimmer when it's grey out.
        windowColor = Color.Lerp(new Color(0.55f, 0.65f, 1f), SunColor, day);
        windowIntensity = Mathf.Lerp(0.1f, 1.6f, day) * (weather == Weather.Rain ? 0.45f : clear ? 1f : 0.65f);
        foreach (Light light in windowLights) if (light != null) { light.color = windowColor; light.intensity = windowIntensity; }

        if (skyGlow != null)
        {
            Color tint = Color.Lerp(bottom, new Color(0.85f, 0.5f, 0.6f), 0.5f); // a faint rose haze behind the room
            tint.a = Mathf.Lerp(0.2f, 0.35f, day);
            skyGlow.color = tint;
        }
    }

    // Grey and a little darker, for a cloudy sky.
    private static Color Overcast(Color c, float amount)
    {
        float g = c.grayscale * 0.8f;
        return Color.Lerp(c, new Color(g, g, g, 1f), amount);
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

        // Pixel sparkles drifting up from below the room, so it hangs in the dark like a diorama: square dots, no
        // filtering, a pale rose glow that twinkles out.
        GameObject sparkleGO = Instantiate(go, go.transform);
        sparkleGO.name = "Sparkles";
        sparkleGO.transform.position = new Vector3(floor.center.x, floor.min.y - size * 0.25f, floor.center.z);
        ParticleSystem sparkles = sparkleGO.GetComponent<ParticleSystem>();
        var sMain = sparkles.main;
        sMain.startLifetime = 9f;
        sMain.startSpeed = new ParticleSystem.MinMaxCurve(size * 0.005f, size * 0.015f);
        sMain.startSize = new ParticleSystem.MinMaxCurve(size * 0.006f, size * 0.01f);
        sMain.startColor = new Color(1f, 0.85f, 0.92f, 0.9f);
        sMain.maxParticles = 140;
        var sEmission = sparkles.emission;
        sEmission.rateOverTime = 14f;
        var sShape = sparkles.shape;
        sShape.scale = new Vector3(size * 1.6f, size * 1.6f, size * 0.3f); // thin along Z, which the rotation turns upright
        sShape.rotation = new Vector3(-90, 0, 0); // the box emits along +Z; tip it so they rise
        var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        sparkleMaterial = new Material(material) { mainTexture = pixel };
        sparkleGO.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkleMaterial;

        // Soft dark disc just under the floor, a child of the motes so both hide together.
        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(shadow.GetComponent<Collider>()); // never catch room clicks
        shadow.name = "RoomShadow";
        shadow.transform.SetParent(go.transform, false);
        shadow.transform.SetPositionAndRotation(new Vector3(floor.center.x, floor.min.y - size * 0.02f, floor.center.z), Quaternion.Euler(90, 0, 0));
        shadow.transform.localScale = Vector3.one * size * 1.4f / go.transform.lossyScale.x;
        Mesh quad = shadow.GetComponent<MeshFilter>().mesh;
        var white = new Color[quad.vertexCount];
        for (int i = 0; i < white.Length; i++) white[i] = Color.white; // the particle shader multiplies by vertex colour
        quad.colors = white;
        shadowMaterial = new Material(material);
        shadowMaterial.SetFloat("_Blend", 0);
        shadowMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        shadowMaterial.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.1f, 0.5f));
        shadowMaterial.renderQueue = (int)RenderQueue.Transparent - 10;
        var shadowRenderer = shadow.GetComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = shadowMaterial;
        shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;

        if (skyTexture != null) PaintSky();
    }
}
