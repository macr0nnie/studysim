using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// The furniture store. Builds its own screen-space UI from a FurnitureCatalog at startup, so it only needs
// a catalog assigned: a "Shop" button opens a left-docked panel with category tabs, a grid of icon cards,
// and a details area (description, tags, price) with a Buy button. Press B to toggle it.
public class FurnitureStoreUI : MonoBehaviour
{
    [SerializeField] private FurnitureCatalog catalog;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private PlayerCurrency playerCurrency;


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
    private readonly Queue<(Image image, FurnitureItem item)> pendingIcons = new Queue<(Image, FurnitureItem)>();
    private int shownCategory = -1;
    private readonly List<(int category, Image image)> tabs = new List<(int, Image)>();

    // Tab labels, indexed by StoreCategory.
    private static readonly string[] CategoryNames =
        { "Seating", "Desks & tables", "Beds", "Storage", "Lighting", "Plants", "Decor", "Wall decor", "Electronics", "Kitchen & bath", "Spooky" };

    // Any scene with a RoomManager gets a store, even if nobody added one to the scene.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureStore();
        EnsureStore();
    }

    private static void EnsureStore()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<FurnitureStoreUI>() == null)
            new GameObject("FurnitureStore").AddComponent<FurnitureStoreUI>();
    }

    private void Awake()
    {
        if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
        if (playerCurrency == null) playerCurrency = FindFirstObjectByType<PlayerCurrency>();
        if (catalog == null) catalog = Resources.Load<FurnitureCatalog>("FurnitureCatalog");
        if (catalog == null)
        {
            Debug.LogError("FurnitureStoreUI: no catalog assigned.", this);
            enabled = false;
            return;
        }
        BuildUI();
        panel.SetActive(false);
        StartCoroutine(RenderIcons()); // in the background, so they are ready by the time the store opens
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
        if (Controls.Pressed(Controls.Act.Store) && !Typing()) Toggle();
    }

    public void Toggle()
    {
        if (ToggleDrawer(this, panel)) Refresh(Coins);
    }

    // Hundreds of items would stall the first open if every icon rendered at once, so do a few per frame.
    private IEnumerator RenderIcons()
    {
        while (pendingIcons.Count > 0)
        {
            for (int i = 0; i < 4 && pendingIcons.Count > 0; i++)
            {
                var (image, item) = pendingIcons.Dequeue();
                image.sprite = FurnitureIconRenderer.Render(item.prefab, 128); // cards are ~124px, so 128 is plenty
                image.enabled = image.sprite != null;
            }
            yield return null;
        }
    }

    // Values match StoreCategory; -1 shows everything.
    public void ShowCategory(int category)
    {
        shownCategory = category;
        foreach (Card c in cards)
            c.root.SetActive(category < 0 || (int)c.item.category == category);
        foreach (var (tabCategory, image) in tabs) image.color = tabCategory == category ? SelectedColor : TabColor;
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

        // Pay when the item is actually placed; right-click cancel is free, removing it later refunds it.
        roomManager.StartPlacingFurniture(item);
        panel.SetActive(false);
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        GameObject canvasGO = MakeCanvas("FurnitureStoreCanvas", transform, 10).gameObject;

        // Shop button, second in the dock.
        Button shop = NavButton(canvasGO.transform, 1, "Shop", Controls.Act.Store);
        shop.onClick.AddListener(Toggle);

        panel = MakeDrawer("StorePanel", canvasGO.transform);

        // Header: title, coins, close.
        GameObject header = Row("Header", panel.transform, 40, 10, false);
        TMP_Text title = MakeText("Title", header.transform, "Furniture Store", TitleSize, TextColor, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        coinsText = MakeText("Coins", header.transform, "0", HeadingSize, AccentColor, TextAlignmentOptions.Right);
        coinsText.gameObject.AddComponent<LayoutElement>().preferredWidth = 110;
        Button close = TextButton("Close", header.transform, "X", TabColor, HeadingSize);
        close.gameObject.AddComponent<LayoutElement>().preferredWidth = 40;
        close.onClick.AddListener(Toggle);

        // Category tabs, wrapping onto as many rows as they need; categories with nothing in them are left out.
        GameObject tabGrid = Make("Tabs", panel.transform, typeof(GridLayoutGroup));
        var tabLayout = tabGrid.GetComponent<GridLayoutGroup>();
        tabLayout.cellSize = new Vector2(97, 30); // four across the drawer
        tabLayout.spacing = new Vector2(5, 5);
        tabLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        tabLayout.constraintCount = 4;
        AddTab(tabGrid.transform, "All", -1);
        foreach (StoreCategory c in Enum.GetValues(typeof(StoreCategory)))
            if (catalog.items.Exists(i => i != null && i.prefab != null && i.category == c))
                AddTab(tabGrid.transform, CategoryNames[(int)c], (int)c);

        // Scrollable grid of cards.
        Transform content = ScrollList(panel.transform, out _);
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(120, 150); // three per row beside the scrollbar
        grid.spacing = new Vector2(10, 10);
        grid.childAlignment = TextAnchor.UpperCenter;

        foreach (FurnitureItem item in catalog.items)
            if (item != null && item.prefab != null) AddCard(content, item);

        // Details of the selected item and the Buy button.
        GameObject details = Make("Details", panel.transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        Style(details.GetComponent<Image>(), CardColor);
        details.GetComponent<LayoutElement>().preferredHeight = 170;
        details.GetComponent<LayoutElement>().flexibleHeight = 0; // the description's flexible height would otherwise grow this box
        var detailsLayout = details.GetComponent<VerticalLayoutGroup>();
        detailsLayout.padding = new RectOffset(14, 14, 10, 10);
        detailsLayout.spacing = 4;
        detailsLayout.childControlWidth = detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        detailName = MakeText("Name", details.transform, "", HeadingSize, TextColor, TextAlignmentOptions.Left);
        detailName.fontStyle = FontStyles.Bold;
        detailDescription = MakeText("Description", details.transform, "Select something to see what it is.", LabelSize, MutedText, TextAlignmentOptions.TopLeft);
        detailDescription.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        detailTags = MakeText("Tags", details.transform, "", CaptionSize, AccentColor, TextAlignmentOptions.Left);
        buyButton = TextButton("Buy", details.transform, "", AccentButtonColor, BodySize);
        buyButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
        buyLabel = buyButton.GetComponentInChildren<TMP_Text>();
        buyButton.onClick.AddListener(Buy);

        ShowCategory(-1);
        Refresh(Coins);
    }

    private void AddTab(Transform parent, string label, int category)
    {
        Button tab = TextButton(label, parent, label, TabColor, CaptionSize);
        tab.onClick.AddListener(() => ShowCategory(category));
        tabs.Add((category, tab.GetComponent<Image>()));
    }

    private void AddCard(Transform parent, FurnitureItem item)
    {
        GameObject root = Make(item.name, parent, typeof(Image), typeof(Button));
        var card = new Card { item = item, root = root, background = root.GetComponent<Image>() };
        Style(card.background, CardColor);
        root.GetComponent<Button>().onClick.AddListener(() => Select(card));

        var icon = Make("Icon", root.transform, typeof(Image)).GetComponent<Image>();
        icon.sprite = item.icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = icon.sprite != null;
        if (icon.sprite == null) pendingIcons.Enqueue((icon, item));
        Anchor(icon.gameObject, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.96f));
        Anchor(MakeText("Name", root.transform, item.displayName, CaptionSize, TextColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.32f));
        Anchor(MakeText("Price", root.transform, item.price.ToString(), LabelSize, AccentColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0, 0.02f), new Vector2(1, 0.17f));
        cards.Add(card);
    }
}
