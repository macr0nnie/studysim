using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Reads what the Spotify desktop app is playing and controls it. No account or API key. Windows: from its window
// title ("Artist - Song" while playing, "Spotify Premium"/"Spotify Free" while paused) and media keys. Mac: AppleScript, for
// Spotify or Apple Music.
// shortcut: no progress bar; upgrade to the Spotify Web API if that is wanted. (Covers come from MusicPlayerUI.)
public static class SpotifyLink
{
    public static bool Running { get; private set; }
    public static bool Playing { get; private set; }
    public static string Title { get; private set; } = "";
    public static string Artist { get; private set; } = "";

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    const float PollSeconds = 1f, ScanSeconds = 5f;
    static float nextPoll, nextScan;
    static IntPtr window;
    static readonly StringBuilder buffer = new StringBuilder(512);

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    public static string Source => "Spotify";

    // Call every frame; it only does work about once a second.
    public static void Poll()
    {
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + PollSeconds;

        string text = window != IntPtr.Zero && IsWindow(window) && GetWindowText(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : null;
        if (text == null)
        {
            window = IntPtr.Zero;
            Running = Playing = false;
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanSeconds;
            window = FindSpotifyWindow();
            if (window == IntPtr.Zero) return;
            text = GetWindowText(window, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
        }
        Running = true;
        int dash = text.IndexOf(" - ", StringComparison.Ordinal);
        Playing = dash > 0;
        // Paused shows only "Spotify ...", so keep the last track on screen.
        if (Playing) { Artist = text.Substring(0, dash); Title = text.Substring(dash + 3); }
    }

    // Spotify owns several windows; the main one is the one with a title.
    static IntPtr FindSpotifyWindow()
    {
        Process[] processes = Process.GetProcessesByName("Spotify");
        if (processes.Length == 0) return IntPtr.Zero;
        pids.Clear();
        foreach (Process p in processes) { pids.Add((uint)p.Id); p.Dispose(); }
        found = IntPtr.Zero;
        EnumWindows(CheckWindow, IntPtr.Zero);
        return found;
    }

    static readonly System.Collections.Generic.HashSet<uint> pids = new System.Collections.Generic.HashSet<uint>();
    static IntPtr found;
    static readonly EnumWindowsProc CheckWindow = OnWindow; // kept in a field so it isn't collected mid-call

    // Static, no captures: IL2CPP builds can only call back into static methods marked like this.
    [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
    static bool OnWindow(IntPtr hWnd, IntPtr _)
    {
        GetWindowThreadProcessId(hWnd, out uint pid);
        if (!pids.Contains(pid) || GetWindowText(hWnd, buffer, buffer.Capacity) == 0) return true;
        found = hWnd;
        return false;
    }

    // Media keys go to whichever app Windows treats as the current media session, normally Spotify.
    static void Key(byte vk)
    {
        keybd_event(vk, 0, 1, UIntPtr.Zero);     // KEYEVENTF_EXTENDEDKEY
        keybd_event(vk, 0, 1 | 2, UIntPtr.Zero); // + KEYEVENTF_KEYUP
        nextPoll = Time.unscaledTime + 0.3f;     // pick up the new title soon after
    }

    public static void PlayPause() => Key(0xB3);
    public static void Next() => Key(0xB0);
    public static void Previous() => Key(0xB1);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    // Mac: asks the Spotify app through AppleScript (osascript). The first time, macOS asks the player to let
    // lofilife control Spotify or Music (the reason text is added to Info.plist by ShipSetupTools.BuildMac).
    // "is running" doesn't launch the app, so nothing opens when the player doesn't use it.
    const float PollSeconds = 1f;
    static float nextPoll;
    static volatile bool busy;
    static volatile string app = "Spotify"; // AppleScript name of the app being shown: "Spotify" or "Music"
    public static string Source => app == "Music" ? "Apple Music" : app;

    // Spotify, or Apple Music when Spotify isn't playing. Output: app, state, artist, song (one per line).
    const string ReadScript =
        "set out to \"\"\n" +
        "if application \"Spotify\" is running then\n" +
        " tell application \"Spotify\"\n" +
        "  try\n" +
        "   set out to \"Spotify\" & linefeed & (player state as text) & linefeed & (artist of current track) & linefeed & (name of current track)\n" +
        "  on error\n" +
        "   set out to \"Spotify\" & linefeed & (player state as text)\n" +
        "  end try\n" +
        " end tell\n" +
        "end if\n" +
        "if out does not contain (linefeed & \"playing\") and application \"Music\" is running then\n" +
        " tell application \"Music\"\n" +
        "  try\n" +
        "   if out is \"\" or player state is playing then set out to \"Music\" & linefeed & (player state as text) & linefeed & (artist of current track) & linefeed & (name of current track)\n" +
        "  end try\n" +
        " end tell\n" +
        "end if\n" +
        "return out";

    // Call every frame; it only does work about once a second, and osascript runs off the main thread.
    public static void Poll()
    {
        if (busy || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + PollSeconds;
        busy = true;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                string[] lines = Run(ReadScript).Split('\n');
                Running = lines.Length >= 2;
                if (!Running) { Playing = false; return; }
                if (lines[0] != app) { app = lines[0]; Artist = Title = ""; } // switched apps: don't show the other one's song
                Playing = lines[1] == "playing";
                if (lines.Length >= 4) { Artist = lines[2]; Title = lines[3]; }
            }
            finally { busy = false; }
        });
    }

    static void Tell(string command)
    {
        nextPoll = Time.unscaledTime + 0.3f; // pick up the new track soon after
        string target = app;
        System.Threading.Tasks.Task.Run(() => Run($"if application \"{target}\" is running then tell application \"{target}\" to {command}"));
    }

    // The script goes in on stdin, so nothing needs quoting for the command line.
    static string Run(string script)
    {
        try
        {
            using (var p = Process.Start(new ProcessStartInfo("/usr/bin/osascript")
                   { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true }))
            {
                p.StandardInput.Write(script);
                p.StandardInput.Close();
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(2000);
                return p.ExitCode == 0 ? output : ""; // e.g. the player said no to controlling Spotify
            }
        }
        catch (Exception) { return ""; }
    }

    public static void PlayPause() => Tell("playpause");
    public static void Next() => Tell("next track");
    public static void Previous() => Tell("previous track");
#else
    public static string Source => "Spotify";
    public static void Poll() { }
    public static void PlayPause() { }
    public static void Next() { }
    public static void Previous() { }
#endif
}
