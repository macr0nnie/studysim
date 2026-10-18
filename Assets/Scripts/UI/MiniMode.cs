using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Mini mode: shrinks the game to a small game-overlay window: borderless, always on top, see-through, no taskbar
// entry, never takes focus, and click-through so it doesn't get in the way of what's underneath. It only displays
// the room and a small sticky note with the countdown and a word for what's going on (no HUD, no room interaction). Ctrl + drag moves it any time; F10 unlocks it (a frame shows) and locks it again; the
// position is remembered. F9 leaves mini mode. In a Windows build both keys are read globally (even while another
// app has focus), so they're fixed to F9 and F10 there. While unlocked, a double-click or right-click also leaves.
// The timer, character and browser extension keep running underneath.
// The window calls run in Windows and Mac builds (Mac: Cocoa through the Objective-C runtime, below); in the Editor
// only the UI and input swap happens.
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
    private TimerManager timer;
    private TMP_Text timerText, stateText; // the only UI kept while mini: one card with the countdown and its state
    private GameObject hint; // the keys, shown for the first few seconds of each mini session
    private float hintUntil, nextSweep;
    private string shownState;
    private bool wasDistracted;
    private float shakeUntil;
    private float lastClick = -1, blockUntil;
    private int shownTimer = -1;
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
        if ((GameSettings.Windows || GameSettings.Mac) && FindAnyObjectByType<RoomManager>() != null && instance == null) // needs a desktop window it can reshape
            instance = new GameObject("MiniMode").AddComponent<MiniMode>();
    }

    // A small square button just right of the dock. Its canvas is a root canvas, so going mini hides it with the rest.
    private void Start()
    {
        Canvas canvas = MakeCanvas("MiniButtonCanvas", transform, 10);
        Button button = TextButton("MiniButton", canvas.transform, "Mini", TabColor, CaptionSize);
        button.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.25f);
        KeyHint(button.GetComponentInChildren<TMP_Text>(), "Mini", Controls.Act.MiniMode, onHover: true); // the key shows on hover, like the dock
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0);
        rect.sizeDelta = new Vector2(NavHeight, NavHeight);
        rect.anchoredPosition = new Vector2((DockSlots - 1) / 2f * (NavWidth + NavGap) + NavWidth / 2 + NavGap + NavHeight / 2, NavY);
        button.onClick.AddListener(() => { if (!Active) Toggle(); });
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
        if (Active) Progress();
        if (timerText != null && timer != null)
        {
            if (hint != null && Time.unscaledTime > hintUntil) Destroy(hint);
            // Toasts and pop-ups made while mini would otherwise show up over the room.
            if (Time.unscaledTime > nextSweep) { nextSweep = Time.unscaledTime + 1f; HideOthers(); }
            string state = wasDistracted ? "distracted" : !timer.IsTimerRunning ? "paused" : timer.IsStudySession ? "focus" : "break";
            if (state != shownState)
            {
                shownState = state;
                stateText.text = state;
                stateText.color = wasDistracted ? new Color(0.78f, 0.25f, 0.22f) : Ink;
                timerText.alpha = timer.IsTimerRunning ? 1f : 0.55f; // paused reads as faded ink
            }
            int s = Mathf.Max(0, Mathf.CeilToInt(timer.CurrentTime));
            if (s == shownTimer) return; // the string only changes once a second
            shownTimer = s;
            timerText.text = $"{s / 60:00}:{s % 60:00}";
        }
    }

    // A distracting site during a study session shakes the window once (the character's face changes too).
    private void Progress()
    {
        if (timer == null) return;
        bool distracted = timer.IsStudySession && timer.IsTimerRunning && Distraction.Reported;
        if (distracted && !wasDistracted) shakeUntil = Time.unscaledTime + 0.8f;
        wasDistracted = distracted;
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
        if (room == null) room = FindAnyObjectByType<RoomManager>();
        if (room != null)
        {
            if (Active && room.IsEditMode) room.ToggleEditMode();
            room.enabled = !Active;
        }

        if (Active)
        {
            hidden.Clear();
            HideOthers(); // the HUD and the character's bubble too: the card already says focus / break / distracted
            ShowTimer();
        }
        else
        {
            if (timerText != null) Destroy(timerText.canvas.gameObject);
            timerText = null;
            foreach (Canvas c in hidden) if (c != null) Show(c, true);
            hidden.Clear();
        }
        Economy(Active);
        blockUntil = Time.unscaledTime + 0.5f;

#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        if (Active) StartCoroutine(EnterWindow()); else ExitWindow();
#endif
    }

    private void HideOthers()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>())
        {
            if (!c.isRootCanvas || !c.enabled || c.name == "MiniCardCanvas") continue;
            Show(c, false); hidden.Add(c);
        }
    }

    // One cosy card at the top: the countdown, and under it a quiet word for the state. The keys show below it for a
    // few seconds so a new player knows how to move it and get back.
    private void ShowTimer()
    {
        if (timer == null) timer = FindAnyObjectByType<TimerManager>();
        if (timer == null) return;
        Canvas canvas = MakeCanvas("MiniCardCanvas", transform, 50);
        // The mini window is only 360x300, so the 1920x1080 scaling would shrink the text to a few pixels.
        canvas.GetComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
        Destroy(canvas.GetComponent<GraphicRaycaster>());

        // A small sticky note taped in the top right corner, slightly crooked. Paper colours stay the same in both themes.
        RectTransform note = Card("Note", canvas.transform, new Vector2(96, 84), new Vector2(1, 1), new Vector2(-14, -16), new Color(1f, 0.92f, 0.62f));
        note.localRotation = Quaternion.Euler(0, 0, -4);
        note.GetComponent<Image>().pixelsPerUnitMultiplier = 4; // paper corners, not the HUD's round ones
        RectTransform tape = Card("Tape", note, new Vector2(38, 14), new Vector2(0.5f, 1), new Vector2(0, 7), new Color(1f, 1f, 1f, 0.55f));
        tape.localRotation = Quaternion.Euler(0, 0, 6);
        tape.GetComponent<Image>().pixelsPerUnitMultiplier = 4;
        Destroy(tape.GetComponent<Shadow>());
        shownTimer = -1; shownState = null; // fresh labels start empty
        timerText = MakeText("MiniTimer", note, "", 30, Ink, TextAlignmentOptions.Center, true);
        Anchor(timerText.gameObject, new Vector2(0, 0.36f), new Vector2(1, 0.92f));
        stateText = MakeText("MiniState", note, "", LabelSize, Ink, TextAlignmentOptions.Top);
        Anchor(stateText.gameObject, new Vector2(0, 0.08f), new Vector2(1, 0.4f));

        // shortcut: the Windows and Mac keys are fixed (read globally); elsewhere it's the rebindable Mini mode key.
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
        string keys = "<b>F9</b> back to the room   <b>Ctrl</b> + drag to move\n<b>F10</b> unlock   <b>Ctrl+F9</b> reset spot";
#else
        string keys = $"<b>{Controls.KeyName(Controls.Act.MiniMode)}</b> or double-click to go back";
#endif
        RectTransform tip = Card("Keys", canvas.transform, new Vector2(300, 52), new Vector2(0.5f, 0), new Vector2(0, 12), PanelColor);
        TMP_Text tipText = MakeText("Text", tip, keys, 12, TextColor, TextAlignmentOptions.Center);
        Anchor(tipText.gameObject, Vector2.zero, Vector2.one);
        tipText.margin = new Vector4(8, 4, 8, 4);
        hint = tip.gameObject;
        hintUntil = Time.unscaledTime + 8f;

        wasDistracted = false;
    }

    private static readonly Color Ink = new Color(0.30f, 0.22f, 0.16f); // brown pen on the note

    // A rounded panel pinned to one spot (anchor = pivot) of its parent.
    private static RectTransform Card(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 position, Color color)
    {
        GameObject go = Make(name, parent, typeof(Image), typeof(Shadow));
        Image image = go.GetComponent<Image>();
        Style(image, color);
        image.raycastTarget = false;
        go.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.25f);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
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
    private bool placed, dragging, grabbing, unlocked, f9, f10, left, right, shaking;
    private RECT shakeBase;
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

        if (pressF9 && Down(VK_CONTROL)) { ResetPosition(); return; }
        if (pressF9) { Toggle(); return; }
        if (pressF10) SetUnlocked(!unlocked);
        // Other topmost windows can push it down, so it's raised again now and then.
        if (Time.unscaledTime - lastTop > 2f) { lastTop = Time.unscaledTime; SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); }
        // The distraction nudge: a short side-to-side wiggle of the whole window, then back where it was.
        if (Time.unscaledTime < shakeUntil && !dragging)
        {
            if (!shaking && GetWindowRect(hwnd, out shakeBase)) shaking = true;
            if (shaking) SetWindowPos(hwnd, HWND_TOPMOST, shakeBase.left + Mathf.RoundToInt(Mathf.Sin(Time.unscaledTime * 30f) * 8f), shakeBase.top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else if (shaking)
        {
            shaking = false;
            SetWindowPos(hwnd, HWND_TOPMOST, shakeBase.left, shakeBase.top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }
        if (!unlocked)
        {
            // Holding Ctrl over the window grabs it for a drag without F10: it takes clicks only while Ctrl is held
            // (or the drag lasts), so the app underneath doesn't get them. Ctrl, not Alt: a lone Alt opens menus.
            bool grab = dragging || (Down(VK_CONTROL) && Inside());
            if (grab != grabbing) { grabbing = grab; ClickThrough(!grab); }
            if (grabbing) Drag(pressLeft, nowLeft);
            return;
        }

        if (LeaveClick(pressLeft && Inside(), pressRight && Inside())) { Toggle(); return; }
        Drag(pressLeft, nowLeft);
    }

    private static int DefaultX => Display.main.systemWidth - Width - 24;
    private const int DefaultY = 24;

    // Ctrl+F9: back to the top right corner (and forget the saved spot).
    private void ResetPosition()
    {
        PlayerPrefs.DeleteKey(PosX); PlayerPrefs.DeleteKey(PosY);
        SetWindowPos(hwnd, HWND_TOPMOST, DefaultX, DefaultY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private bool Inside() => GetCursorPos(out POINT p) && GetWindowRect(hwnd, out RECT r) && p.x >= r.left && p.x < r.right && p.y >= r.top && p.y < r.bottom;

    // Locked: clicks pass straight through the window. Unlocked: it takes clicks so it can be dragged.
    private void SetUnlocked(bool on)
    {
        unlocked = on;
        ClickThrough(!on);
        dragging = grabbing = false;
    }

    private void ClickThrough(bool on)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, on ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
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
        int x = PlayerPrefs.GetInt(PosX, DefaultX), y = PlayerPrefs.GetInt(PosY, DefaultY);
        // A saved spot on a monitor that's gone would leave it unreachable: keep it inside the desktop.
        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
        if (vw > Width && vh > Height) { x = Mathf.Clamp(x, vx, vx + vw - Width); y = Mathf.Clamp(y, vy, vy + vh - Height); }
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
        unlocked = grabbing = false;
        f9 = Down(VK_F9); f10 = Down(VK_F10); left = Down(VK_LBUTTON); right = Down(VK_RBUTTON); // a key held from before isn't a new press
        placed = true;
    }

    private void ExitWindow()
    {
        SavePosition();
        placed = dragging = grabbing = unlocked = false;
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
    const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_F9 = 0x78, VK_F10 = 0x79, VK_CONTROL = 0x11, SW_HIDE = 0, SW_SHOW = 5, SW_SHOWNA = 8;
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
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
    // Mac: the same overlay through Cocoa's NSWindow, called with objc_msgSend (no native plugin, so it builds off a Mac).
    // Coordinates are Cocoa's: points, origin at the bottom left of the main screen.
    private IntPtr window;
    private ulong fullStyle, fullBehavior;
    private long fullLevel;
    private int fullWidth, fullHeight;
    private FullScreenMode fullMode;
    private CameraClearFlags clearFlags;
    private Color background;
    private bool hdr, post;
    private bool placed, dragging, grabbing, unlocked, f9, f10, left, right, shaking;
    private NSPoint shakeBase, grabOffset;
    private float lastTop;

    // F9/F10 read globally so they work while another app is in front. macOS may only allow that with
    // Input Monitoring permission, so the game's own F9 binding also works whenever lofilife is in front.
    private static bool Key(ushort mac) => CGEventSourceKeyState(0, mac);
    private static ulong Buttons => (ulong)MsgLong(Class("NSEvent"), Sel("pressedMouseButtons"));
    private static bool Ctrl => ((ulong)MsgLong(Class("NSEvent"), Sel("modifierFlags")) & (1UL << 18)) != 0;
    private static NSPoint Mouse => MsgPoint(Class("NSEvent"), Sel("mouseLocation"));

    private void Overlay()
    {
        bool nowF9 = Key(MacF9) || Controls.Pressed(Controls.Act.MiniMode), nowF10 = Key(MacF10);
        ulong buttons = Buttons;
        bool nowLeft = (buttons & 1) != 0, nowRight = (buttons & 2) != 0;
        bool pressF9 = nowF9 && !f9, pressF10 = nowF10 && !f10, pressLeft = nowLeft && !left, pressRight = nowRight && !right;
        f9 = nowF9; f10 = nowF10; left = nowLeft; right = nowRight;
        if (!placed) return;

        if (pressF9 && Ctrl) { ResetPosition(); return; }
        if (pressF9) { Toggle(); return; }
        if (pressF10) SetUnlocked(!unlocked);
        if (Time.unscaledTime - lastTop > 2f) { lastTop = Time.unscaledTime; MsgVoid(window, Sel("orderFrontRegardless")); }
        // The distraction nudge: a short wiggle, then back where it was.
        if (Time.unscaledTime < shakeUntil && !dragging)
        {
            if (!shaking) { shakeBase = Frame(window).origin; shaking = true; }
            Move(new NSPoint { x = shakeBase.x + Mathf.Sin(Time.unscaledTime * 30f) * 8f, y = shakeBase.y });
        }
        else if (shaking) { shaking = false; Move(shakeBase); }
        if (!unlocked)
        {
            bool grab = dragging || (Ctrl && Inside());
            if (grab != grabbing) { grabbing = grab; MsgBool(window, Sel("setIgnoresMouseEvents:"), !grab); }
            if (grabbing) Drag(pressLeft, nowLeft);
            return;
        }
        if (LeaveClick(pressLeft && Inside(), pressRight && Inside())) { Toggle(); return; }
        Drag(pressLeft, nowLeft);
    }

    private void Move(NSPoint origin) => MsgSetPoint(window, Sel("setFrameOrigin:"), origin);

    private bool Inside()
    {
        NSPoint p = Mouse; NSRect r = Frame(window);
        return p.x >= r.origin.x && p.x < r.origin.x + r.size.x && p.y >= r.origin.y && p.y < r.origin.y + r.size.y;
    }

    private void SetUnlocked(bool on)
    {
        unlocked = on;
        MsgBool(window, Sel("setIgnoresMouseEvents:"), !on);
        dragging = grabbing = false;
    }

    // Top right of the main screen, below the menu bar.
    private NSPoint DefaultOrigin()
    {
        NSRect screen = Frame(MsgPtr(Class("NSScreen"), Sel("mainScreen")), "visibleFrame"), r = Frame(window);
        return new NSPoint { x = screen.origin.x + screen.size.x - r.size.x - 24, y = screen.origin.y + screen.size.y - r.size.y - 24 };
    }

    private void ResetPosition()
    {
        PlayerPrefs.DeleteKey(PosX); PlayerPrefs.DeleteKey(PosY);
        Move(DefaultOrigin());
    }

    private void OnGUI()
    {
        if (!Active || !unlocked) return;
        int w = Screen.width, h = Screen.height;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0, 0, w, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, h - 2, w, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 0, 2, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(w - 2, 0, 2, h), Texture2D.whiteTexture);
    }

    private System.Collections.IEnumerator EnterWindow()
    {
        IntPtr app = MsgPtr(Class("NSApplication"), Sel("sharedApplication"));
        window = MsgPtr(app, Sel("mainWindow"));
        if (window == IntPtr.Zero) window = MsgIndex(MsgPtr(app, Sel("windows")), Sel("objectAtIndex:"), 0);
        if (window == IntPtr.Zero) yield break;
        fullWidth = Screen.width; fullHeight = Screen.height; fullMode = Screen.fullScreenMode;
        fullStyle = (ulong)MsgLong(window, Sel("styleMask"));
        fullLevel = MsgLong(window, Sel("level"));
        fullBehavior = (ulong)MsgLong(window, Sel("collectionBehavior"));

        Camera cam = Camera.main;
        if (cam != null)
        {
            clearFlags = cam.clearFlags; background = cam.backgroundColor; hdr = cam.allowHDR;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.allowHDR = false;
            var data = cam.GetUniversalAdditionalCameraData();
            post = data.renderPostProcessing;
            data.renderPostProcessing = false;
        }

        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
        yield return null; // Unity applies the resolution at the end of the frame
        if (!Active) yield break;
        MsgULong(window, Sel("setStyleMask:"), 0); // borderless
        MsgULong(window, Sel("setLevel:"), 3); // floating: above normal windows
        MsgULong(window, Sel("setCollectionBehavior:"), 1 | 16 | 256); // every Space, stays put, over full-screen apps
        See(false);
        MsgDouble(window, Sel("setAlphaValue:"), Mathf.Clamp(Opacity, 0.2f, 1f));
        MsgBool(window, Sel("setIgnoresMouseEvents:"), true);

        NSPoint origin = PlayerPrefs.HasKey(PosX) ? new NSPoint { x = PlayerPrefs.GetInt(PosX), y = PlayerPrefs.GetInt(PosY) } : DefaultOrigin();
        Move(OnSomeScreen(origin));
        MsgVoid(window, Sel("orderFrontRegardless"));
        unlocked = grabbing = false;
        ulong buttons = Buttons;
        f9 = true; f10 = Key(MacF10); left = (buttons & 1) != 0; right = (buttons & 2) != 0; // a key held from before isn't a new press
        placed = true;
    }

    // A saved spot on a display that's gone would leave it unreachable: then it goes back to the default corner.
    private NSPoint OnSomeScreen(NSPoint p)
    {
        IntPtr screens = MsgPtr(Class("NSScreen"), Sel("screens"));
        long count = MsgLong(screens, Sel("count"));
        for (long i = 0; i < count; i++)
        {
            NSRect s = Frame(MsgIndex(screens, Sel("objectAtIndex:"), (ulong)i));
            if (p.x >= s.origin.x && p.x < s.origin.x + s.size.x - 40 && p.y >= s.origin.y && p.y < s.origin.y + s.size.y - 40) return p;
        }
        return DefaultOrigin();
    }

    // see = true restores the normal opaque window; false makes everything the camera leaves clear see-through.
    private void See(bool see)
    {
        MsgBool(window, Sel("setOpaque:"), see);
        MsgBool(window, Sel("setHasShadow:"), see);
        MsgSetPtr(window, Sel("setBackgroundColor:"), MsgPtr(Class("NSColor"), Sel(see ? "windowBackgroundColor" : "clearColor")));
        IntPtr layer = MsgPtr(MsgPtr(window, Sel("contentView")), Sel("layer"));
        if (layer != IntPtr.Zero) MsgBool(layer, Sel("setOpaque:"), see);
    }

    private void ExitWindow()
    {
        if (window == IntPtr.Zero) return;
        SavePosition();
        placed = dragging = grabbing = unlocked = false;
        MsgBool(window, Sel("setIgnoresMouseEvents:"), false);
        MsgDouble(window, Sel("setAlphaValue:"), 1);
        See(true);
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = clearFlags; cam.backgroundColor = background; cam.allowHDR = hdr;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = post;
        }
        MsgULong(window, Sel("setCollectionBehavior:"), fullBehavior);
        MsgULong(window, Sel("setLevel:"), (ulong)fullLevel);
        MsgULong(window, Sel("setStyleMask:"), fullStyle);
        Screen.SetResolution(fullWidth, fullHeight, fullMode);
    }

    private void SavePosition()
    {
        if (!placed) return;
        NSPoint o = Frame(window).origin;
        PlayerPrefs.SetInt(PosX, Mathf.RoundToInt((float)o.x));
        PlayerPrefs.SetInt(PosY, Mathf.RoundToInt((float)o.y));
        PlayerPrefs.Save();
    }

    private void Drag(bool pressed, bool held)
    {
        if (pressed)
        {
            NSPoint p = Mouse, o = Frame(window).origin;
            dragging = true;
            grabOffset = new NSPoint { x = p.x - o.x, y = p.y - o.y };
        }
        if (!held) dragging = false;
        if (dragging) { NSPoint now = Mouse; Move(new NSPoint { x = now.x - grabOffset.x, y = now.y - grabOffset.y }); }
    }

    const ushort MacF9 = 101, MacF10 = 109; // kVK_F9, kVK_F10

    struct NSPoint { public double x, y; }
    struct NSRect { public NSPoint origin, size; }

    // A 32-byte struct comes back through objc_msgSend_stret on Intel and through plain objc_msgSend on Apple silicon.
    private static NSRect Frame(IntPtr obj, string selector = "frame")
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64) { MsgRectStret(out NSRect r, obj, Sel(selector)); return r; }
        return MsgRect(obj, Sel(selector));
    }

    const string ObjC = "/usr/lib/libobjc.A.dylib";
    private static IntPtr Class(string name) => objc_getClass(name);
    private static IntPtr Sel(string name) => sel_registerName(name);
    [DllImport(ObjC)] static extern IntPtr objc_getClass(string name);
    [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern IntPtr MsgPtr(IntPtr obj, IntPtr sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void MsgVoid(IntPtr obj, IntPtr sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern long MsgLong(IntPtr obj, IntPtr sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern IntPtr MsgIndex(IntPtr obj, IntPtr sel, ulong index);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void MsgULong(IntPtr obj, IntPtr sel, ulong value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void MsgBool(IntPtr obj, IntPtr sel, [MarshalAs(UnmanagedType.I1)] bool value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void MsgDouble(IntPtr obj, IntPtr sel, double value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void MsgSetPtr(IntPtr obj, IntPtr sel, IntPtr value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void MsgSetPoint(IntPtr obj, IntPtr sel, NSPoint value);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern NSPoint MsgPoint(IntPtr obj, IntPtr sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern NSRect MsgRect(IntPtr obj, IntPtr sel);
    [DllImport(ObjC, EntryPoint = "objc_msgSend_stret")] static extern void MsgRectStret(out NSRect result, IntPtr obj, IntPtr sel);
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)] static extern bool CGEventSourceKeyState(int state, ushort key);
#else
    private void SavePosition() { }

    // Editor / other platforms: only the UI swap, so the Mini mode key (and a double/right-click) leaves.
    private void Overlay()
    {
        if (Controls.Pressed(Controls.Act.MiniMode) || LeaveClick(Controls.ClickDown, Controls.RightClickDown)) Toggle();
    }
#endif
}
