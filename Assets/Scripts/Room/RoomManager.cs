using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the study room's appearance and placed items
/// </summary>
public class RoomManager : MonoBehaviour
{
    [SerializeField] private MeshRenderer wallRenderer;
    [SerializeField] private MeshRenderer floorRenderer;
    [SerializeField] private Transform roomItemsContainer;
    
    [SerializeField] private Material defaultWallpaper;
    [SerializeField] private Material defaultFlooring;

    private Dictionary<ItemType, GameObject> activeItems = new Dictionary<ItemType, GameObject>();
    private ISaveSystem saveSystem;

    private void Awake()
    {
        saveSystem = new PlayerPrefsSaveSystem();
    }

    private void Start()
    {
        LoadRoomState();
    }

    /// <summary>
    /// Applies a wallpaper material to the room walls
    /// </summary>
    public void ApplyWallpaper(Material wallpaperMaterial)
    {
        if (wallRenderer != null && wallpaperMaterial != null)
        {
            wallRenderer.material = wallpaperMaterial;
            SaveRoomState();
        }
    }

    /// <summary>
    /// Applies a flooring material to the room floor
    /// </summary>
    public void ApplyFlooring(Material floorMaterial)
    {
        if (floorRenderer != null && floorMaterial != null)
        {
            floorRenderer.material = floorMaterial;
            SaveRoomState();
        }
    }

    /// <summary>
    /// Places a furniture or decoration item in the room
    /// </summary>
    public void PlaceItem(GameObject itemPrefab, ItemType itemType, Vector3 position)
    {
        if (itemPrefab == null) return;

        RemoveItem(itemType);

        GameObject newItem = Instantiate(itemPrefab, position, Quaternion.identity, roomItemsContainer);
        activeItems.Add(itemType, newItem);
        SaveRoomState();
    }

    /// <summary>
    /// Removes an item from the room
    /// </summary>
    public void RemoveItem(ItemType itemType)
    {
        if (activeItems.ContainsKey(itemType))
        {
            Destroy(activeItems[itemType]);
            activeItems.Remove(itemType);
            SaveRoomState();
        }
    }

    private void SaveRoomState()
    {
        var roomData = new RoomData
        {
            currentWallpaper = wallRenderer?.material?.name ?? defaultWallpaper?.name,
            currentFlooring = floorRenderer?.material?.name ?? defaultFlooring?.name
        };

        foreach (var item in activeItems)
        {
            var itemTransform = item.Value.transform;
            roomData.placedItems.Add(new PlacedItemData(
                item.Key,
                itemTransform.position,
                itemTransform.rotation,
                item.Value.name.Replace("(Clone)", "")
            ));
        }

        saveSystem.SaveData("RoomState", roomData);
    }

    private void LoadRoomState()
    {
        var roomData = saveSystem.LoadData("RoomState", new RoomData());
        
        // Load materials
        if (!string.IsNullOrEmpty(roomData.currentWallpaper))
        {
            Material wallpaper = ResourceManager.Instance.GetMaterial(roomData.currentWallpaper);
            if (wallpaper != null)
                wallRenderer.material = wallpaper;
            else if (defaultWallpaper != null)
                wallRenderer.material = defaultWallpaper;
        }

        if (!string.IsNullOrEmpty(roomData.currentFlooring))
        {
            Material flooring = ResourceManager.Instance.GetMaterial(roomData.currentFlooring);
            if (flooring != null)
                floorRenderer.material = flooring;
            else if (defaultFlooring != null)
                floorRenderer.material = defaultFlooring;
        }

        // Clear existing items
        foreach (var item in activeItems.Values)
        {
            Destroy(item);
        }
        activeItems.Clear();

        // Load placed items
        foreach (var itemData in roomData.placedItems)
        {
            GameObject prefab = ResourceManager.Instance.GetPrefab(itemData.prefabName);
            if (prefab != null)
            {
                GameObject newItem = Instantiate(prefab, itemData.position, itemData.rotation, roomItemsContainer);
                activeItems[itemData.type] = newItem;
            }
        }
    }
}