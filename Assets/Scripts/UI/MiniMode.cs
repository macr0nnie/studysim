using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static UIKit;

// Mini mode: shrinks the game to a small borderless, always-on-top window that only displays the room
// (with a see-through background on Windows builds). No HUD and no room interaction: drag anywhere to move
// the window (the position is remembered); the Mini mode key, a double-click or a right-click goes back.
// The timer, character and browser extension keep running underneath.
// The window calls only run in a Windows build; in the Editor only the UI and input swap happens.
public class MiniMode : MonoBehaviour
{
    const int Width = 360, Height = 300;
    const float DoubleClick = 0.3f;
    const string PosX = "MiniTopX", PosY = "MiniTopY";

    // Fallback if the per-pixel transparency shows black or a solid box: Settings > Focus > "See-through: alpha / color key".
    public static bool ColorKey { get => PlayerPrefs.GetInt("MiniColorKey", 0) == 1; set => PlayerPrefs.SetInt("MiniColorKey", value ? 1 : 0); }

    public static bool Active { get; private set; }
    static MiniMode instance;

    private readonly List<Canvas> hidden = new List<Canvas>();
    private RoomManager room;
    private float lastClick = -1;
    // Full-mode settings, restored on the way back.
    private int frameRate, vSync;
    private bool shadows;
    private AntialiasingMode antialiasing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureMiniMode();
        EnsureMiniMode();
    }

    private static void EnsureMiniMode()
    {
        if (FindFirstObjectByType<RoomManager>() != null && instance == null)
            instance = new GameObject("MiniMode").AddComponent<MiniMode>();
    }

    public static void Set(bool on)
    {
        if (instance != null && on != Active) instance.Toggle();
    }

    private void Update()
    {
        if (Controls.Pressed(Controls.Act.MiniMode) && !Typing()) { Toggle(); return; }
        if (!Active) return;

        if (Controls.RightClickDown) { Toggle(); return; }
        if (Controls.ClickDown)
        {
            if (Time.unscaledTime - lastClick <= DoubleClick) { lastClick = -1; Toggle(); return; }
            lastClick = Time.unscaledTime;
        }
        Drag();
    }

    private void OnApplicationQuit()
    {
        if (Active) SavePosition();
    }

    public void Toggle()
    {
        Active = !Active;
        // Keys other than the Mini mode key would open hidden panels, so they're off while mini.
        Controls.MiniOnly(Active);

        // Display only: the room ignores clicks (a right-click would otherwise delete furniture).
        if (room == null) room = FindFirstObjectByType<RoomManager>();
        if (room != null)
        {
            if (Active && room.IsEditMode) room.ToggleEditMode();
            room.enabled = !Active;
        }

        if (Active)
        {
            hidden.Clear();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.enabled) { Show(c, false); hidden.Add(c); }
        }
        else
        {
            foreach (Canvas c in hidden) if (c != null) Show(c, true);
            hidden.Clear();
        }
        Economy(Active);

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (Active) StartCoroutine(EnterWindow()); else ExitWindow();
#endif
    }

    private static void Show(Canvas c, bool on)
    {
        c.enabled = on;
        if (c.TryGetComponent(out UnityEngine.UI.GraphicRaycaster raycaster)) raycaster.enabled = on;
    }

    // Low power while mini: 15 fps, no vsync, no shadows or anti-aliasing (post-processing is off for transparency).
    private void Economy(bool on)
    {
        if (on) { frameRate = Application.targetFrameRate; vSync = QualitySettings.vSyncCount; }
        Application.targetFrameRate = on ? 15 : frameRate;
        QualitySettings.vSyncCount = on ? 0 : vSync;

        Camera cam = Camera.main;
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (on) { shadows = data.renderShadows; antialiasing = data.antialiasing; }
        data.renderShadows = !on && shadows;
        data.antialiasing = on ? AntialiasingMode.None : antialiasing;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private IntPtr hwnd;
    private int fullStyle;
    // What full mode looked like, restored on the way back.
    private int fullWidth, fullHeight;
    private FullScreenMode fullMode;
    private CameraClearFlags clearFlags;
    private Color background;
    private bool hdr, post;
    private bool placed, dragging;
    private POINT grabOffset;

    private System.Collections.IEnumerator EnterWindow()
    {
        hwnd = GetActiveWindow();
        fullWidth = Screen.width; fullHeight = Screen.height; fullMode = Screen.fullScreenMode;
        fullStyle = GetWindowLong(hwnd, GWL_STYLE);

        // No background at all, two ways. Per-pixel alpha (default): the camera clears to alpha 0 and DWM blends
        // the window over the desktop; needs the flip-model swapchain off, no HDR/post-processing, and the URP
        // asset's alpha output on. Colour key (fallback): the camera clears to pure green and Windows makes that
        // colour transparent and click-through. Pure 0/1 channels survive the sRGB round trip exactly.
        Camera cam = Camera.main;
        if (cam != null)
        {
            clearFlags = cam.clearFlags; background = cam.backgroundColor; hdr = cam.allowHDR;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColorKey ? new Color(0, 1, 0, 1) : new Color(0, 0, 0, 0);
            cam.allowHDR = false;
            var data = cam.GetUniversalAdditionalCameraData();
            post = data.renderPostProcessing;
            data.renderPostProcessing = false;
        }

        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
        yield return null; // Unity applies the resolution at the end of the frame and resets the window style
        if (!Active) yield break;
        // Top right corner unless it was dragged somewhere else before.
        int x = PlayerPrefs.GetInt(PosX, Display.main.systemWidth - Width - 24);
        int y = PlayerPrefs.GetInt(PosY, 24);
        SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, Width, Height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        if (ColorKey)
        {
            SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_LAYERED);
            SetLayeredWindowAttributes(hwnd, KeyColorRef, 0, LWA_COLORKEY);
        }
        else
        {
            var margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        placed = true;
    }

    private void ExitWindow()
    {
        SavePosition();
        placed = dragging = false;
        var margins = new MARGINS();
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        SetWindowLong(hwnd, GWL_EXSTYLE, GetWindowLong(hwnd, GWL_EXSTYLE) & ~WS_EX_LAYERED);
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = clearFlags; cam.backgroundColor = background; cam.allowHDR = hdr;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = post;
        }
        SetWindowLong(hwnd, GWL_STYLE, fullStyle);
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        Screen.SetResolution(fullWidth, fullHeight, fullMode);
    }

    private void SavePosition()
    {
        if (!placed || !GetWindowRect(hwnd, out RECT r)) return;
        PlayerPrefs.SetInt(PosX, r.left);
        PlayerPrefs.SetInt(PosY, r.top);
        PlayerPrefs.Save();
    }

    // Click and drag moves the window. With the colour key, clicks on the see-through parts go to the apps behind,
    // so only room pixels start a drag; with per-pixel alpha the whole 360x300 rectangle takes the click. Cursor positions are in screen space,
    // so they stay valid while the window moves under the mouse.
    private void Drag()
    {
        if (Controls.ClickDown && GetCursorPos(out POINT p) && GetWindowRect(hwnd, out RECT r))
        {
            dragging = true;
            grabOffset = new POINT { x = p.x - r.left, y = p.y - r.top };
        }
        if (!Controls.ClickHeld) dragging = false;
        if (dragging && GetCursorPos(out POINT now))
            SetWindowPos(hwnd, HWND_TOPMOST, now.x - grabOffset.x, now.y - grabOffset.y, 0, 0, SWP_NOSIZE);
    }

    const int GWL_STYLE = -16, GWL_EXSTYLE = -20, WS_EX_LAYERED = 0x80000;
    const uint LWA_COLORKEY = 0x1, KeyColorRef = 0x0000FF00; // COLORREF 0x00BBGGRR: pure green
    const int WS_POPUP = unchecked((int)0x80000000), WS_VISIBLE = 0x10000000;
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);

    struct MARGINS { public int left, right, top, bottom; }
    struct RECT { public int left, top, right, bottom; }
    struct POINT { public int x, y; }

    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
#else
    private void SavePosition() { }
    private void Drag() { }
#endif
}
