using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the room customization store functionality, including purchasing and applying room items
/// </summary>
public class RoomItemManager : MonoBehaviour
{
    [SerializeField] private List<RoomCustomizationItem> availableItems = new List<RoomCustomizationItem>();
    private RoomManager roomManager;
    private ISaveSystem saveSystem;

    private void Awake()
    {
        roomManager = FindObjectOfType<RoomManager>();
        saveSystem = new PlayerPrefsSaveSystem();
        LoadPurchasedItems();
    }

    /// <summary>
    /// Attempts to purchase a room item at the given index
    /// </summary>
    /// <param name="index">Index of the item to purchase</param>
    /// <returns>True if purchase was successful, false otherwise</returns>
    public bool PurchaseItem(int index)
    {
        if (index >= 0 && index < availableItems.Count)
        {
            RoomCustomizationItem item = availableItems[index];
            if (!item.IsOwned)
            {
                if (CurrencyManager.Instance.SpendMoney(item.Price))
                {
                    item.IsOwned = true;
                    SavePurchasedItems();
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Applies a purchased room item at the specified position
    /// </summary>
    /// <param name="index">Index of the item to apply</param>
    /// <param name="position">Position to place the item</param>
    public void ApplyItem(int index, Vector3 position)
    {
        if (index >= 0 && index < availableItems.Count)
        {
            availableItems[index].Apply();
        }
    }

    private void SavePurchasedItems()
    {
        var saveData = new List<bool>();
        foreach (var item in availableItems)
        {
            saveData.Add(item.IsOwned);
        }
        saveSystem.SaveData("RoomItems", saveData);
    }

    private void LoadPurchasedItems()
    {
        var saveData = saveSystem.LoadData("RoomItems", new List<bool>());
        for (int i = 0; i < Mathf.Min(saveData.Count, availableItems.Count); i++)
        {
            availableItems[i].IsOwned = saveData[i];
        }
    }
}