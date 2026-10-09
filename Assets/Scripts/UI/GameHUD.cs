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
    private Image cover, coverNote, editPill;
    private Sprite coverPlaceholder;
    private RoomManager room;

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
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<GameHUD>() == null)
            new GameObject("GameHUD").AddComponent<GameHUD>();
    }

    private void Awake()
    {
        currency = FindFirstObjectByType<PlayerCurrency>();
        experience = FindFirstObjectByType<Experience>();
        room = FindFirstObjectByType<RoomManager>();
        BuildUI();
    }

    private IEnumerator Start()
    {
        foreach (string name in ReplacedObjects)
        {
            GameObject old = GameObject.Find(name);
            if (old != null) old.SetActive(false);
        }
        GameObject debug = GameObject.Find("Debugging_Text");
        if (debug != null) debugText = debug.GetComponent<TMP_Text>();

        Refresh();
        yield return null; // the iPod fills its playlist in Start; theme it after that
        ThemeSceneUI();
    }

    private void OnEnable()
    {
        if (currency != null) currency.OnCoinsChanged.AddListener(OnCoins);
        if (experience != null)
        {
            experience.OnExperienceChanged += Refresh;
            experience.PlayerLevelUp?.AddListener(ShowLevelUp);
        }
    }

    private void OnDisable()
    {
        if (currency != null) currency.OnCoinsChanged.RemoveListener(OnCoins);
        if (experience != null)
        {
            experience.OnExperienceChanged -= Refresh;
            experience.PlayerLevelUp?.RemoveListener(ShowLevelUp);
        }
    }

    // The Chrome extension status line only matters for a moment; fade it once it stops changing.
    private void Update()
    {
        if (editPill != null && room != null) editPill.color = room.IsEditMode ? AccentButtonColor : TabColor;
        if (cover != null)
        {
            bool hasArt = cover.sprite != coverPlaceholder;
            cover.color = hasArt ? Color.white : CardColor;
            coverNote.enabled = !hasArt;
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
        toastText.text = $"Level up!  You reached level {level}";
        StopCoroutine(nameof(Toast));
        StartCoroutine(nameof(Toast));
    }

    // Pops in under the timer, holds, then fades.
    private IEnumerator Toast()
    {
        var rect = (RectTransform)toast.transform;
        for (float t = 0; t < 3f; t += Time.unscaledDeltaTime)
        {
            float pop = t < 0.25f ? 1 + Mathf.Sin(t / 0.25f * Mathf.PI) * 0.12f : 1;
            rect.localScale = Vector3.one * pop;
            toast.alpha = t < 0.15f ? t / 0.15f : t > 2.4f ? (3f - t) / 0.6f : 1;
            yield return null;
        }
        toast.alpha = 0;
    }

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("HUDCanvas", transform, 9).transform;

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

        levelLabel = MakeText("LevelLabel", card.transform, "Level 1", 22, TextColor, TextAlignmentOptions.Left);
        levelLabel.fontStyle = FontStyles.Bold;
        Place(levelLabel.rectTransform, new Vector2(88, -6), new Vector2(150, 36)); // Zain has tall line height; a short box hides the text
        xpLabel = MakeText("XP", card.transform, "", 15, MutedText, TextAlignmentOptions.Right);
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
        coin.GetComponent<Image>().sprite = UIIcons.Named("Icons_21") ?? Circle; // the $ coin from the icon sheet
        coin.GetComponent<Image>().color = AccentColor;
        coinsLabel = MakeText("Coins", card.transform, "0", 22, AccentColor, TextAlignmentOptions.Left, display: true);
        Place(coinsLabel.rectTransform, new Vector2(110, -60), new Vector2(200, 24));

        // Edit-mode toggle, first in the room-button row (Shop and Planner follow it).
        Button edit = TextButton("EditButton", canvas, "Edit  (Esc)", TabColor, 20);
        var editRect = (RectTransform)edit.transform;
        editRect.anchorMin = editRect.anchorMax = editRect.pivot = new Vector2(0, 0);
        editRect.sizeDelta = new Vector2(130, 42);
        editRect.anchoredPosition = new Vector2(24, 334);
        editPill = edit.GetComponent<Image>();
        edit.onClick.AddListener(() => { if (room != null) room.ToggleEditMode(); });

        // Level-up toast, centred under the timer.
        GameObject toastGO = Make("LevelUpToast", canvas, typeof(Image), typeof(CanvasGroup));
        var toastRect = (RectTransform)toastGO.transform;
        toastRect.anchorMin = toastRect.anchorMax = toastRect.pivot = new Vector2(0.5f, 1);
        toastRect.sizeDelta = new Vector2(460, 56);
        toastRect.anchoredPosition = new Vector2(0, -130);
        Style(toastGO.GetComponent<Image>(), AccentButtonColor);
        toast = toastGO.GetComponent<CanvasGroup>();
        toast.alpha = 0;
        toast.blocksRaycasts = false;
        toastText = MakeText("Text", toastGO.transform, "", 24, TextColor, TextAlignmentOptions.Center);
        toastText.fontStyle = FontStyles.Bold;
        Stretch((RectTransform)toastText.transform);
    }

    private static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
    }

    // ---------- theme pass over the scene's own UI ----------

    // Only plain shapes get recoloured; album covers and other artwork keep their colours.
    static readonly string[] ShapeSprites = { "UISprite", "Background", "Knob", "InputFieldBackground", "Rectangle", "Ipod", "Group" };

    private void ThemeSceneUI()
    {
        var ipod = FindFirstObjectByType<iPodUIController>();
        if (ipod != null) ThemeTree(ipod.transform.root);

        GameObject timerButtons = GameObject.Find("Button_Panel");
        if (timerButtons != null)
        {
            ThemeTree(timerButtons.transform);
            Label(timerButtons.transform, "MinusButton", "-5");
            Label(timerButtons.transform, "Plus_Button", "+5");
        }

        // The old room buttons lost their icons (the sheet was re-sliced) and none of them is wired up;
        // the Edit pill built in BuildUI replaces them.
        foreach (string name in new[] { "Wall_ColorChanger", "Camera_ColorChanger", "EditMode", "Icon (3)" })
        {
            GameObject go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }

        if (ipod != null) LayoutNowPlaying(ipod.transform);
    }

    // Now-playing screen: give the title and artist their own space above the controls,
    // and show a note on the empty art box until a song with cover art plays.
    private void LayoutNowPlaying(Transform ipodRoot)
    {
        foreach (RectTransform rect in ipodRoot.GetComponentsInChildren<RectTransform>(true))
        {
            switch (rect.name)
            {
                case "Cover_Image":
                    rect.anchoredPosition = new Vector2(0, 140);
                    rect.sizeDelta = new Vector2(220, 220);
                    cover = rect.GetComponent<Image>();
                    coverPlaceholder = cover.sprite;
                    cover.color = CardColor;
                    GameObject note = Make("Note", rect, typeof(Image));
                    Anchor(note, new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.65f));
                    coverNote = note.GetComponent<Image>();
                    coverNote.sprite = UIIcons.Named("Icons_5");
                    coverNote.preserveAspect = true;
                    coverNote.color = MutedText;
                    coverNote.raycastTarget = false;
                    break;
                case "CurrentSong":
                    rect.anchoredPosition = new Vector2(0, 8);
                    if (rect.TryGetComponent(out TMP_Text song)) song.fontSize = 24;
                    break;
                case "Artist_Text":
                    rect.anchoredPosition = new Vector2(0, -20);
                    if (rect.TryGetComponent(out TMP_Text artist)) { artist.fontSize = 18; artist.color = MutedText; }
                    break;
                case "Slider":
                    if (rect.parent == ipodRoot || rect.parent.name == "IPOD") rect.anchoredPosition = new Vector2(0, -165);
                    break;
            }
        }
    }

    private static void ThemeTree(Transform root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.GetComponent<Mask>() != null) continue; // masks need their sprite's alpha
            if (image.name.Contains("Cover")) continue; // album art, coloured by LayoutNowPlaying
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

    // Gives a plain colour-square button a readable label.
    private static void Label(Transform root, string buttonName, string label)
    {
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            if (b.name != buttonName || b.GetComponentInChildren<TMP_Text>() != null) continue;
            Stretch((RectTransform)MakeText("Label", b.transform, label, 22, TextColor, TextAlignmentOptions.Center).transform);
        }
    }
}
