using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

// ── Payloads ──────────────────────────────────────────────────────────────────

/// <summary>Sent on /focus when the browser window gains or loses focus.</summary>
[Serializable]
public class BrowserFocusData
{
    public bool focused;
    public long timestamp;
}

/// <summary>
/// Sent on /session for every timer lifecycle event.
/// event values: "start" | "pause" | "resume" | "stop" | "complete"
/// </summary>
[Serializable]
public class SessionData
{
    public string @event;    // start | pause | resume | stop | complete
    public string mode;      // focus | deepWork | shortBreak | longBreak
    public int    minutes;   // populated on start + complete
    public int    xpGain;    // populated on complete
    public int    coinGain;  // populated on complete
    public int    streak;    // populated on complete
    public bool   leveledUp; // populated on complete
    public long   timestamp;
}

/// <summary>Cumulative stats — read by the Unity stats/UI screen.</summary>
[Serializable]
public class ProductivityStats
{
    public int totalSessions;
    public int totalFocusMinutes;
    public int totalDeepWorkMinutes;
    public int deepWorkSessions;
    public int streak;
    public int level;
    public int coins;
}

// ── Main component ────────────────────────────────────────────────────────────

/// <summary>
/// Receives POST messages from the Cozy Focus browser extension (optional Unity integration).
///
/// Endpoints — all POST to http://localhost:{port}/...
///   /focus    BrowserFocusData  — browser window focused / unfocused
///   /session  SessionData       — timer event (start, pause, resume, stop, complete)
///   /ping     (no body)         — connection test from the options page
/// </summary>
public class ChromeWebEx : MonoBehaviour
{
    [SerializeField] private int      port      = 8080;
    [SerializeField] private TMP_Text debugText;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<BrowserFocusData> OnBrowserFocusChanged;
    public event Action<SessionData>      OnSessionEvent;

    // ── Properties ────────────────────────────────────────────────────────────
    public bool             IsBrowserFocused { get; private set; } = true;
    public bool             IsDeepWorkActive { get; private set; }
    public bool             IsTimerRunning   { get; private set; }
    public string           CurrentMode      { get; private set; } = "focus";
    public ProductivityStats Stats           { get; private set; } = new();
    public IReadOnlyList<SessionData> SessionHistory => _sessionHistory;

    // ── Internals ─────────────────────────────────────────────────────────────
    private HttpListener  _listener;
    private Thread        _listenerThread;
    private volatile bool _running;

    private readonly object                          _lock    = new();
    private readonly Queue<(string path, string body)> _pending = new();
    private readonly List<SessionData>               _sessionHistory = new();
    private const int MaxHistory = 50;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            _running = true;
            _listenerThread = new Thread(ListenLoop) { IsBackground = true };
            _listenerThread.Start();
            SetDebug("Listening for browser extension...");
        }
        catch (Exception ex)
        {
            SetDebug($"Server error: {ex.Message}");
            Debug.LogError($"ChromeWebEx failed to start: {ex.Message}");
        }
    }

    private void OnDestroy()         => StopServer();
    private void OnApplicationQuit() => StopServer();

    private void StopServer()
    {
        _running = false;
        _listener?.Stop();
    }

    // ── Background thread ─────────────────────────────────────────────────────

    private void ListenLoop()
    {
        while (_running && _listener.IsListening)
        {
            try
            {
                HttpListenerContext ctx = _listener.GetContext();
                HandleRequest(ctx);
            }
            catch (HttpListenerException) { }
            catch (Exception ex) when (_running)
            {
                Debug.LogWarning($"ChromeWebEx: {ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        string path = ctx.Request.Url.AbsolutePath.ToLowerInvariant().TrimEnd('/');
        if (string.IsNullOrEmpty(path)) path = "/ping";

        string body = "";
        if (ctx.Request.HttpMethod == "POST")
        {
            using var reader = new System.IO.StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            body = reader.ReadToEnd();
        }

        if (!string.IsNullOrWhiteSpace(body))
            lock (_lock) _pending.Enqueue((path, body));

        var res = ctx.Response;
        res.Headers.Add("Access-Control-Allow-Origin",  "*");
        res.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        byte[] buf = Encoding.UTF8.GetBytes("OK");
        res.ContentLength64 = buf.Length;
        res.OutputStream.Write(buf, 0, buf.Length);
        res.OutputStream.Close();
    }

    // ── Main-thread dispatch ──────────────────────────────────────────────────

    private void Update()
    {
        while (true)
        {
            (string path, string body) msg;
            lock (_lock)
            {
                if (_pending.Count == 0) break;
                msg = _pending.Dequeue();
            }

            switch (msg.path)
            {
                case "/focus":   HandleFocus(msg.body);   break;
                case "/session": HandleSession(msg.body); break;
                case "/ping":                             break; // connection test — no-op
                default:
                    Debug.LogWarning($"ChromeWebEx: unknown path '{msg.path}'");
                    break;
            }
        }
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void HandleFocus(string body)
    {
        BrowserFocusData data = JsonUtility.FromJson<BrowserFocusData>(body);
        if (data == null) return;

        IsBrowserFocused = data.focused;
        SetDebug(data.focused ? "Browser focused" : "Browser unfocused");
        OnBrowserFocusChanged?.Invoke(data);
    }

    private void HandleSession(string body)
    {
        SessionData data = JsonUtility.FromJson<SessionData>(body);
        if (data == null) return;

        // Update live state
        switch (data.@event)
        {
            case "start":
                IsTimerRunning   = true;
                CurrentMode      = data.mode;
                IsDeepWorkActive = data.mode == "deepWork";
                SetDebug($"Timer started: {data.mode} {data.minutes}min");
                break;

            case "pause":
                SetDebug($"Timer paused ({data.mode})");
                break;

            case "resume":
                SetDebug($"Timer resumed ({data.mode})");
                break;

            case "stop":
                IsTimerRunning   = false;
                IsDeepWorkActive = false;
                SetDebug("Timer stopped");
                break;

            case "complete":
                IsTimerRunning   = false;
                IsDeepWorkActive = false;
                UpdateStats(data);
                SetDebug($"Session complete: {data.mode} {data.minutes}min +{data.xpGain}xp");
                break;
        }

        _sessionHistory.Insert(0, data);
        if (_sessionHistory.Count > MaxHistory)
            _sessionHistory.RemoveAt(_sessionHistory.Count - 1);

        OnSessionEvent?.Invoke(data);
    }

    private void UpdateStats(SessionData data)
    {
        bool isFocus = data.mode == "focus" || data.mode == "deepWork";
        if (!isFocus) return;

        Stats.totalSessions++;
        Stats.totalFocusMinutes += data.minutes;

        if (data.mode == "deepWork")
        {
            Stats.deepWorkSessions++;
            Stats.totalDeepWorkMinutes += data.minutes;
        }

        if (data.streak > Stats.streak) Stats.streak = data.streak;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetDebug(string message)
    {
        if (debugText != null) debugText.text = message;
        Debug.Log($"[ChromeWebEx] {message}");
    }
}
