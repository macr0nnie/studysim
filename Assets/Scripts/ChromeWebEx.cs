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
public class ChromeWebEx : MonoBehaviour
{
    [Serializable]
    private class Command
    {
        public string action;
        public float minutes;
    }

    private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>(); // filled by the listener thread
    private float _lastFocusReward = -999f;
    private const float FocusCooldown = 60f;
    private HttpListener _httpListener;
    private Thread _listenerThread;

    // Debugging
    [SerializeField] private TMP_Text debugText;


    void Start()
    {
        // Initialize the HttpListener and listen on localhost:8080
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add("http://localhost:8080/");

        try
        {
            _httpListener.Start();
        }
        catch (HttpListenerException ex)
        {
            // Port in use or not permitted: run without the extension instead of breaking Start.
            Debug.LogWarning($"Chrome extension listener disabled: {ex.Message}");
            _httpListener = null;
            return;
        }
        if (debugText) debugText.text = "Connected to Chrome Extension!";
        Debug.Log("HTTP Server started on http://localhost:8080/");
        // Start the listener thread
        _listenerThread = new Thread(HandleRequests);
        _listenerThread.Start();
    }

    private void HandleRequests()
    {
        while (_httpListener.IsListening)
        {
            try
            {
                // Wait for an incoming request
                HttpListenerContext context = _httpListener.GetContext();
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
                string responseString = "Unity received your request!";
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
            if (debugText) debugText.text = result;
        }
    }

    private string Handle(string body)
    {
        Command command = Parse(body);
        if (command == null) return "Extension sent something unrecognised";
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
                return $"Extension: sessions are now {timer.StudyMinutes:0} minutes";
            case "focus":
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

    void OnApplicationQuit()
    {
        // Stop the listener when the application quits
        if (_httpListener != null)
        {
            _httpListener.Stop();
            _listenerThread.Abort();
            Debug.Log("HTTP Server stopped.");
        }
    }
}