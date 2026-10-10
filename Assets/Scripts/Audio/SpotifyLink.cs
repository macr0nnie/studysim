using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Reads what the Spotify desktop app is playing from its window title ("Artist - Song" while playing,
// "Spotify Premium"/"Spotify Free" while paused) and controls it with media keys. No account or API key.
// shortcut: no album art, progress or reliable paused-track info; upgrade to the Spotify Web API if those are wanted.
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
#else
    public static void Poll() { }
    public static void PlayPause() { }
    public static void Next() { }
    public static void Previous() { }
#endif
}
