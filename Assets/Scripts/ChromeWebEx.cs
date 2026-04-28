using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

[Serializable]
public class DistractionData
{
    public bool isDistracted;
    public string url;
    public string site;
}

/// <summary>
/// Receives POST requests from the companion Chrome extension on localhost.
/// The extension sends {"isDistracted":true/false,"url":"...","site":"..."} JSON.
/// Subscribe to OnDistractionDetected / OnFocusRestored to react in other systems.
/// </summary>
public class ChromeWebEx : MonoBehaviour
{
    [SerializeField] private int port = 8080;
    [SerializeField] private TMP_Text debugText;

    public event Action<DistractionData> OnDistractionDetected;
    public event Action OnFocusRestored;

    public bool IsDistracted { get; private set; }
    public int DistractionCount { get; private set; }

    // Total seconds spent on distracting sites this session
    public float TotalDistractionTime =>
        _accumulatedDistractionTime + (IsDistracted ? Time.time - _distractionStartTime : 0f);

    private HttpListener _listener;
    private Thread _listenerThread;
    private volatile bool _running;

    private readonly object _lock = new();
    private DistractionData _pendingData;
    private bool _hasPendingData;

    private float _distractionStartTime;
    private float _accumulatedDistractionTime;

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
            SetDebug("Listening for Chrome extension...");
        }
        catch (Exception ex)
        {
            SetDebug($"Server error: {ex.Message}");
            Debug.LogError($"ChromeWebEx failed to start: {ex.Message}");
        }
    }

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
        HttpListenerRequest req = ctx.Request;

        if (req.HttpMethod == "POST")
        {
            using var reader = new System.IO.StreamReader(req.InputStream, Encoding.UTF8);
            string body = reader.ReadToEnd();

            DistractionData data = JsonUtility.FromJson<DistractionData>(body);
            if (data != null)
            {
                lock (_lock)
                {
                    _pendingData = data;
                    _hasPendingData = true;
                }
            }
        }

        // CORS headers so the extension can reach localhost
        HttpListenerResponse res = ctx.Response;
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        byte[] buffer = Encoding.UTF8.GetBytes("OK");
        res.ContentLength64 = buffer.Length;
        res.OutputStream.Write(buffer, 0, buffer.Length);
        res.OutputStream.Close();
    }

    private void Update()
    {
        DistractionData data;
        lock (_lock)
        {
            if (!_hasPendingData) return;
            data = _pendingData;
            _hasPendingData = false;
        }

        if (data.isDistracted && !IsDistracted)
        {
            IsDistracted = true;
            DistractionCount++;
            _distractionStartTime = Time.time;
            SetDebug($"Distracted: {data.site ?? data.url}");
            OnDistractionDetected?.Invoke(data);
        }
        else if (!data.isDistracted && IsDistracted)
        {
            _accumulatedDistractionTime += Time.time - _distractionStartTime;
            IsDistracted = false;
            SetDebug("Back on track!");
            OnFocusRestored?.Invoke();
        }
    }

    private void SetDebug(string message)
    {
        if (debugText != null)
            debugText.text = message;
    }

    private void OnDestroy() => StopServer();
    private void OnApplicationQuit() => StopServer();

    private void StopServer()
    {
        _running = false;
        _listener?.Stop();
    }
}
