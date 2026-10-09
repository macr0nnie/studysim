using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shared look and building blocks for the game's code-built UI (store, HUD, planner), so every panel
// uses the same palette, fonts, rounded corners and motion.
public static class UIKit
{
    // Menu colours are a theme: every shade below comes from one menu colour the player can pick
    // (Paint > Menus), saved under MenuColor. Text, gold accent, XP green and danger red stay fixed.
    public static Color PanelColor, CardColor, SelectedColor, TabColor, MutedText, AccentButtonColor;
    public static readonly Color TextColor = new Color(0.96f, 0.93f, 0.88f, 1f);
    public static readonly Color AccentColor = new Color(1f, 0.82f, 0.40f, 1f);
    public static readonly Color XpColor = new Color(0.55f, 0.85f, 0.65f, 1f);
    public static readonly Color DangerColor = new Color(0.85f, 0.42f, 0.48f, 1f);

    public static readonly Color DefaultMenuColor = new Color(0.45f, 0.34f, 0.60f, 1f); // the original purple
    private const string MenuColorKey = "MenuColor";
    public static bool MenuColorChanged => PlayerPrefs.HasKey(MenuColorKey);

    static UIKit() => LoadSavedTheme();

    // Also called before a save slot reloads the room, since that slot may carry another menu colour.
    public static void LoadSavedTheme()
    {
        Color menu = DefaultMenuColor;
        if (PlayerPrefs.HasKey(MenuColorKey) && !ColorUtility.TryParseHtmlString("#" + PlayerPrefs.GetString(MenuColorKey), out menu))
            menu = DefaultMenuColor;
        SetPalette(menu);
    }

    // Shades keep the purple theme's saturation and brightness steps, on the picked colour's hue.
    private static void SetPalette(Color menu)
    {
        Color.RGBToHSV(menu, out float h, out float s, out _);
        float k = s / 0.43f; // the default purple's saturation
        Color Shade(float sat, float value, float alpha = 1f)
        {
            Color c = Color.HSVToRGB(h, Mathf.Clamp01(sat * k), value);
            c.a = alpha;
            return c;
        }
        PanelColor = Shade(0.45f, 0.22f, 0.96f);
        CardColor = Shade(0.41f, 0.34f);
        SelectedColor = Shade(0.43f, 0.60f);
        TabColor = Shade(0.42f, 0.52f);
        AccentButtonColor = Shade(0.46f, 0.78f);
        MutedText = Shade(0.14f, 0.84f);
    }

    private static Color[] Palette() => new[] { PanelColor, CardColor, SelectedColor, TabColor, AccentButtonColor, MutedText };

    // New menu colour (null = default purple): recolours everything already built, which was coloured
    // from the old palette; panels built later read the new one.
    public static void ApplyMenuColor(Color? menu)
    {
        Color[] before = Palette();
        SetPalette(menu ?? DefaultMenuColor);
        if (menu.HasValue) PlayerPrefs.SetString(MenuColorKey, ColorUtility.ToHtmlStringRGB(menu.Value));
        else PlayerPrefs.DeleteKey(MenuColorKey);
        PlayerPrefs.Save();
        Color[] after = Palette();
        foreach (Graphic g in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (int i = 0; i < before.Length; i++)
            {
                Color c = g.color;
                if (Mathf.Abs(c.r - before[i].r) + Mathf.Abs(c.g - before[i].g) + Mathf.Abs(c.b - before[i].b) > 0.01f) continue;
                g.color = new Color(after[i].r, after[i].g, after[i].b, c.a);
                break;
            }
    }

    // Left-side drawer slot shared by the store and planner: below the HUD card, above the room buttons.
    public const float DrawerWidth = 440, DrawerBottom = 410, DrawerTop = 130;
    // Type scale: every panel picks from these instead of one-off sizes.
    public const float TitleSize = 30, HeadingSize = 23, BodySize = 19, LabelSize = 16, CaptionSize = 14;

    // Room-button row (bottom left, under the drawers): one shared slot per button so they line up.
    const float NavWidth = 96, NavGap = 6, NavY = 334;

    public static Button NavButton(Transform canvas, int slot, string label, string key)
    {
        string text = key == null ? label : $"{label}  <size={CaptionSize}><alpha=#99>{key}</size>";
        Button button = TextButton(label + "Button", canvas, text, TabColor, LabelSize + 1);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(NavWidth, 40);
        rect.anchoredPosition = new Vector2(24 + slot * (NavWidth + NavGap), NavY);
        return button;
    }

    public static readonly Vector2 DrawerOffset = new Vector2(24, (DrawerBottom - DrawerTop) / 2);

    private static Sprite rounded, circle;
    private static TMP_FontAsset bodyFont, displayFont;
    private static GameObject openDrawer;

    // 9-sliced rounded rectangle drawn once, so panels get soft corners without an art asset.
    public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 16);
    public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32);

    public static TMP_FontAsset BodyFont => bodyFont != null ? bodyFont : bodyFont = Resources.Load<TMP_FontAsset>("Fonts/Zain-Regular SDF");
    public static TMP_FontAsset DisplayFont => displayFont != null ? displayFont : displayFont = Resources.Load<TMP_FontAsset>("Fonts/Jersey15-Regular SDF");

    private static Sprite MakeRounded(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // distance outside the inner rectangle, anti-aliased over one pixel
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0);
                float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        float border = Mathf.Min(radius, size / 2 - 1);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // Overlay canvas scaled like the game's other canvases (1920x1080, match width) so positions line up.
    public static Canvas MakeCanvas(string name, Transform parent, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // Expand keeps the whole 1920x1080 layout on screen at any aspect: ultrawide gets extra width,
        // 16:10 and 4:3 get extra height, and nothing scales off the edge.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        return canvas;
    }

    // A left drawer panel with the standard look; starts hidden.
    public static GameObject MakeDrawer(string name, Transform canvas)
    {
        GameObject panel = Make(name, canvas, typeof(Image), typeof(VerticalLayoutGroup), typeof(CanvasGroup), typeof(Shadow));
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 0.5f);
        rect.sizeDelta = new Vector2(DrawerWidth, -(DrawerBottom + DrawerTop));
        rect.anchoredPosition = DrawerOffset;
        Style(panel.GetComponent<Image>(), PanelColor);
        var shadow = panel.GetComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.35f);
        shadow.effectDistance = new Vector2(0, -6);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 10;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.SetActive(false);
        return panel;
    }

    // Opens or closes a drawer; only one drawer is open at a time. Returns true if it is now open.
    public static bool ToggleDrawer(MonoBehaviour host, GameObject panel)
    {
        bool open = !panel.activeSelf;
        if (open && openDrawer != null && openDrawer != panel) openDrawer.SetActive(false);
        panel.SetActive(open);
        openDrawer = open ? panel : null;
        if (open) host.StartCoroutine(SlideIn((RectTransform)panel.transform, panel.GetComponent<CanvasGroup>()));
        return open;
    }

    // Short slide + fade from the left; unscaled so it still plays if the game is paused.
    private static IEnumerator SlideIn(RectTransform rect, CanvasGroup group)
    {
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.18f)
        {
            float e = 1 - (1 - t) * (1 - t) * (1 - t); // ease-out cubic
            rect.anchoredPosition = Vector2.LerpUnclamped(DrawerOffset + new Vector2(-60, 0), DrawerOffset, e);
            group.alpha = e;
            yield return null;
        }
        rect.anchoredPosition = DrawerOffset;
        group.alpha = 1;
    }

    // True while the player is typing in a text field, so single-key shortcuts stay quiet.
    public static bool Typing()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }

    public static GameObject Make(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (Type c in components) go.AddComponent(c);
        return go;
    }

    public static GameObject Row(string name, Transform parent, float height, float spacing, bool expand)
    {
        GameObject row = Make(name, parent, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        var element = row.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        element.flexibleHeight = 0; // otherwise the layout group reports flexible height and steals space from lists
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = expand;
        return row;
    }

    public static void Style(Image image, Color color)
    {
        image.color = color;
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
    }

    public static TMP_Text MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool display = false)
    {
        var tmp = Make(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = display ? DisplayFont : BodyFont;
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        // TMP hides a line taller than its box (panel titles, music card rows), so shrink to fit instead.
        tmp.fontSizeMax = size;
        tmp.fontSizeMin = Mathf.Min(size, 8);
        tmp.enableAutoSizing = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Button TextButton(string name, Transform parent, string label, Color color, float fontSize)
    {
        GameObject go = Make(name, parent, typeof(Image), typeof(Button));
        Style(go.GetComponent<Image>(), color);
        Stretch((RectTransform)MakeText("Label", go.transform, label, fontSize, TextColor, TextAlignmentOptions.Center).transform);
        return go.GetComponent<Button>();
    }

    public static Button SmallButton(Transform parent, string label, Color color, float width)
    {
        Button b = TextButton(label, parent, label, color, LabelSize);
        var element = b.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.flexibleWidth = 0;
        return b;
    }

    // Header row with a title and a close button.
    public static TMP_Text Header(Transform panel, string title, Action onClose)
    {
        GameObject header = Row("Header", panel, 40, 10, false);
        TMP_Text text = MakeText("Title", header.transform, title, TitleSize, TextColor, TextAlignmentOptions.Left);
        text.fontStyle = FontStyles.Bold;
        text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        Button close = TextButton("Close", header.transform, "X", TabColor, HeadingSize);
        close.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
        close.onClick.AddListener(() => onClose());
        return text;
    }

    // Vertical scroll list with a thin auto-hiding scrollbar; returns the content to add rows to.
    public static Transform ScrollList(Transform parent, out ScrollRect scrollRect)
    {
        GameObject scroll = Make("Scroll", parent, typeof(ScrollRect), typeof(LayoutElement));
        scroll.GetComponent<LayoutElement>().flexibleHeight = 1;
        GameObject viewport = Make("Viewport", scroll.transform, typeof(RectMask2D));
        Stretch((RectTransform)viewport.transform);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-16, 0); // room for the scrollbar
        GameObject content = Make("Content", viewport.transform, typeof(ContentSizeFitter));
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = Vector2.zero;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewport.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30;

        GameObject bar = Make("Scrollbar", scroll.transform, typeof(Image), typeof(Scrollbar));
        Anchor(bar, new Vector2(1, 0), new Vector2(1, 1));
        ((RectTransform)bar.transform).offsetMin = new Vector2(-10, 0);
        Style(bar.GetComponent<Image>(), CardColor);
        GameObject slidingArea = Make("Sliding Area", bar.transform);
        Stretch((RectTransform)slidingArea.transform);
        GameObject handle = Make("Handle", slidingArea.transform, typeof(Image));
        Stretch((RectTransform)handle.transform);
        Style(handle.GetComponent<Image>(), TabColor);
        var scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = (RectTransform)handle.transform;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content.transform;
    }

    // Single-line text field with a placeholder.
    public static TMP_InputField MakeInput(Transform parent, string placeholder)
    {
        GameObject go = Make("Input", parent, typeof(Image), typeof(TMP_InputField), typeof(LayoutElement));
        Style(go.GetComponent<Image>(), CardColor);
        go.GetComponent<LayoutElement>().flexibleWidth = 1;
        GameObject area = Make("Text Area", go.transform, typeof(RectMask2D));
        Stretch((RectTransform)area.transform);
        ((RectTransform)area.transform).offsetMin = new Vector2(12, 4);
        ((RectTransform)area.transform).offsetMax = new Vector2(-12, -4);
        TMP_Text hint = MakeText("Placeholder", area.transform, placeholder, BodySize, MutedText, TextAlignmentOptions.Left);
        hint.fontStyle = FontStyles.Italic;
        TMP_Text text = MakeText("Text", area.transform, "", BodySize, TextColor, TextAlignmentOptions.Left);
        Stretch((RectTransform)hint.transform);
        Stretch((RectTransform)text.transform);
        var field = go.GetComponent<TMP_InputField>();
        field.textViewport = (RectTransform)area.transform;
        field.textComponent = text;
        field.placeholder = hint;
        if (BodyFont != null) field.fontAsset = BodyFont;
        field.pointSize = 18;
        field.characterLimit = 60;
        return field;
    }

    public static void Anchor(GameObject go, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
