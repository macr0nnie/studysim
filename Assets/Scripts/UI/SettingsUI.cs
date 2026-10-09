using System;
using System.Collections;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Game settings window (top-left Settings button or F10): Sound, Focus, Notifications, Controls, the
// browser extension and Credits, plus Save and quit. Values live in GameSettings and Controls.
public class SettingsUI : MonoBehaviour
{

    private GameObject window;
    private GameObject[] pages;
    private Image[] tabs;
    private TimerManager timer;
    private ChromeWebEx bridge;

    // Focus page
    private Image[] modeCards;
    private GameObject customLengths;
    private Slider study, rest;
    private TMP_Text studyText, restText, timerNote;

    // Extension page
    private Image statusDot;
    private TMP_Text statusText, testResult;

    private static readonly string[] PageNames = { "Sound", "Focus", "Notifications", "Controls", "Browser extension", "Credits" };
    private const int ExtensionPage = 4;

    // Controls page
    private TMP_Text[] keyLabels;
    private TMP_Text controlsNote;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureSettings();
        EnsureSettings();
    }

    private static void EnsureSettings()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<SettingsUI>() == null)
            new GameObject("Settings").AddComponent<SettingsUI>();
    }

    private void Awake()
    {
        timer = FindFirstObjectByType<TimerManager>();
        bridge = FindFirstObjectByType<ChromeWebEx>();
        BuildUI();
    }

    private void Update()
    {
        if (Controls.Pressed(Controls.Act.Settings) && !Typing()) Toggle();
        if (window.activeSelf && pages[ExtensionPage].activeSelf) RefreshExtension();
    }

    public void Toggle()
    {
        window.SetActive(!window.activeSelf);
        if (window.activeSelf) RefreshFocus();
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            tabs[i].color = i == index ? SelectedColor : TabColor;
        }
        if (index == 1) RefreshFocus();
    }

    private static void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- focus ----------

    private void PickMode(GameSettings.FocusMode mode)
    {
        if (timer != null && timer.IsTimerRunning) return; // lengths only change while stopped
        GameSettings.Mode = mode;
        var preset = GameSettings.Modes[(int)mode];
        if (timer != null && mode != GameSettings.FocusMode.Custom)
        {
            timer.SetCustomDuration(preset.study);
            timer.SetBreakDuration(preset.rest);
        }
        RefreshFocus();
    }

    private void RefreshFocus()
    {
        bool running = timer != null && timer.IsTimerRunning;
        for (int i = 0; i < modeCards.Length; i++)
            modeCards[i].color = i == (int)GameSettings.Mode ? SelectedColor : CardColor;
        foreach (Image card in modeCards) card.GetComponent<Button>().interactable = !running;
        customLengths.SetActive(GameSettings.Mode == GameSettings.FocusMode.Custom && timer != null);
        timerNote.gameObject.SetActive(running);
        if (timer == null) return;
        // Labels only: invoking onValueChanged would reset a paused timer.
        study.SetValueWithoutNotify(Mathf.Round(timer.StudyMinutes / 5f));
        rest.SetValueWithoutNotify(Mathf.Round(timer.BreakMinutes));
        study.interactable = rest.interactable = !running;
        studyText.text = $"{study.value * 5:0} min";
        restText.text = $"{rest.value:0} min";
    }

    // ---------- browser extension ----------

    private void RefreshExtension()
    {
        if (bridge == null)
        {
            SetStatus(DangerColor, "The extension bridge isn't in this scene.");
            return;
        }
        if (!bridge.IsListening)
        {
            SetStatus(DangerColor, "Not listening. Another program may be using port 8080; close it and press Reconnect."
                + (bridge.ListenError.Length > 0 ? $"\n<size={CaptionSize}>{bridge.ListenError}</size>" : ""));
            return;
        }
        float ago = bridge.LastExtensionMessageAt < 0 ? -1 : Time.unscaledTime - bridge.LastExtensionMessageAt;
        if (ago < 0) SetStatus(AccentColor, $"Listening on {ChromeWebEx.Address}. Waiting for the extension's first message.");
        else SetStatus(XpColor, $"Connected. Last message {Ago(ago)}: {bridge.LastResult}");
    }

    private static string Ago(float seconds) =>
        seconds < 5 ? "just now" : seconds < 120 ? $"{seconds:0}s ago" : $"{seconds / 60:0} min ago";

    private void SetStatus(Color color, string text)
    {
        statusDot.color = color;
        statusText.text = text;
    }

    // Posts a ping to our own listener: proves the extension's address works without changing anything.
    // HttpClient rather than UnityWebRequest, which the player settings block for plain http.
    private IEnumerator Test()
    {
        testResult.text = "Testing...";
        using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
        {
            Task<HttpResponseMessage> request = client.PostAsync(ChromeWebEx.Address, new StringContent("ping"));
            while (!request.IsCompleted) yield return null;
            bool ok = request.Status == TaskStatus.RanToCompletion && request.Result.IsSuccessStatusCode;
            testResult.text = ok
                ? "The game is reachable. The extension can connect."
                : $"No answer from {ChromeWebEx.Address}. Press Reconnect.";
        }
    }

    private const string SetupInfo =
        "Study Sim browser extension setup\n" +
        "Send POST requests to http://localhost:8080/ with a plain word or JSON {\"action\":\"...\",\"minutes\":25}:\n" +
        "  start, pause, reset: control the timer\n" +
        "  set: study length in minutes (5-120, timer stopped)\n" +
        "  focus: on a study site, +1 XP at most once a minute\n" +
        "  distracted: on a distracting site, {\"action\":\"distracted\",\"site\":\"youtube.com\"}; re-send at least\n" +
        "    every 45 s while it stays open (during a study session the room falls apart and coins drain)\n" +
        "  back: left the distracting site (ends it, no XP)\n" +
        "  ping: connection check\n" +
        "GET http://localhost:8080/ returns {\"running\",\"studying\",\"secondsLeft\",\"mode\",\"strict\",\"distracted\"} so the\n" +
        "extension can follow the timer, e.g. block distracting sites during a strict session.";

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("SettingsCanvas", transform, 20).transform;

        // Next to the player card, top left.
        Button open = TextButton("SettingsButton", canvas, "Settings", TabColor, LabelSize + 1);
        KeyHint(open.GetComponentInChildren<TMP_Text>(), "Settings", Controls.Act.Settings);
        var openRect = (RectTransform)open.transform;
        openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0, 1);
        openRect.sizeDelta = new Vector2(130, 44);
        openRect.anchoredPosition = new Vector2(24 + 380 + 12, -46);
        open.onClick.AddListener(Toggle);

        // Dimmed backdrop (click to close) with the window centred on it. They are siblings so a click
        // inside the window doesn't bubble up to the backdrop's button.
        window = Make("SettingsWindow", canvas);
        Stretch((RectTransform)window.transform);
        GameObject backdrop = Make("Backdrop", window.transform, typeof(Image), typeof(Button));
        Stretch((RectTransform)backdrop.transform);
        backdrop.GetComponent<Image>().color = new Color(0.05f, 0.03f, 0.08f, 0.6f);
        backdrop.GetComponent<Button>().transition = Selectable.Transition.None;
        backdrop.GetComponent<Button>().onClick.AddListener(Toggle);

        GameObject frame = Make("Frame", window.transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(Shadow));
        var frameRect = (RectTransform)frame.transform;
        frameRect.sizeDelta = new Vector2(900, 620);
        Style(frame.GetComponent<Image>(), PanelColor);
        frame.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.4f);
        frame.GetComponent<Shadow>().effectDistance = new Vector2(0, -8);
        var frameLayout = frame.GetComponent<VerticalLayoutGroup>();
        frameLayout.padding = new RectOffset(24, 24, 20, 24);
        frameLayout.spacing = 16;
        frameLayout.childControlWidth = frameLayout.childControlHeight = true;
        frameLayout.childForceExpandWidth = true;
        frameLayout.childForceExpandHeight = false;
        Header(frame.transform, "Settings", Toggle);

        GameObject body = Row("Body", frame.transform, 0, 24, false);
        body.GetComponent<LayoutElement>().flexibleHeight = 1;
        body.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        // Left: page tabs, Save and quit at the bottom.
        GameObject side = Column("Tabs", body.transform, 8);
        side.AddComponent<LayoutElement>().preferredWidth = 210;
        tabs = new Image[PageNames.Length];
        for (int i = 0; i < PageNames.Length; i++)
        {
            int index = i;
            Button tab = TextButton(PageNames[i], side.transform, PageNames[i], TabColor, BodySize);
            tab.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            tab.onClick.AddListener(() => ShowPage(index));
            tabs[i] = tab.GetComponent<Image>();
        }
        Make("Spacer", side.transform, typeof(LayoutElement)).GetComponent<LayoutElement>().flexibleHeight = 1;
        Button quit = TextButton("Quit", side.transform, "Save and quit", DangerColor, BodySize);
        quit.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
        quit.onClick.AddListener(Quit);

        // Right: one page per tab.
        GameObject content = Make("Pages", body.transform, typeof(LayoutElement));
        content.GetComponent<LayoutElement>().flexibleWidth = 1;
        pages = new GameObject[PageNames.Length];
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = Column(PageNames[i] + "Page", content.transform, 10);
            Stretch((RectTransform)pages[i].transform);
        }
        BuildSound(pages[0].transform);
        BuildFocus(pages[1].transform);
        BuildNotifications(pages[2].transform);
        BuildControls(pages[3].transform);
        BuildExtension(pages[ExtensionPage].transform);
        BuildCredits(pages[5].transform);

        ShowPage(0);
        window.SetActive(false);
    }

    private void BuildSound(Transform page)
    {
        Heading(page, "Sound");
        AddSlider(page, "Master volume", 0, 100, GameSettings.MasterVolume * 100, v => $"{v:0}%", v => GameSettings.MasterVolume = v / 100f, out _);
        AddSlider(page, "Music", 0, 100, GameSettings.MusicVolume * 100, v => $"{v:0}%", v => GameSettings.MusicVolume = v / 100f, out _);
        AddSlider(page, "Effects (UI sounds, chime)", 0, 100, GameSettings.EffectsVolume * 100, v => $"{v:0}%", v => GameSettings.EffectsVolume = v / 100f, out _);
        Switch(page, "Interface sounds", "Clicks, placing furniture, coins and lamps.", () => GameSettings.UISounds, v => GameSettings.UISounds = v);
    }

    private void BuildFocus(Transform page)
    {
        Heading(page, "Focus mode");
        GameObject grid = Make("Modes", page, typeof(GridLayoutGroup));
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(298, 64);
        layout.spacing = new Vector2(10, 10);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;
        modeCards = new Image[GameSettings.Modes.Length];
        for (int i = 0; i < modeCards.Length; i++)
        {
            var mode = (GameSettings.FocusMode)i;
            var preset = GameSettings.Modes[i];
            GameObject card = Make(preset.name, grid.transform, typeof(Image), typeof(Button));
            Style(card.GetComponent<Image>(), CardColor);
            card.GetComponent<Button>().onClick.AddListener(() => PickMode(mode));
            TMP_Text label = MakeText("Label", card.transform,
                $"<b>{preset.name}</b>\n<size={CaptionSize}><color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{preset.detail}</color></size>",
                BodySize, TextColor, TextAlignmentOptions.Left);
            Stretch((RectTransform)label.transform);
            ((RectTransform)label.transform).offsetMin = new Vector2(14, 0);
            modeCards[i] = card.GetComponent<Image>();
        }

        customLengths = Column("CustomLengths", page, 6);
        if (timer != null)
        {
            study = AddSlider(customLengths.transform, "Study session", 1, 24, Mathf.Round(timer.StudyMinutes / 5f), v => $"{v * 5:0} min",
                v => { if (!timer.IsTimerRunning) timer.SetCustomDuration(v * 5); }, out studyText);
            rest = AddSlider(customLengths.transform, "Break", 1, 30, Mathf.Round(timer.BreakMinutes), v => $"{v:0} min",
                v => { if (!timer.IsTimerRunning) timer.SetBreakDuration(v); }, out restText);
        }
        timerNote = MakeText("TimerNote", page, "Stop the timer to change the focus mode.", CaptionSize, MutedText, TextAlignmentOptions.Left);
        timerNote.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        Switch(page, "Strict focus", "Study sessions can't be paused, only given up (no rewards).",
            () => GameSettings.StrictFocus, v => GameSettings.StrictFocus = v);
        Switch(page, "Start breaks automatically", "A finished session rolls straight into its break.",
            () => GameSettings.AutoStartBreak, v => GameSettings.AutoStartBreak = v);
        Switch(page, "Start the next session automatically", "After a break, the next session starts by itself.",
            () => GameSettings.AutoStartStudy, v => GameSettings.AutoStartStudy = v);
    }

    private void BuildNotifications(Transform page)
    {
        Heading(page, "Notifications");
        Switch(page, "Session messages", "When a session or break ends.", () => GameSettings.SessionToasts, v => GameSettings.SessionToasts = v);
        Switch(page, "Session chime", "A soft chime when a session or break ends.", () => GameSettings.SessionChime, v => GameSettings.SessionChime = v);
        Switch(page, "Level-up messages", "When you reach a new level.", () => GameSettings.LevelUpToasts, v => GameSettings.LevelUpToasts = v);
        Switch(page, "Autosave note", "The small \"Autosaved\" note, bottom right.", () => GameSettings.AutosaveBadge, v => GameSettings.AutosaveBadge = v);
    }

    private void BuildExtension(Transform page)
    {
        Heading(page, "Browser extension");
        GameObject statusRow = Row("Status", page, 64, 12, false);
        Style(statusRow.AddComponent<Image>(), CardColor);
        statusRow.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(14, 14, 8, 8);
        statusRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        GameObject dot = Make("Dot", statusRow.transform, typeof(Image), typeof(LayoutElement));
        dot.GetComponent<LayoutElement>().preferredWidth = 14;
        dot.GetComponent<LayoutElement>().preferredHeight = 14;
        statusDot = dot.GetComponent<Image>();
        statusDot.sprite = Circle;
        statusText = MakeText("Text", statusRow.transform, "", LabelSize, TextColor, TextAlignmentOptions.Left);
        statusText.textWrappingMode = TextWrappingModes.Normal;
        statusText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        GameObject buttons = Row("Buttons", page, 42, 10, false);
        SmallButton(buttons.transform, "Test connection", AccentButtonColor, 170).onClick.AddListener(() => StartCoroutine(Test()));
        SmallButton(buttons.transform, "Reconnect", TabColor, 120).onClick.AddListener(() => { if (bridge != null) bridge.Restart(); });
        SmallButton(buttons.transform, "Copy setup info", TabColor, 160).onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = SetupInfo;
            testResult.text = "Setup info copied.";
        });
        testResult = MakeText("Result", page, "", LabelSize, MutedText, TextAlignmentOptions.Left);
        testResult.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        Switch(page, "Distractions break the room", "On a distracting site during a study session, your room falls apart and coins drain.",
            () => GameSettings.DistractionPenalty, v => GameSettings.DistractionPenalty = v);
        AddSlider(page, "Coins lost per minute while distracted", 0, 30, GameSettings.DistractionCoinsPerMinute, v => $"{v:0}",
            v => GameSettings.DistractionCoinsPerMinute = Mathf.RoundToInt(v), out _);

        TMP_Text help = MakeText("Help", page,
            "Install the extension from the game's BrowserExtension folder (Chrome: Extensions, Developer mode, Load unpacked), "
            + "keep the game running, and it connects by itself. It talks to the game at "
            + $"<color=#{ColorUtility.ToHtmlStringRGB(AccentColor)}>{ChromeWebEx.Address}</color>: it can start, pause and reset the timer, "
            + "set the session length, earn focus XP on study sites, and tell the game when you're on a distracting one. "
            + "Copy setup info gives the full message list.",
            LabelSize, MutedText, TextAlignmentOptions.TopLeft);
        help.textWrappingMode = TextWrappingModes.Normal;
        help.enableAutoSizing = false;
        help.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
    }

    private void BuildControls(Transform page)
    {
        Heading(page, "Controls");
        Transform list = ScrollList(page, out _);
        StackChildren(list, 6);
        var labels = Controls.Labels;
        keyLabels = new TMP_Text[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            var act = (Controls.Act)i;
            GameObject row = Row(labels[i], list, 40, 12, false);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            MakeText("Label", row.transform, labels[i], BodySize, TextColor, TextAlignmentOptions.Left)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Button key = SmallButton(row.transform, Controls.KeyName(act), CardColor, 150);
            keyLabels[i] = key.GetComponentInChildren<TMP_Text>();
            TMP_Text label = keyLabels[i];
            key.onClick.AddListener(() =>
            {
                label.text = "Press a key...";
                controlsNote.text = "Press the new key, or Esc to keep the old one.";
                Controls.Rebind(act, RefreshControls);
            });
        }
        TMP_Text mouse = MakeText("Mouse", list,
            "Mouse: click to place or pick up, drag to move, right-click to cancel or remove. Click a lamp to switch it on or off; double-click a piece to edit it.",
            LabelSize, MutedText, TextAlignmentOptions.TopLeft);
        mouse.textWrappingMode = TextWrappingModes.Normal;
        mouse.enableAutoSizing = false;

        GameObject footer = Row("Footer", page, 40, 12, false);
        footer.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        SmallButton(footer.transform, "Reset to defaults", TabColor, 180).onClick.AddListener(() => { Controls.ResetAll(); RefreshControls(); });
        controlsNote = MakeText("Note", footer.transform, "", LabelSize, MutedText, TextAlignmentOptions.Left);
        controlsNote.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
    }

    private void RefreshControls()
    {
        for (int i = 0; i < keyLabels.Length; i++) keyLabels[i].text = Controls.KeyName((Controls.Act)i);
        controlsNote.text = "";
    }

    // Credits come from Resources/Credits.txt, so they can be edited without touching code.
    // "[CHECK]" marks lines still to be confirmed; it is hidden in the game.
    private static void BuildCredits(Transform page)
    {
        Heading(page, "Credits");
        Transform list = ScrollList(page, out _);
        StackChildren(list, 0);
        TextAsset file = Resources.Load<TextAsset>("Credits");
        TMP_Text text = MakeText("Text", list, file != null ? file.text.Replace(" [CHECK]", "") : "Credits file missing.",
            LabelSize, TextColor, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = false;
    }

    // ---------- widgets ----------

    // Lays a scroll list's children out top to bottom at their preferred heights.
    private static void StackChildren(Transform list, float spacing)
    {
        var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
    }

    private static GameObject Column(string name, Transform parent, float spacing)
    {
        GameObject column = Make(name, parent, typeof(VerticalLayoutGroup));
        var v = column.GetComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return column;
    }

    private static void Heading(Transform page, string text)
    {
        TMP_Text heading = MakeText("Heading", page, text, HeadingSize, TextColor, TextAlignmentOptions.Left);
        heading.fontStyle = FontStyles.Bold;
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
    }

    // Title and description on the left, an on/off switch on the right.
    private static void Switch(Transform page, string title, string description, Func<bool> get, Action<bool> set)
    {
        GameObject row = Row(title, page, 48, 12, false);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        TMP_Text label = MakeText("Label", row.transform,
            $"{title}\n<size={CaptionSize}><color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{description}</color></size>",
            BodySize, TextColor, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        GameObject track = Make("Switch", row.transform, typeof(Image), typeof(Button), typeof(LayoutElement));
        track.GetComponent<LayoutElement>().preferredWidth = 56;
        track.GetComponent<LayoutElement>().preferredHeight = 30;
        Image trackImage = track.GetComponent<Image>();
        trackImage.sprite = Circle;
        trackImage.type = Image.Type.Sliced;
        GameObject knob = Make("Knob", track.transform, typeof(Image));
        var knobRect = (RectTransform)knob.transform;
        knobRect.sizeDelta = new Vector2(24, 24);
        knob.GetComponent<Image>().sprite = Circle;
        knob.GetComponent<Image>().color = TextColor;
        knob.GetComponent<Image>().raycastTarget = false;

        void Show()
        {
            bool on = get();
            trackImage.color = on ? XpColor : CardColor;
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(on ? 1 : 0, 0.5f);
            knobRect.anchoredPosition = new Vector2(on ? -15 : 15, 0);
        }
        track.GetComponent<Button>().onClick.AddListener(() => { set(!get()); PlayerPrefs.Save(); Show(); });
        Show();
    }

    // Label and value on one line, a slider under it.
    private static Slider AddSlider(Transform page, string title, float min, float max, float value, Func<float, string> format,
        Action<float> changed, out TMP_Text valueText)
    {
        GameObject labels = Row(title, page, 26, 8, false);
        MakeText("Title", labels.transform, title, BodySize, TextColor, TextAlignmentOptions.Left).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        valueText = MakeText("Value", labels.transform, format(value), BodySize, AccentColor, TextAlignmentOptions.Right);
        valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;

        GameObject root = Make(title + "Slider", page, typeof(Slider), typeof(LayoutElement));
        root.GetComponent<LayoutElement>().preferredHeight = 28;
        GameObject track = Make("Track", root.transform, typeof(Image));
        Anchor(track, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
        Style(track.GetComponent<Image>(), CardColor);
        GameObject fillArea = Make("FillArea", root.transform);
        Anchor(fillArea, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
        GameObject fill = Make("Fill", fillArea.transform, typeof(Image));
        Anchor(fill, Vector2.zero, Vector2.one); // the Slider only drives x, so y must already span the track
        Style(fill.GetComponent<Image>(), AccentButtonColor);
        GameObject handleArea = Make("HandleArea", root.transform);
        Anchor(handleArea, Vector2.zero, Vector2.one);
        GameObject handle = Make("Handle", handleArea.transform, typeof(Image));
        Anchor(handle, Vector2.zero, Vector2.one);
        ((RectTransform)handle.transform).sizeDelta = new Vector2(24, 0);
        handle.GetComponent<Image>().sprite = Circle;
        handle.GetComponent<Image>().color = TextColor;

        var slider = root.GetComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.wholeNumbers = true;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);
        TMP_Text label = valueText; // out params can't be captured
        slider.onValueChanged.AddListener(v => { label.text = format(v); changed(v); PlayerPrefs.Save(); });
        return slider;
    }
}
