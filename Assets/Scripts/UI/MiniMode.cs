using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static UIKit;

// Mini mode: shrinks the game to a small game-overlay window: borderless, always on top, see-through, no taskbar
// entry, never takes focus, and click-through so it doesn't get in the way of what's underneath. It only displays
// the room (no HUD, no room interaction). F10 unlocks it for dragging (a frame shows) and locks it again; the
// position is remembered. F9 leaves mini mode. In a Windows build both keys are read globally (even while another
// app has focus), so they're fixed to F9 and F10 there. While unlocked, a double-click or right-click also leaves.
// The timer, character and browser extension keep running underneath.
// The window calls only run in a Windows build; in the Editor only the UI and input swap happens.
public class MiniMode : MonoBehaviour
{
    const int Width = 360, Height = 300;
    const float DoubleClick = 0.3f;
    const string PosX = "MiniTopX", PosY = "MiniTopY";

    // Fallback if the per-pixel transparency shows black or a solid box: Settings > Focus > "See-through: alpha / color key".
    public static bool ColorKey { get => PlayerPrefs.GetInt("MiniColorKey", 0) == 1; set => PlayerPrefs.SetInt("MiniColorKey", value ? 1 : 0); }

    public static float Opacity { get => PlayerPrefs.GetFloat("MiniOpacity", 1f); set => PlayerPrefs.SetFloat("MiniOpacity", value); }

    public static bool Active { get; private set; }
    static MiniMode instance;

    private readonly List<Canvas> hidden = new List<Canvas>();
    private RoomManager room;
    private float lastClick = -1, blockUntil;
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
        // Leaving mini mode re-enables the key action, which can report the same key press once more.
        if (!Active && Time.unscaledTime > blockUntil && Controls.Pressed(Controls.Act.MiniMode) && !Typing()) Toggle();
        else if (Active) Overlay();
    }

    // Wants to leave mini mode: the (editor-only) key, or a double-click / right-click while unlocked.
    private bool LeaveClick(bool down, bool rightDown)
    {
        if (rightDown) return true;
        if (!down) return false;
        bool second = Time.unscaledTime - lastClick <= DoubleClick;
        lastClick = second ? -1 : Time.unscaledTime;
        return second;
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
        blockUntil = Time.unscaledTime + 0.5f;

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
    private int fullStyle, fullExStyle;
    // What full mode looked like, restored on the way back.
    private int fullWidth, fullHeight;
    private FullScreenMode fullMode;
    private CameraClearFlags clearFlags;
    private Color background;
    private bool hdr, post;
    private bool placed, dragging, unlocked, f9, f10, left, right;
    private float lastTop;
    private POINT grabOffset;

    // Fixed keys, read with GetAsyncKeyState so they work while another app has focus.
    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private void Overlay()
    {
        bool nowF9 = Down(VK_F9), nowF10 = Down(VK_F10), nowLeft = Down(VK_LBUTTON), nowRight = Down(VK_RBUTTON);
        bool pressF9 = nowF9 && !f9, pressF10 = nowF10 && !f10, pressLeft = nowLeft && !left, pressRight = nowRight && !right;
        f9 = nowF9; f10 = nowF10; left = nowLeft; right = nowRight;
        if (!placed) return;

        if (pressF9) { Toggle(); return; }
        if (pressF10) SetUnlocked(!unlocked);
        // Other topmost windows can push it down, so it's raised again now and then.
        if (Time.unscaledTime - lastTop > 2f) { lastTop = Time.unscaledTime; SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); }
        if (!unlocked) return;

        if (LeaveClick(pressLeft && Inside(), pressRight && Inside())) { Toggle(); return; }
        Drag(pressLeft, nowLeft);
    }

    private bool Inside() => GetCursorPos(out POINT p) && GetWindowRect(hwnd, out RECT r) && p.x >= r.left && p.x < r.right && p.y >= r.top && p.y < r.bottom;

    // Locked: clicks pass straight through the window. Unlocked: it takes clicks so it can be dragged.
    private void SetUnlocked(bool on)
    {
        unlocked = on;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, on ? ex & ~WS_EX_TRANSPARENT : ex | WS_EX_TRANSPARENT);
        dragging = false;
    }

    // A thin frame while unlocked so it's clear the window can be moved.
    private void OnGUI()
    {
        if (!Active || !unlocked) return;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, Width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Height - 2, Width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 0, 2, Height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Width - 2, 0, 2, Height), Texture2D.whiteTexture);
    }

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
        fullExStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        ShowWindow(hwnd, SW_HIDE); // the taskbar entry only changes while the window is hidden
        SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        // Overlay: layered + transparent (click-through), no taskbar entry, never activated.
        SetWindowLong(hwnd, GWL_EXSTYLE, fullExStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp(Opacity, 0.2f, 1f) * 255);
        if (ColorKey) SetLayeredWindowAttributes(hwnd, KeyColorRef, alpha, LWA_COLORKEY | LWA_ALPHA);
        else
        {
            SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
            var margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, Width, Height, SWP_FRAMECHANGED | SWP_NOACTIVATE);
        ShowWindow(hwnd, SW_SHOWNA);
        unlocked = false;
        f9 = Down(VK_F9); f10 = Down(VK_F10); left = Down(VK_LBUTTON); right = Down(VK_RBUTTON); // a key held from before isn't a new press
        placed = true;
    }

    private void ExitWindow()
    {
        SavePosition();
        placed = dragging = unlocked = false;
        var margins = new MARGINS();
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        ShowWindow(hwnd, SW_HIDE);
        SetWindowLong(hwnd, GWL_EXSTYLE, fullExStyle);
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = clearFlags; cam.backgroundColor = background; cam.allowHDR = hdr;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = post;
        }
        SetWindowLong(hwnd, GWL_STYLE, fullStyle);
        SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
        ShowWindow(hwnd, SW_SHOW);
        Screen.SetResolution(fullWidth, fullHeight, fullMode);
    }

    private void SavePosition()
    {
        if (!placed || !GetWindowRect(hwnd, out RECT r)) return;
        PlayerPrefs.SetInt(PosX, r.left);
        PlayerPrefs.SetInt(PosY, r.top);
        PlayerPrefs.Save();
    }

    // While unlocked, holding the left button inside the window moves it. Cursor positions are in screen space,
    // so they stay valid while the window moves under the mouse.
    private void Drag(bool pressed, bool held)
    {
        if (pressed && GetCursorPos(out POINT p) && GetWindowRect(hwnd, out RECT r))
        {
            dragging = true;
            grabOffset = new POINT { x = p.x - r.left, y = p.y - r.top };
        }
        if (!held) dragging = false;
        if (dragging && GetCursorPos(out POINT now))
            SetWindowPos(hwnd, HWND_TOPMOST, now.x - grabOffset.x, now.y - grabOffset.y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_F9 = 0x78, VK_F10 = 0x79, SW_HIDE = 0, SW_SHOW = 5, SW_SHOWNA = 8;
    const uint LWA_COLORKEY = 0x1, LWA_ALPHA = 0x2, KeyColorRef = 0x0000FF00; // COLORREF 0x00BBGGRR: pure green
    const int WS_POPUP = unchecked((int)0x80000000), WS_VISIBLE = 0x10000000;
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);

    struct MARGINS { public int left, right, top, bottom; }
    struct RECT { public int left, top, right, bottom; }
    struct POINT { public int x, y; }

    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
#else
    private void SavePosition() { }

    // Editor / other platforms: only the UI swap, so the Mini mode key (and a double/right-click) leaves.
    private void Overlay()
    {
        if (Controls.Pressed(Controls.Act.MiniMode) || LeaveClick(Controls.ClickDown, Controls.RightClickDown)) Toggle();
    }
#endif
}
