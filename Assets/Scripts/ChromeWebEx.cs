using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

// Local bridge for the study Chrome extension. The extension POSTs to http://localhost:8080/ with either a
// plain word or JSON {"action": "...", "minutes": 25}:
//   start | pause | reset   control the study timer
//   set                     set the study length in minutes (5-120), only while the timer is stopped
//   focus                   "I'm on a study site": +1 XP, at most once a minute (anything on this PC can
//                           reach localhost, so rewards are rate-limited rather than trusted)
//   distracted              "I'm on a distracting site", with {"site":"youtube.com"}: during a study session the
//                           room falls apart and coins drain. Re-send it at least every 45 s while the site
//                           stays open; it ends on "focus", "back" or when the reports stop.
//   back                    left the distracting site (no XP, unlike focus)
//   ping                    connection check, changes nothing
// A GET returns the game's state as JSON, so the extension can follow the timer and focus mode
// (e.g. block sites only during a strict study session):
//   {"running":true,"studying":true,"secondsLeft":1234,"mode":"Pomodoro","strict":false,"distracted":false}
public class ChromeWebEx : MonoBehaviour
{
    [Serializable]
    private class Command
    {
        public string action;
        public float minutes;
        public string site;
        public string tag;
    }

    // GET /stats: the whole study log (minutes and sessions per day and tag), for the extension's graphs page.
    [Serializable]
    private class History { public System.Collections.Generic.List<StudyLog.Entry> entries; }

    private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>(); // filled by the listener thread
    private float _lastFocusReward = -999f;
    private const float FocusCooldown = 60f;
    private HttpListener _httpListener;
    private Thread _listenerThread;
    private volatile string _status = "{}"; // written on the main thread, served by the listener thread
    private volatile string _history = "{\"entries\":[]}";
    private float _nextStatus, _nextStats;
    private readonly Status _state = new Status();

    [Serializable]
    private class Status
    {
        public bool running, studying, strict, distracted;
        public int secondsLeft, phaseSeconds;
        public string mode;
        // Progress for the extension popup, refreshed every few seconds.
        public int level, xp, xpNext, todayMinutes, todaySessions, distractionsToday, streak;
        public string topSubject = ""; // most studied subject this week
        public string tag = "";         // what the current sessions are logged under (StudyLog.Subject)
        public string[] tags = new string[0]; // tags used before, most studied first
        public int[] week = new int[7]; // minutes studied per day, oldest first, today last
        public Theme theme = new Theme();
    }

    // The menu palette as hex, so the extension popup matches the game (and follows Paint > Menus).
    [Serializable]
    private class Theme
    {
        public bool light;
        public string panel, card, selected, tab, muted, button, text, accent, xp, danger;
    }

    public const string Address = "http://localhost:8080/";
    // Chrome Web Store page for the extension; Settings > Browser extension opens it. Paste the item's URL here once it's listed.
    public const string StoreUrl = "";
    public bool IsListening => _httpListener != null && _httpListener.IsListening;
    public string ListenError { get; private set; } = "";
    public float LastExtensionMessageAt { get; private set; } = -1; // unscaled time; pings don't count
    public string LastResult { get; private set; } = "";

    // Debugging
    [SerializeField] private TMP_Text debugText;


    void Start() { if (GameSettings.Desktop) StartListening(); } // a web page or phone app can't take the extension's messages

    // Settings > Browser extension "Reconnect": e.g. after closing whatever held the port.
    public void Restart()
    {
        StopListening();
        StartListening();
    }

    private void StartListening()
    {
        ListenError = "";
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add(Address);

        try
        {
            _httpListener.Start();
        }
        catch (Exception ex) when (ex is HttpListenerException || ex is System.Net.Sockets.SocketException) // Mono throws SocketException when the port is taken
        {
            // Port in use or not permitted: run without the extension instead of breaking Start.
            Debug.LogWarning($"Chrome extension listener disabled: {ex.Message}");
            ListenError = ex.Message;
            _httpListener = null;
            if (debugText) debugText.text = "";
            return;
        }
        if (debugText) debugText.text = "Connected to Chrome Extension!";
        Debug.Log("HTTP Server started on http://localhost:8080/");
        // Start the listener thread
        HttpListener listener = _httpListener; // the field can be swapped by Restart while this thread runs
        _listenerThread = new Thread(() => HandleRequests(listener)) { IsBackground = true };
        _listenerThread.Start();
    }

    private void HandleRequests(HttpListener listener)
    {
        while (listener.IsListening)
        {
            try
            {
                // Wait for an incoming request
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                // Any web page can reach localhost too, so only the extension (or a local tool, which sends no
                // Origin) is served. The Host check stops DNS-rebinding pages that pose as localhost.
                string origin = request.Headers["Origin"];
                string host = request.Headers["Host"] ?? "";
                if (!AllowedOrigin(origin) || !(host.StartsWith("localhost:") || host.StartsWith("127.0.0.1:")))
                {
                    response.StatusCode = 403;
                    response.Close();
                    continue;
                }

                // Log the incoming request data
                if (request.HttpMethod == "POST")
                {
                    using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string requestBody = reader.ReadToEnd();

                        if (requestBody.Length <= 1024) _messages.Enqueue(requestBody); // ignore oversized junk
                    }
                }

                // Send a response back to the client
                if (origin != null) response.AddHeader("Access-Control-Allow-Origin", origin);
                bool get = request.HttpMethod == "GET";
                if (get) response.ContentType = "application/json";
                string responseString = !get ? "Unity received your request!" : request.Url.AbsolutePath == "/stats" ? _history : _status;
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception) { } // closed listener or a dropped connection: the loop condition decides whether to go on
        }
    }

    private static bool AllowedOrigin(string origin) =>
        origin == null || origin.StartsWith("chrome-extension://") || origin.StartsWith("moz-extension://");

    void Update()
    {
        // Unity objects can only be touched on the main thread, so the listener queues and we act here.
        while (_messages.TryDequeue(out string body))
        {
            string result = Handle(body);
            LastResult = result;
            if (debugText) debugText.text = result;
        }
        if (Time.unscaledTime >= _nextStatus)
        {
            _nextStatus = Time.unscaledTime + 0.5f;
            TimerManager timer = FindAnyObjectByType<TimerManager>();
            _state.running = timer != null && timer.IsTimerRunning;
            _state.studying = timer == null || timer.IsStudySession;
            _state.secondsLeft = timer != null ? Mathf.CeilToInt(timer.CurrentTime) : 0;
            _state.phaseSeconds = timer == null ? 0 : Mathf.RoundToInt((timer.IsStudySession ? timer.StudyMinutes : timer.BreakMinutes) * 60f);
            _state.mode = GameSettings.Modes[(int)GameSettings.Mode].name;
            _state.strict = GameSettings.StrictFocus;
            _state.distracted = Distraction.Reported;
            if (Time.unscaledTime >= _nextStats) // the study log is parsed from PlayerPrefs, so not every half second
            {
                _nextStats = Time.unscaledTime + 5f;
                UpdateStats();
            }
            _status = JsonUtility.ToJson(_state);
        }
    }

    private void UpdateStats()
    {
        Experience experience = FindAnyObjectByType<Experience>();
        _state.level = experience != null ? experience.GetPlayerLevel() : 1;
        _state.xp = experience != null ? experience.GetExperience() : 0;
        _state.xpNext = experience != null ? experience.ExperienceToNextLevel : 100;
        DateTime today = DateTime.Now.Date;
        for (int i = 0; i < 7; i++) _state.week[i] = 0;
        _state.todaySessions = 0;
        var subjects = new System.Collections.Generic.Dictionary<string, int>();
        var entries = StudyLog.Entries;
        _history = JsonUtility.ToJson(new History { entries = entries });
        _state.tag = StudyLog.Subject;
        var allTags = StudyLog.BySubject();
        _state.tags = new string[Mathf.Min(allTags.Count, 12)];
        for (int i = 0; i < _state.tags.Length; i++) _state.tags[i] = allTags[i].Key;
        foreach (StudyLog.Entry e in entries)
        {
            if (!DateTime.TryParseExact(e.day, StudyHabit.Day, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime day)) continue;
            int ago = (today - day).Days;
            if (ago < 0 || ago >= 7) continue;
            _state.week[6 - ago] += e.minutes;
            if (ago == 0) _state.todaySessions += e.sessions;
            subjects[e.subject] = (subjects.TryGetValue(e.subject, out int m) ? m : 0) + e.minutes;
        }
        _state.todayMinutes = _state.week[6];
        _state.topSubject = "";
        int best = 0;
        foreach (var pair in subjects) if (pair.Value > best) { best = pair.Value; _state.topSubject = pair.Key; }
        _state.distractionsToday = PlayerPrefs.GetString(DistractionDayKey, "") == StudyHabit.Today ? PlayerPrefs.GetInt(DistractionCountKey, 0) : 0;
        _state.streak = StudyLog.DayStreak();

        Theme t = _state.theme;
        string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        t.light = UIKit.LightTheme;
        t.panel = Hex(UIKit.PanelColor); t.card = Hex(UIKit.CardColor); t.selected = Hex(UIKit.SelectedColor);
        t.tab = Hex(UIKit.TabColor); t.muted = Hex(UIKit.MutedText); t.button = Hex(UIKit.AccentButtonColor);
        t.text = Hex(UIKit.TextColor); t.accent = Hex(UIKit.AccentColor); t.xp = Hex(UIKit.XpColor); t.danger = Hex(UIKit.DangerColor);
    }

    private string Handle(string body)
    {
        Command command = Parse(body);
        if (command == null) return "Extension sent something unrecognised";
        if (command.action == "ping") return "Extension: connection works";
        LastExtensionMessageAt = Time.unscaledTime;
        TimerManager timer = FindAnyObjectByType<TimerManager>();
        switch (command.action)
        {
            case "start":
                if (timer == null) break;
                timer.StartTimer();
                return "Extension: timer started";
            case "pause":
                if (timer == null) break;
                timer.PauseTimer();
                return "Extension: timer paused";
            case "reset":
                if (timer == null) break;
                timer.ResetTimer();
                return "Extension: timer reset";
            case "set":
                if (timer == null || timer.IsTimerRunning || command.minutes <= 0) break;
                timer.SetCustomDuration(command.minutes);
                GameSettings.Mode = GameSettings.FocusMode.Custom; // no longer one of the presets
                return $"Extension: sessions are now {timer.StudyMinutes:0} minutes";
            case "distracted":
                // a new visit during a study session, not the 30 s re-report or a break
                if (!Distraction.Reported && timer != null && timer.IsTimerRunning && timer.IsStudySession) CountDistraction();
                Distraction.Report(command.site);
                return $"Extension: distracted by {Distraction.Site}";
            case "tag":
                // shown in the HUD and planner, so drop rich-text tags; the Subject setter trims and caps the length
                StudyLog.Subject = (command.tag ?? "").Replace("<", "").Replace(">", "");
                _nextStats = 0; // send the new tag on the next status
                return $"Extension: now studying {StudyLog.Subject}";
            case "back":
                Distraction.Clear();
                return "Extension: back on task";
            case "focus":
                Distraction.Clear();
                if (Time.unscaledTime - _lastFocusReward < FocusCooldown) return "Extension: focus noted";
                _lastFocusReward = Time.unscaledTime;
                Experience experience = FindAnyObjectByType<Experience>();
                if (experience != null) experience.GainExperience(1);
                return "Extension: +1 XP for staying focused";
        }
        return $"Extension: can't {command.action} right now";
    }

    private const string DistractionDayKey = "ExtDistractionDay", DistractionCountKey = "ExtDistractions";

    private static void CountDistraction()
    {
        bool sameDay = PlayerPrefs.GetString(DistractionDayKey, "") == StudyHabit.Today;
        PlayerPrefs.SetString(DistractionDayKey, StudyHabit.Today);
        PlayerPrefs.SetInt(DistractionCountKey, sameDay ? PlayerPrefs.GetInt(DistractionCountKey, 0) + 1 : 1);
    }

    private static Command Parse(string body)
    {
        body = body.Trim();
        if (body.StartsWith("{"))
        {
            try
            {
                Command json = JsonUtility.FromJson<Command>(body);
                if (json != null && !string.IsNullOrEmpty(json.action))
                {
                    json.action = json.action.Trim().ToLowerInvariant();
                    return json;
                }
            }
            catch (ArgumentException) { } // not valid JSON
            return null;
        }
        return body.Length > 0 && body.Length <= 32 ? new Command { action = body.ToLowerInvariant() } : null;
    }

    // OnDestroy also covers scene reloads (loading a save), which would otherwise leave port 8080 held
    void OnDestroy() => StopListening();

    private void StopListening()
    {
        if (_httpListener == null) return;
        _httpListener.Close(); // unblocks GetContext, so the background thread ends on its own
        _httpListener = null;
        Debug.Log("HTTP Server stopped.");
    }
}