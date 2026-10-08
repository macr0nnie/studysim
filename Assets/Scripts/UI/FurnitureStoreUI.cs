using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Builds the furniture store from a FurnitureCatalog: one card per item, priced, filterable by category.
// Card template children: "Icon" (Image), "Name" (TMP_Text), "Price" (TMP_Text), plus a Button on the root.
// Study Sim > Create Furniture Store UI builds a ready-wired version of this panel.
public class FurnitureStoreUI : MonoBehaviour
{
    [SerializeField] private FurnitureCatalog catalog;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject cardTemplate;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private PlayerCurrency playerCurrency;
    [SerializeField] private bool closeWhenPlacing = true;

    private readonly List<(FurnitureItem item, GameObject card, Button button)> cards = new List<(FurnitureItem, GameObject, Button)>();
    private int shownCategory = -1;

    private void Awake()
    {
        if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
        if (playerCurrency == null) playerCurrency = FindFirstObjectByType<PlayerCurrency>();
        if (cardTemplate != null) cardTemplate.SetActive(false);
        BuildCards();
    }

    private void OnEnable()
    {
        if (playerCurrency != null) playerCurrency.OnCoinsChanged.AddListener(Refresh);
        Refresh(playerCurrency != null ? playerCurrency.GetCoins() : 0);
    }

    private void OnDisable()
    {
        if (playerCurrency != null) playerCurrency.OnCoinsChanged.RemoveListener(Refresh);
    }

    private void BuildCards()
    {
        if (catalog == null || cardContainer == null || cardTemplate == null)
        {
            Debug.LogError("FurnitureStoreUI: catalog, card container or card template not assigned.", this);
            return;
        }

        foreach (FurnitureItem item in catalog.items)
        {
            if (item == null || item.prefab == null) continue;

            GameObject card = Instantiate(cardTemplate, cardContainer);
            card.name = item.name;
            card.SetActive(true);

            Image icon = card.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = item.icon;
                icon.enabled = item.icon != null;
            }
            TMP_Text label = card.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (label != null) label.text = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
            TMP_Text price = card.transform.Find("Price")?.GetComponent<TMP_Text>();
            if (price != null) price.text = item.price.ToString();

            Button button = card.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => Buy(item));
            cards.Add((item, card, button));
        }
    }

    // Wired to the category tabs; -1 shows everything. Values match Furniture.FurnitureType.
    public void ShowCategory(int category)
    {
        shownCategory = category;
        foreach (var c in cards)
            c.card.SetActive(category < 0 || (int)c.item.category == category);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Refresh(int coins)
    {
        if (coinsText != null) coinsText.text = coins.ToString();
        foreach (var c in cards)
            if (c.button != null) c.button.interactable = coins >= c.item.price;
        ShowCategory(shownCategory);
    }

    private void Buy(FurnitureItem item)
    {
        if (roomManager == null || playerCurrency == null) return;
        if (playerCurrency.GetCoins() < item.price) return;

        // Pay when the item is actually placed; right-click cancel is free.
        roomManager.StartPlacingFurniture(item.prefab, () => playerCurrency.SpendCoins(item.price));
        if (closeWhenPlacing) Close();
    }
}
