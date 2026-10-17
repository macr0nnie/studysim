using UnityEngine;

// Only the old scene store's item buttons still use this (their onClick calls StartFurniturePlacement).
// The rest of the UI is built at runtime by GameHUD, FurnitureStoreUI, MusicPlayerUI and friends.
public class UIManager : MonoBehaviour
{
    [SerializeField] private int defaultFurniturePrice = 10;
    [SerializeField] private PlayerCurrency playerCurrency;

    public void StartFurniturePlacement(GameObject furniturePrefab)
    {
        RoomManager roomManager = FindAnyObjectByType<RoomManager>();
        if (roomManager == null || playerCurrency == null) return;
        int price = defaultFurniturePrice;
        if (playerCurrency.GetCoins() < price)
        {
            Debug.Log("Not enough currency to place furniture!");
            return;
        }
        // Charge on placement, not on pick, so cancelling with right-click costs nothing.
        roomManager.StartPlacingFurniture(furniturePrefab, () => playerCurrency.SpendCoins(price));
    }
}
