using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The room falls apart while the player is on a distracting site during a study session. The browser
// extension reports it ({"action":"distracted","site":"youtube.com"}, re-sent while the site stays open);
// furniture tilts, wobbles and sags, the lights flicker, and coins drain. Back on task ("focus" or "back", or no
// report for Timeout seconds) the room puts itself back together. Settings > Focus can turn it off.
public class Distraction : MonoBehaviour
{
    private const float Timeout = 45f;      // seconds without a "distracted" report before it counts as over
    private const float FallSeconds = 8f;   // time for the room to fall apart completely
    private const float MendSeconds = 1.5f;
    private const float MaxTilt = 16f;

    private static Distraction instance;
    private static float lastReport = -999f;
    public static string Site { get; private set; } = "";

    // True from the first report until the room has finished mending: furniture can't be edited meanwhile.
    public static bool Busy => instance != null && instance.rest.Count > 0;

    private struct Rest
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 axis;   // which way it keels over
        public float delay;    // pieces go one after another, not all at once
        public float phase;
        public bool wall;
    }

    private readonly Dictionary<Transform, Rest> rest = new Dictionary<Transform, Rest>();
    private readonly Dictionary<Light, float> lights = new Dictionary<Light, float>();
    private RoomManager room;
    private TimerManager timer;
    private PlayerCurrency currency;
    private float level, coinClock;
    private bool active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => Ensure();
        Ensure();
    }

    private static void Ensure()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<Distraction>() == null)
            new GameObject("Distraction").AddComponent<Distraction>();
    }

    public static void Report(string site)
    {
        lastReport = Time.unscaledTime;
        // Shown in a toast: drop rich-text tags and keep it short, since anything on this PC can send it.
        string clean = (site ?? "").Replace("<", "").Replace(">", "").Trim();
        Site = clean.Length == 0 ? "A distracting site" : clean.Length > 60 ? clean.Substring(0, 60) : clean;
    }

    public static void Clear() => lastReport = -999f;
    public static bool Reported => Time.unscaledTime - lastReport < Timeout;

    // Where a piece really is, ignoring the shaking, so saving the room never stores a fallen piece.
    public static void RestPose(Transform t, out Vector3 position, out Quaternion rotation)
    {
        if (instance != null && instance.rest.TryGetValue(t, out Rest r)) { position = r.position; rotation = r.rotation; }
        else { position = t.position; rotation = t.rotation; }
    }

    private void Awake()
    {
        instance = this;
        room = FindFirstObjectByType<RoomManager>();
        timer = FindFirstObjectByType<TimerManager>();
        currency = FindFirstObjectByType<PlayerCurrency>();
    }

    private void Update()
    {
        bool want = GameSettings.DistractionPenalty && Reported && timer != null && timer.IsTimerRunning && timer.IsStudySession;
        if (want && !active) Begin();
        if (!want && active) End();

        level = Mathf.MoveTowards(level, want ? 1 : 0, Time.deltaTime / (want ? FallSeconds : MendSeconds));
        if (rest.Count > 0) Animate();
        if (level <= 0 && !want && rest.Count > 0) Restore();

        if (want && currency != null && GameSettings.DistractionCoinsPerMinute > 0)
        {
            coinClock += Time.deltaTime;
            float every = 60f / GameSettings.DistractionCoinsPerMinute;
            while (coinClock >= every)
            {
                coinClock -= every;
                if (currency.SpendCoins(1)) UISound.Play(UISound.Cue.Coin);
            }
        }
    }

    private void Begin()
    {
        active = true;
        coinClock = 0;
        if (rest.Count == 0) // not still mending from the last time
        {
            if (room != null)
                foreach (GameObject piece in room.PlacedPieces)
                {
                    if (piece == null || !piece.activeInHierarchy) continue;
                    Vector2 dir = Random.insideUnitCircle.normalized;
                    rest[piece.transform] = new Rest
                    {
                        position = piece.transform.position,
                        rotation = piece.transform.rotation,
                        axis = new Vector3(dir.x, 0, dir.y),
                        delay = Random.Range(0f, 0.45f),
                        phase = Random.Range(0f, 10f),
                        wall = piece.TryGetComponent(out Furniture f) && f.Type == Furniture.FurnitureType.Wall,
                    };
                }
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type != LightType.Directional) lights[light] = light.intensity;
        }
        UISound.Play(UISound.Cue.Warning);
        Toast($"{Site} is pulling you away. Your room is falling apart, and it's costing you coins!");
    }

    private void End()
    {
        active = false;
        if (rest.Count == 0) return;
        UISound.Play(UISound.Cue.Recovered);
        Toast("Back on task. Your room is putting itself back together.");
    }

    private void Animate()
    {
        float time = Time.time;
        foreach (var pair in rest)
        {
            if (pair.Key == null) continue;
            Rest r = pair.Value;
            float amount = Mathf.SmoothStep(0, 1, Mathf.Clamp01((level - r.delay) / (1 - r.delay)));
            float wobble = Mathf.Sin(time * 7f + r.phase) * 3f * amount;
            // Wall decor swings from its hook and slips down; everything else keels over and rattles.
            Vector3 axis = r.wall ? r.rotation * Vector3.forward : r.axis;
            Quaternion tilt = Quaternion.AngleAxis(amount * (r.wall ? 25f : MaxTilt) + wobble, axis);
            Vector3 sag = r.wall ? Vector3.down * 0.25f * amount : Vector3.zero;
            Vector3 rattle = new Vector3(Mathf.PerlinNoise(time * 9f, r.phase) - 0.5f, 0, Mathf.PerlinNoise(r.phase, time * 9f) - 0.5f) * 0.03f * amount;
            pair.Key.SetPositionAndRotation(r.position + sag + rattle, tilt * r.rotation);
        }
        foreach (var pair in lights)
        {
            if (pair.Key == null) continue;
            float flicker = Mathf.PerlinNoise(time * 12f, pair.Value) > 0.35f ? 1f : 0.15f;
            pair.Key.intensity = pair.Value * Mathf.Lerp(1f, flicker * 0.6f, level);
        }
    }

    private void Restore()
    {
        foreach (var pair in rest)
            if (pair.Key != null) pair.Key.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
        foreach (var pair in lights)
            if (pair.Key != null) pair.Key.intensity = pair.Value;
        rest.Clear();
        lights.Clear();
    }

    private void OnDestroy()
    {
        Restore();
        if (instance == this) instance = null;
    }

    private static void Toast(string message)
    {
        GameHUD hud = FindFirstObjectByType<GameHUD>();
        if (hud != null) hud.ShowToast(message);
    }
}
