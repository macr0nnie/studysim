using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Top-left player card (level badge, XP bar, coins), a level-up toast, and a theme pass over the
// scene-built UI (iPod, timer buttons, room buttons) so everything shares the store's look.
public class GameHUD : MonoBehaviour
{
    private PlayerCurrency currency;
    private Experience experience;
    private TMP_Text levelBadge, levelLabel, xpLabel, coinsLabel, toastText;
    private RectTransform xpFill;
    private CanvasGroup toast;
    private TMP_Text debugText;
    private string lastDebug;
    private float debugShownAt;
    private Image editPill;
    private RoomManager room;
    private TimerManager timer;
    private TMP_Text timerText;
    private Transform hudCanvas;
    private AudioSource chime;
    private readonly System.Collections.Generic.Queue<string> toasts = new System.Collections.Generic.Queue<string>();
    private bool toastPlaying, wasEditing;
    static readonly Color BreakColor = new Color(0.55f, 0.85f, 0.65f, 1f);

    // The old texts this card replaces.
    static readonly string[] ReplacedObjects = { "money_text", "player_experience", "coinsText" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureHUD();
        EnsureHUD();
    }

    private static void EnsureHUD()
    {
        if (FindAnyObjectByType<RoomManager>() != null && FindAnyObjectByType<GameHUD>() == null)
            new GameObject("GameHUD").AddComponent<GameHUD>();
    }

    private void Awake()
    {
        currency = FindAnyObjectByType<PlayerCurrency>();
        experience = FindAnyObjectByType<Experience>();
        room = FindAnyObjectByType<RoomManager>();
        timer = FindAnyObjectByType<TimerManager>();
        BuildUI();
        if (PlayerPrefs.GetInt("SeenWelcome", 0) == 0)
        {
            PlayerPrefs.SetInt("SeenWelcome", 1);
            ShowToast($"Welcome! {Controls.KeyName(Controls.Act.Store)} opens the shop, {Controls.KeyName(Controls.Act.Edit)} lets you move furniture, {Controls.KeyName(Controls.Act.Planner)} is your planner.");
        }
        chime = MakeChime();
    }

    private IEnumerator Start()
    {
        // Each part guarded, so a problem in one can't stop the rest of the HUD from coming up.
        try
        {
            foreach (string name in ReplacedObjects)
            {
                GameObject old = GameObject.Find(name);
                if (old != null) old.SetActive(false);
            }
            GameObject oldClock = GameObject.Find("TimerText"); // the scene's own clock, replaced by the one in the timer bar
            if (oldClock != null) oldClock.SetActive(false);
            GameObject debug = GameObject.Find("Debugging_Text");
            if (debug != null) debugText = debug.GetComponent<TMP_Text>();
        }
        catch (System.Exception e) { Debug.LogException(e); }
        try { Refresh(); } catch (System.Exception e) { Debug.LogException(e); }
        yield return null; // the iPod fills its playlist in Start; theme it after that
        try { ThemeSceneUI(); } catch (System.Exception e) { Debug.LogException(e); }
    }

    private int shownSeconds = -1;
    private float nextClockPoll;

    private void ShowClock(float seconds)
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(Mathf.Max(0, seconds));
        if (s == shownSeconds) return;
        shownSeconds = s;
        timerText.text = $"{s / 60:00}:{s % 60:00}";
    }

    private void OnEnable()
    {
        if (currency != null) currency.OnCoinsChanged.AddListener(OnCoins);
        if (experience != null)
        {
            experience.OnExperienceChanged += Refresh;
            experience.PlayerLevelUp?.AddListener(ShowLevelUp);
        }
        if (timer != null) { timer.OnTimerComplete += OnTimerComplete; timer.OnTimerTick += ShowClock; }
    }

    private void OnDisable()
    {
        if (currency != null) currency.OnCoinsChanged.RemoveListener(OnCoins);
        if (experience != null)
        {
            experience.OnExperienceChanged -= Refresh;
            experience.PlayerLevelUp?.RemoveListener(ShowLevelUp);
        }
        if (timer != null) { timer.OnTimerComplete -= OnTimerComplete; timer.OnTimerTick -= ShowClock; }
    }

    // The Chrome extension status line only matters for a moment; fade it once it stops changing.
    private void Update()
    {
        if (timer != null) UpdateTimerBar();
        if (timer != null && Time.unscaledTime >= nextClockPoll) { nextClockPoll = Time.unscaledTime + 0.5f; ShowClock(timer.CurrentTime); } // backs up the tick event
        // Green countdown while on a break, so it's clear which phase is running.
        if (timerText != null && timer != null) timerText.color = timer.IsStudySession ? TextColor : XpColor;
        if (editPill != null && room != null)
        {
            editPill.color = room.IsEditMode ? AccentButtonColor : TabColor;
            if (room.IsEditMode != wasEditing) hintBar.SetActive(room.IsEditMode);
            wasEditing = room.IsEditMode;
        }
        if (fitPanel != null && room != null)
        {
            bool show = room.IsEditMode && room.SelectedPiece != null;
            if (fitPanel.activeSelf != show) fitPanel.SetActive(show);
            if (show)
            {
                fitName.text = room.SelectedName;
                lightsButton.gameObject.SetActive(room.SelectedHasLights);
                fitInfo.text = room.SelectedFits
                    ? $"Height +{room.SelectedLift:0.00}   Size {room.SelectedSize:0.00}x"
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(DangerColor)}>Overlaps something. Let go somewhere free, or {Controls.KeyName(Controls.Act.Undo)} to undo</color>";
            }
        }
        if (debugText == null) return;
        if (debugText.text != lastDebug)
        {
            lastDebug = debugText.text;
            debugShownAt = Time.unscaledTime;
        }
        float age = Time.unscaledTime - debugShownAt;
        debugText.alpha = age < 4 ? 1 : Mathf.Clamp01(1 - (age - 4));
    }

    // Edit mode, bottom centre (clear of the drawers on the left and the music card on the right): a key hint
    // bar, and above it the selected piece's panel to turn, remove, raise/lower and grow/shrink it.
    private GameObject fitPanel, hintBar;
    private Button lightsButton;
    private TMP_Text fitInfo, fitName;
    const float HintHeight = 40;

    // Each hint is also a button doing what its key does, so everything works with the mouse alone.
    private void BuildHintBar(Transform canvas)
    {
        hintBar = Make("EditHints", canvas, typeof(Image), typeof(HorizontalLayoutGroup));
        var rect = (RectTransform)hintBar.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0);
        rect.sizeDelta = new Vector2(960, HintHeight);
        rect.anchoredPosition = new Vector2(0, DockTop + 12); // above the dock
        Style(hintBar.GetComponent<Image>(), PanelColor);
        var h = hintBar.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(6, 6, 5, 5);
        h.spacing = 4;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;
        HintButton("Rotate / spin", Controls.Act.Rotate, () => room.RotateSelected());
        HintButton("Lower", Controls.Act.Lower, () => room.NudgeHeight(-0.1f));
        HintButton("Raise", Controls.Act.Raise, () => room.NudgeHeight(0.1f));
        HintButton("Shrink", Controls.Act.Shrink, () => room.Resize(1 / 1.1f));
        HintButton("Grow", Controls.Act.Grow, () => room.Resize(1.1f));
        HintButton("Remove", Controls.Act.Delete, () => room.DeleteSelected());
        HintButton("Undo", Controls.Act.Undo, () => room.Undo());
        HintButton("Done", Controls.Act.Edit, () => room.ToggleEditMode());
        HintButton("Settings", Controls.Act.Settings, () => FindAnyObjectByType<SettingsUI>()?.Toggle());
        hintBar.SetActive(false);
    }

    private void HintButton(string label, Controls.Act key, UnityEngine.Events.UnityAction onClick)
    {
        Button b = TextButton(label, hintBar.transform, label, TabColor, CaptionSize);
        TMP_Text text = b.GetComponentInChildren<TMP_Text>();
        text.enableAutoSizing = true;
        text.fontSizeMin = 10;
        text.fontSizeMax = CaptionSize;
        KeyHint(text, label, key); // "Rotate  R", kept current when keys are rebound
        b.onClick.AddListener(onClick);
    }

    private void BuildFitPanel(Transform canvas)
    {
        fitPanel = Make("FitPanel", canvas, typeof(Image), typeof(VerticalLayoutGroup));
        var rect = (RectTransform)fitPanel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0);
        rect.sizeDelta = new Vector2(520, 160);
        rect.anchoredPosition = new Vector2(0, DockTop + 12 + HintHeight + 8);
        Style(fitPanel.GetComponent<Image>(), PanelColor);
        var v = fitPanel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(12, 12, 8, 8);
        v.spacing = 6;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        GameObject header = Row("Header", fitPanel.transform, 34, 8, false);
        fitName = MakeText("Name", header.transform, "", BodySize, TextColor, TextAlignmentOptions.Left);
        fitName.fontStyle = FontStyles.Bold;
        fitName.overflowMode = TextOverflowModes.Ellipsis;
        fitName.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        lightsButton = SmallButton(header.transform, "Lights", TabColor, 90);
        lightsButton.onClick.AddListener(() => room.ToggleSelectedLights());
        Button rotate = SmallButton(header.transform, "Rotate", TabColor, 110);
        KeyHint(rotate.GetComponentInChildren<TMP_Text>(), "Rotate", Controls.Act.Rotate);
        rotate.onClick.AddListener(() => room.RotateSelected());
        Button remove = SmallButton(header.transform, "Remove", TabColor, 130);
        KeyHint(remove.GetComponentInChildren<TMP_Text>(), "Remove", Controls.Act.Delete);
        remove.onClick.AddListener(() => room.DeleteSelected());
        fitInfo = MakeText("Info", fitPanel.transform, "", LabelSize, MutedText, TextAlignmentOptions.Center);
        fitInfo.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
        FitRow("Height", Controls.Act.Raise, Controls.Act.Lower, () => room.NudgeHeight(0.1f), () => room.NudgeHeight(-0.1f), () => room.ResetHeight());
        FitRow("Size", Controls.Act.Grow, Controls.Act.Shrink, () => room.Resize(1.1f), () => room.Resize(1 / 1.1f), () => room.ResetSize());
        fitPanel.SetActive(false);
    }

    private void FitRow(string label, Controls.Act up, Controls.Act down, UnityEngine.Events.UnityAction more, UnityEngine.Events.UnityAction less, UnityEngine.Events.UnityAction reset)
    {
        GameObject row = Row(label, fitPanel.transform, 34, 8, false);
        TMP_Text name = MakeText("Name", row.transform, $"{label}  ({Controls.KeyName(down)} / {Controls.KeyName(up)})", LabelSize, TextColor, TextAlignmentOptions.Left);
        name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        SmallButton(row.transform, "−", TabColor, 44).onClick.AddListener(less);
        SmallButton(row.transform, "+", TabColor, 44).onClick.AddListener(more);
        SmallButton(row.transform, "Reset", TabColor, 70).onClick.AddListener(reset);
    }

    private void OnCoins(int coins) => coinsLabel.text = coins.ToString("N0");

    private void Refresh()
    {
        if (currency != null) OnCoins(currency.GetCoins());
        if (experience == null) return;
        int level = experience.GetPlayerLevel(), xp = experience.GetExperience(), next = experience.ExperienceToNextLevel;
        levelBadge.text = level.ToString();
        levelLabel.text = $"Level {level}";
        xpLabel.text = $"{xp} / {next} XP";
        xpFill.anchorMax = new Vector2(Mathf.Clamp01((float)xp / next), 1);
    }

    private void ShowLevelUp(int level)
    {
        if (GameSettings.LevelUpToasts) ShowToast($"Level up!  You reached level {level}");
    }

    private void OnTimerComplete()
    {
        // The timer has already flipped: now on a break means a study session just finished.
        if (GameSettings.SessionToasts)
        {
            string next = timer.IsTimerRunning ? "started" : "ready when you are";
            ShowToast(!timer.IsStudySession
                ? $"Session complete!  +{timer.MoneyReward} coins, +{timer.ExperienceReward} XP.  Break {next}"
                : $"Break's over.  Next session {next}");
        }
        if (chime != null && GameSettings.SessionChime)
        {
            chime.volume = GameSettings.EffectsVolume;
            chime.Play();
        }
    }

    // Toasts queue up so a level-up and a session reward don't overwrite each other.
    public void ShowToast(string message)
    {
        toasts.Enqueue(message);
        if (!toastPlaying) StartCoroutine(PlayToasts());
    }

    // Each one pops in under the timer, holds, then fades.
    private IEnumerator PlayToasts()
    {
        toastPlaying = true;
        var rect = (RectTransform)toast.transform;
        while (toasts.Count > 0)
        {
            toastText.text = toasts.Dequeue();
            for (float t = 0; t < 3f; t += Time.unscaledDeltaTime)
            {
                float pop = t < 0.25f ? 1 + Mathf.Sin(t / 0.25f * Mathf.PI) * 0.12f : 1;
                rect.localScale = Vector3.one * pop;
                toast.alpha = t < 0.15f ? t / 0.15f : t > 2.4f ? (3f - t) / 0.6f : 1;
                yield return null;
            }
            toast.alpha = 0;
        }
        toastPlaying = false;
    }

    private void BuildUI()
    {
        Transform canvas = hudCanvas = MakeCanvas("HUDCanvas", transform, 9).transform;

        // Player card, top left: where the eye starts, and clear of the timer and the music player.
        GameObject card = Make("PlayerCard", canvas, typeof(Image), typeof(Shadow));
        var cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(0, 1);
        cardRect.sizeDelta = new Vector2(380, 88);
        cardRect.anchoredPosition = new Vector2(24, -24);
        Style(card.GetComponent<Image>(), PanelColor);
        card.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.35f);
        card.GetComponent<Shadow>().effectDistance = new Vector2(0, -4);

        GameObject badge = Make("LevelBadge", card.transform, typeof(Image));
        var badgeRect = (RectTransform)badge.transform;
        badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(0, 0.5f);
        badgeRect.sizeDelta = new Vector2(64, 64);
        badgeRect.anchoredPosition = new Vector2(12, 0);
        var badgeImage = badge.GetComponent<Image>();
        badgeImage.sprite = Circle;
        badgeImage.color = AccentButtonColor;
        levelBadge = MakeText("Level", badge.transform, "1", 34, TextColor, TextAlignmentOptions.Center, display: true);
        Stretch((RectTransform)levelBadge.transform);

        levelLabel = MakeText("LevelLabel", card.transform, "Level 1", HeadingSize, TextColor, TextAlignmentOptions.Left);
        levelLabel.fontStyle = FontStyles.Bold;
        Place(levelLabel.rectTransform, new Vector2(88, -6), new Vector2(150, 36)); // Zain has tall line height; a short box hides the text
        xpLabel = MakeText("XP", card.transform, "", CaptionSize, MutedText, TextAlignmentOptions.Right);
        Place(xpLabel.rectTransform, new Vector2(240, -14), new Vector2(124, 24));

        GameObject bar = Make("XPBar", card.transform, typeof(Image));
        Place((RectTransform)bar.transform, new Vector2(88, -46), new Vector2(276, 12));
        Style(bar.GetComponent<Image>(), CardColor);
        GameObject fill = Make("Fill", bar.transform, typeof(Image));
        xpFill = (RectTransform)fill.transform;
        Stretch(xpFill);
        Style(fill.GetComponent<Image>(), XpColor);

        GameObject coin = Make("CoinIcon", card.transform, typeof(Image));
        Place((RectTransform)coin.transform, new Vector2(88, -64), new Vector2(16, 16));
        coin.GetComponent<Image>().sprite = UIIcons.Coin ?? Circle;
        coin.GetComponent<Image>().color = AccentColor;
        coinsLabel = MakeText("Coins", card.transform, "0", HeadingSize, AccentColor, TextAlignmentOptions.Left, display: true);
        Place(coinsLabel.rectTransform, new Vector2(110, -60), new Vector2(200, 24));

        // Edit-mode toggle, first in the dock (Shop, Paint, Planner and Settings follow it).
        Button edit = NavButton(canvas, 0, "Edit", Controls.Act.Edit); // Edit toggles edit mode; Cancel only leaves it
        editPill = edit.GetComponent<Image>();
        edit.onClick.AddListener(() => { if (room != null) room.ToggleEditMode(); });

        if (timer != null) BuildTimerBar(canvas);
        BuildHintBar(canvas);
        BuildFitPanel(canvas);

        // Level-up toast, centred under the clock.
        GameObject toastGO = Make("LevelUpToast", canvas, typeof(Image), typeof(CanvasGroup));
        var toastRect = (RectTransform)toastGO.transform;
        toastRect.anchorMin = toastRect.anchorMax = toastRect.pivot = new Vector2(0.5f, 1);
        toastRect.sizeDelta = new Vector2(660, 56);
        toastRect.anchoredPosition = new Vector2(0, -124); // under the clock
        Style(toastGO.GetComponent<Image>(), AccentButtonColor);
        toast = toastGO.GetComponent<CanvasGroup>();
        toast.alpha = 0;
        toast.blocksRaycasts = false;
        toastText = MakeText("Text", toastGO.transform, "", HeadingSize, TextColor, TextAlignmentOptions.Center);
        toastText.fontStyle = FontStyles.Bold;
        // Long hints (edit mode, session complete) were clipped at 24pt, so shrink them to fit.
        toastText.enableAutoSizing = true;
        toastText.fontSizeMin = 14;
        toastText.fontSizeMax = 24;
        Stretch((RectTransform)toastText.transform);
        toastText.margin = new Vector4(16, 0, 16, 0);
    }

    // Soft two-note chime made in code, so there's an audible cue without adding an audio asset.
    private AudioSource MakeChime()
    {
        const int rate = 44100;
        var samples = new float[(int)(rate * 0.9f)];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float note = t < 0.3f ? 659.25f : 987.77f; // E5 then B5
            float local = t < 0.3f ? t : t - 0.3f;
            samples[i] = Mathf.Sin(2 * Mathf.PI * note * t) * Mathf.Exp(-local * 6f) * 0.25f;
        }
        var clip = AudioClip.Create("TimerChime", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 0;
        return source;
    }

    private static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
    }

    // ---------- timer controls ----------

    private Image timerPlayGlyph, timerPauseLeft, timerPauseRight;
    private Button timerMinus, timerPlus;
    private TMP_Text timerPhase;

    // Under the countdown: -5 | play/pause | stop | +5, and which phase is running. Lengths only
    // change while the timer is stopped, so -5/+5 grey out while it runs.
    private void BuildTimerBar(Transform canvas)
    {
        GameObject bar = Make("TimerControls", canvas, typeof(Image), typeof(HorizontalLayoutGroup));
        var rect = (RectTransform)bar.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1); // top right, clear of the clock text
        rect.sizeDelta = new Vector2(330, 96);
        rect.anchoredPosition = new Vector2(-24, -8);
        Style(bar.GetComponent<Image>(), PanelColor);
        var layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 46, 6); // the countdown takes the top strip
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;

        // The countdown: always built with the buttons, never read from the scene.
        timerText = MakeText("Countdown", bar.transform, "00:00", 38, TextColor, TextAlignmentOptions.Center, display: true);
        timerText.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var clock = (RectTransform)timerText.transform;
        clock.anchorMin = new Vector2(0, 1);
        clock.anchorMax = new Vector2(1, 1);
        clock.pivot = new Vector2(0.5f, 1);
        clock.sizeDelta = new Vector2(0, 42);
        clock.anchoredPosition = new Vector2(0, -4);
        ShowClock(timer.CurrentTime);

        timerMinus = TimerButton(bar.transform, "Minus", 56, 36, TabColor, timer.RemoveFiveMinutes);
        Stretch((RectTransform)MakeText("Label", timerMinus.transform, "-5", BodySize, TextColor, TextAlignmentOptions.Center).transform);

        Button play = TimerButton(bar.transform, "PlayPause", 40, 40, AccentButtonColor, () =>
        {
            if (timer.IsTimerRunning) timer.PauseTimer();
            else timer.StartTimer();
        });
        play.GetComponent<Image>().sprite = Circle;
        timerPlayGlyph = Glyph(play.transform, Triangle, new Vector2(16, 16), new Vector2(2, 0));
        timerPauseLeft = Glyph(play.transform, null, new Vector2(4, 15), new Vector2(-4, 0));
        timerPauseRight = Glyph(play.transform, null, new Vector2(4, 15), new Vector2(4, 0));

        Button stop = TimerButton(bar.transform, "Stop", 40, 40, TabColor, timer.ResetTimer);
        stop.GetComponent<Image>().sprite = Circle;
        Glyph(stop.transform, Rounded, new Vector2(13, 13), Vector2.zero);

        timerPlus = TimerButton(bar.transform, "Plus", 56, 36, TabColor, timer.AddFiveMinutes);
        Stretch((RectTransform)MakeText("Label", timerPlus.transform, "+5", BodySize, TextColor, TextAlignmentOptions.Center).transform);

        timerPhase = MakeText("Phase", bar.transform, "Focus", CaptionSize, MutedText, TextAlignmentOptions.Center);
        ((RectTransform)timerPhase.transform).sizeDelta = new Vector2(52, 36);
    }

    private static Button TimerButton(Transform parent, string name, float width, float height, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = Make(name, parent, typeof(Image), typeof(Button));
        ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
        Style(go.GetComponent<Image>(), color);
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);
        return button;
    }

    private static Image Glyph(Transform parent, Sprite sprite, Vector2 size, Vector2 offset)
    {
        GameObject go = Make("Glyph", parent, typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = TextColor;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateTimerBar()
    {
        if (timerPlayGlyph == null) return;
        bool running = timer.IsTimerRunning;
        timerPlayGlyph.enabled = !running;
        timerPauseLeft.enabled = timerPauseRight.enabled = running;
        timerMinus.interactable = timerPlus.interactable = !running;
        timerPhase.text = timer.IsStudySession ? "Focus" : "Break";
    }

    // ---------- theme pass over the scene's own UI ----------

    // Only plain shapes get recoloured; album covers and other artwork keep their colours.
    static readonly string[] ShapeSprites = { "UISprite", "Background", "Knob", "InputFieldBackground", "Rectangle", "Ipod", "Group" };

    private void ThemeSceneUI()
    {
        // Scene canvases scale the same way as the built ones, so nothing slides off on other aspect ratios.
        foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var ipod = FindAnyObjectByType<iPodUIController>();
        if (ipod != null) ThemeTree(ipod.transform.root);

        // The scene's timer buttons are replaced by the control bar built in BuildTimerBar.
        GameObject timerButtons = GameObject.Find("Button_Panel");
        if (timerButtons != null) timerButtons.SetActive(false);

        // The old room buttons lost their icons (the sheet was re-sliced) and none of them is wired up;
        // the Edit pill built in BuildUI replaces them.
        foreach (string name in new[] { "Wall_ColorChanger", "Camera_ColorChanger", "EditMode", "Icon (3)" })
        {
            GameObject go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }

    }

    public static void Theme(Transform root) => ThemeTree(root);

    private static void ThemeTree(Transform root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Mask>() != null) continue; // masks need their sprite's alpha
            if (image.name.Contains("Cover")) continue; // album art keeps its colours
            // Song-list rows: keep the cover art as is, theme only the row background and its buttons.
            if (image.GetComponentInParent<PlaylistItemUI>(true) is PlaylistItemUI row && row.gameObject != image.gameObject
                && image.name != "Play" && image.name != "BuyButton") continue;
            Sprite white = UIIcons.Light(image.sprite);
            if (white != null) { image.sprite = white; image.color = TextColor; continue; } // dark line icons can't be tinted light
            if (!IsShape(image.sprite)) continue;
            float depth = Depth(image.transform, root);
            bool interactive = image.GetComponent<Selectable>() != null || image.name == "Handle" || image.name == "Fill";
            image.color = image.name == "Fill" ? AccentColor
                : interactive ? TabColor
                : depth <= 2 ? PanelColor
                : CardColor;
            if (image.type == Image.Type.Simple && image.sprite != null && image.sprite.name != "Ipod")
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }
        }
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            text.color = TextColor;
    }

    private static bool IsShape(Sprite sprite) => sprite == null || StartsWithAny(sprite.name, ShapeSprites);

    private static bool StartsWithAny(string value, string[] prefixes)
    {
        foreach (string p in prefixes)
            if (value.StartsWith(p)) return true;
        return false;
    }

    private static int Depth(Transform t, Transform root)
    {
        int d = 0;
        for (; t != null && t != root; t = t.parent) d++;
        return d;
    }

}
