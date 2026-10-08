using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The furniture store. Builds its own screen-space UI from a FurnitureCatalog at startup, so it only needs
// a catalog assigned: a "Shop" button opens a right-docked panel with category tabs, a grid of icon cards,
// and a details area (description, tags, price) with a Buy button. Press B to toggle it.
public class FurnitureStoreUI : MonoBehaviour
{
    [SerializeField] private FurnitureCatalog catalog;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private PlayerCurrency playerCurrency;
    [SerializeField] private Sprite roundedSprite; // optional 9-sliced background for panels and cards
    [SerializeField] private KeyCode toggleKey = KeyCode.B;

    static readonly Color PanelColor = new Color(0.16f, 0.12f, 0.22f, 0.96f);
    static readonly Color CardColor = new Color(0.26f, 0.20f, 0.34f, 1f);
    static readonly Color SelectedColor = new Color(0.45f, 0.34f, 0.60f, 1f);
    static readonly Color TabColor = new Color(0.40f, 0.30f, 0.52f, 1f);
    static readonly Color TextColor = new Color(0.96f, 0.93f, 0.88f, 1f);
    static readonly Color MutedText = new Color(0.78f, 0.72f, 0.84f, 1f);
    static readonly Color AccentColor = new Color(1f, 0.82f, 0.40f, 1f);

    private class Card
    {
        public FurnitureItem item;
        public GameObject root;
        public Image background;
    }

    private readonly List<Card> cards = new List<Card>();
    private GameObject panel;
    private TMP_Text coinsText, detailName, detailDescription, detailTags, buyLabel;
    private Button buyButton;
    private Card selected;
    private int shownCategory = -1;

    private void Awake()
    {
        if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
        if (playerCurrency == null) playerCurrency = FindFirstObjectByType<PlayerCurrency>();
        if (catalog == null)
        {
            Debug.LogError("FurnitureStoreUI: no catalog assigned.", this);
            enabled = false;
            return;
        }
        BuildUI();
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerCurrency != null) playerCurrency.OnCoinsChanged.AddListener(Refresh);
    }

    private void OnDisable()
    {
        if (playerCurrency != null) playerCurrency.OnCoinsChanged.RemoveListener(Refresh);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Toggle();
    }

    public void Toggle()
    {
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) Refresh(Coins);
    }

    // Values match StoreCategory; -1 shows everything.
    public void ShowCategory(int category)
    {
        shownCategory = category;
        foreach (Card c in cards)
            c.root.SetActive(category < 0 || (int)c.item.category == category);
    }

    private int Coins => playerCurrency != null ? playerCurrency.GetCoins() : 0;

    private void Select(Card card)
    {
        if (selected != null) selected.background.color = CardColor;
        selected = card;
        card.background.color = SelectedColor;
        detailName.text = card.item.displayName;
        detailDescription.text = card.item.description;
        detailTags.text = card.item.tags.Length > 0 ? "#" + string.Join("  #", card.item.tags) : "";
        Refresh(Coins);
    }

    private void Refresh(int coins)
    {
        coinsText.text = coins.ToString();
        bool canBuy = selected != null && coins >= selected.item.price;
        buyButton.interactable = canBuy;
        buyLabel.text = selected == null ? "Pick an item"
            : canBuy ? $"Buy for {selected.item.price}"
            : $"Need {selected.item.price}";
    }

    private void Buy()
    {
        if (selected == null || roomManager == null || playerCurrency == null) return;
        FurnitureItem item = selected.item;
        if (playerCurrency.GetCoins() < item.price) return;

        // Pay when the item is actually placed; right-click cancel is free.
        roomManager.StartPlacingFurniture(item.prefab, () => playerCurrency.SpendCoins(item.price));
        panel.SetActive(false);
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        var canvasGO = new GameObject("FurnitureStoreCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Always-visible shop button, bottom right.
        Button shop = TextButton("ShopButton", canvasGO.transform, "Shop", TabColor, 26);
        var shopRect = (RectTransform)shop.transform;
        shopRect.anchorMin = shopRect.anchorMax = shopRect.pivot = new Vector2(1, 0);
        shopRect.sizeDelta = new Vector2(150, 60);
        shopRect.anchoredPosition = new Vector2(-24, 24);
        shop.onClick.AddListener(Toggle);

        // Panel docked to the right so the room stays visible while browsing.
        panel = Make("StorePanel", canvasGO.transform, typeof(Image), typeof(VerticalLayoutGroup));
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(1, 0);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 0.5f);
        panelRect.sizeDelta = new Vector2(500, -48);
        panelRect.anchoredPosition = new Vector2(-24, 0);
        Style(panel.GetComponent<Image>(), PanelColor);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 14;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Header: title, coins, close.
        GameObject header = Row("Header", panel.transform, 48, 12, false);
        TMP_Text title = Text("Title", header.transform, "Furniture Store", 32, TextColor, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        coinsText = Text("Coins", header.transform, "0", 26, AccentColor, TextAlignmentOptions.Right);
        coinsText.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
        Button close = TextButton("Close", header.transform, "X", TabColor, 22);
        close.gameObject.AddComponent<LayoutElement>().preferredWidth = 48;
        close.onClick.AddListener(Toggle);

        // Category tabs.
        GameObject tabs = Row("Tabs", panel.transform, 40, 8, true);
        AddTab(tabs.transform, "All", -1);
        foreach (StoreCategory c in Enum.GetValues(typeof(StoreCategory)))
            AddTab(tabs.transform, c.ToString(), (int)c);

        // Scrollable grid of cards.
        GameObject scroll = Make("Scroll", panel.transform, typeof(ScrollRect), typeof(LayoutElement));
        scroll.GetComponent<LayoutElement>().flexibleHeight = 1;
        GameObject viewport = Make("Viewport", scroll.transform, typeof(RectMask2D));
        Stretch((RectTransform)viewport.transform);
        GameObject content = Make("Content", viewport.transform, typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(140, 170);
        grid.spacing = new Vector2(12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewport.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30;

        foreach (FurnitureItem item in catalog.items)
            if (item != null && item.prefab != null) AddCard(content.transform, item);

        // Details of the selected item and the Buy button.
        GameObject details = Make("Details", panel.transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Style(details.GetComponent<Image>(), CardColor);
        details.GetComponent<LayoutElement>().preferredHeight = 210;
        var detailsLayout = details.GetComponent<VerticalLayoutGroup>();
        detailsLayout.padding = new RectOffset(16, 16, 12, 12);
        detailsLayout.spacing = 6;
        detailsLayout.childControlWidth = detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        detailName = Text("Name", details.transform, "", 24, TextColor, TextAlignmentOptions.Left);
        detailName.fontStyle = FontStyles.Bold;
        detailDescription = Text("Description", details.transform, "Select something to see what it is.", 18, MutedText, TextAlignmentOptions.TopLeft);
        detailDescription.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        detailTags = Text("Tags", details.transform, "", 16, AccentColor, TextAlignmentOptions.Left);
        buyButton = TextButton("Buy", details.transform, "", TabColor, 22);
        buyButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
        buyLabel = buyButton.GetComponentInChildren<TMP_Text>();
        buyButton.onClick.AddListener(Buy);

        Refresh(Coins);
    }

    private void AddTab(Transform parent, string label, int category)
    {
        TextButton(label, parent, label, TabColor, 18).onClick.AddListener(() => ShowCategory(category));
    }

    private void AddCard(Transform parent, FurnitureItem item)
    {
        GameObject root = Make(item.name, parent, typeof(Image), typeof(Button));
        var card = new Card { item = item, root = root, background = root.GetComponent<Image>() };
        Style(card.background, CardColor);
        root.GetComponent<Button>().onClick.AddListener(() => Select(card));

        var icon = Make("Icon", root.transform, typeof(Image)).GetComponent<Image>();
        icon.sprite = item.icon != null ? item.icon : FurnitureIconRenderer.Render(item.prefab);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;
        Anchor(icon.gameObject, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.96f));
        Anchor(Text("Name", root.transform, item.displayName, 17, TextColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.32f));
        Anchor(Text("Price", root.transform, item.price.ToString(), 19, AccentColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0, 0.02f), new Vector2(1, 0.17f));
        cards.Add(card);
    }

    private static GameObject Make(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (Type c in components) go.AddComponent(c);
        return go;
    }

    private static GameObject Row(string name, Transform parent, float height, float spacing, bool expand)
    {
        GameObject row = Make(name, parent, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = height;
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = expand;
        return row;
    }

    private void Style(Image image, Color color)
    {
        image.color = color;
        if (roundedSprite == null) return;
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
    }

    private static TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = Make(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button TextButton(string name, Transform parent, string label, Color color, float fontSize)
    {
        GameObject go = Make(name, parent, typeof(Image), typeof(Button));
        Style(go.GetComponent<Image>(), color);
        Stretch((RectTransform)Text("Label", go.transform, label, fontSize, TextColor, TextAlignmentOptions.Center).transform);
        return go.GetComponent<Button>();
    }

    private static void Anchor(GameObject go, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
