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
    }

    private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>(); // filled by the listener thread
    private float _lastFocusReward = -999f;
    private const float FocusCooldown = 60f;
    private HttpListener _httpListener;
    private Thread _listenerThread;
    private volatile string _status = "{}"; // written on the main thread, served by the listener thread
    private float _nextStatus;

    [Serializable]
    private class Status
    {
        public bool running, studying, strict, distracted;
        public int secondsLeft;
        public string mode;
    }

    public const string Address = "http://localhost:8080/";
    public bool IsListening => _httpListener != null && _httpListener.IsListening;
    public string ListenError { get; private set; } = "";
    public float LastExtensionMessageAt { get; private set; } = -1; // unscaled time; pings don't count
    public string LastResult { get; private set; } = "";

    // Debugging
    [SerializeField] private TMP_Text debugText;


    void Start() => StartListening();

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

                // Log the incoming request data
                if (request.HttpMethod == "POST")
                {
                    using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string requestBody = reader.ReadToEnd();
                        Debug.Log($"Received message: {requestBody}");

                        if (requestBody.Length <= 1024) _messages.Enqueue(requestBody); // ignore oversized junk
                    }
                }

                // Send a response back to the client
                HttpListenerResponse response = context.Response;
                response.AddHeader("Access-Control-Allow-Origin", "*");
                bool get = request.HttpMethod == "GET";
                if (get) response.ContentType = "application/json";
                string responseString = get ? _status : "Unity received your request!";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (HttpListenerException ex)
            {
               // Debug.LogWarning($"HttpListenerException: {ex.Message}");
            }
            catch (System.Exception ex)
            {
               // Debug.LogError($"Exception: {ex.Message}");
            }
        }
    }

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
            TimerManager timer = FindFirstObjectByType<TimerManager>();
            _status = JsonUtility.ToJson(new Status
            {
                running = timer != null && timer.IsTimerRunning,
                studying = timer == null || timer.IsStudySession,
                secondsLeft = timer != null ? Mathf.CeilToInt(timer.CurrentTime) : 0,
                mode = GameSettings.Modes[(int)GameSettings.Mode].name,
                strict = GameSettings.StrictFocus,
                distracted = Distraction.Reported,
            });
        }
    }

    private string Handle(string body)
    {
        Command command = Parse(body);
        if (command == null) return "Extension sent something unrecognised";
        if (command.action == "ping") return "Extension: connection works";
        LastExtensionMessageAt = Time.unscaledTime;
        TimerManager timer = FindFirstObjectByType<TimerManager>();
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
                Distraction.Report(command.site);
                return $"Extension: distracted by {Distraction.Site}";
            case "back":
                Distraction.Clear();
                return "Extension: back on task";
            case "focus":
                Distraction.Clear();
                if (Time.unscaledTime - _lastFocusReward < FocusCooldown) return "Extension: focus noted";
                _lastFocusReward = Time.unscaledTime;
                Experience experience = FindFirstObjectByType<Experience>();
                if (experience != null) experience.GainExperience(1);
                return "Extension: +1 XP for staying focused";
        }
        return $"Extension: can't {command.action} right now";
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