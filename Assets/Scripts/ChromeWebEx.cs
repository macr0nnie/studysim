using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

public class ChromeWebEx : MonoBehaviour
{
   private HttpListener _httpListener;
    private Thread _listenerThread;

    void Start()
    {
        // Initialize the HttpListener and listen on localhost:8080
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add("http://localhost:8080/");
        _httpListener.Start();

        Debug.Log("HTTP Server started on http://localhost:8080/");

        // Start the listener thread
        _listenerThread = new Thread(HandleRequests);
        _listenerThread.Start();
    }

    private void HandleRequests()
    {
        while (_httpListener.IsListening)
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
