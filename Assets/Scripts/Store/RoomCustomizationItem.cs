using UnityEngine;

[System.Serializable]
public class RoomCustomizationItem : IStoreItem
{
    [SerializeField] private string itemName;
    [SerializeField] private float price;
    [SerializeField] private bool isOwned;
    [SerializeField] private StoreManager.ItemType itemType;
    [SerializeField] private GameObject prefab;
    
    public string Name => itemName;
    public float Price => price;
    public bool IsOwned
    {
        get => isOwned;
        set => isOwned = value;
    }

    private RoomManager roomManager;

    public RoomCustomizationItem(RoomManager roomManager)
    {
        this.roomManager = roomManager;
    }

    public void Apply()
    {
        if (IsOwned)
        {
            Vector3 defaultPosition = Vector3.zero; // This should be set based on the item type
            roomManager.PlaceItem(prefab, itemType, defaultPosition);
        }
    }
}