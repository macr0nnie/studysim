using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Data structure for saving/loading room state
/// </summary>
[Serializable]
public class RoomData
{
    public string currentWallpaper;
    public string currentFlooring;
    public List<PlacedItemData> placedItems = new List<PlacedItemData>();
}

/// <summary>
/// Data structure for a placed item in the room
/// </summary>
[Serializable]
public class PlacedItemData
{
    public ItemType type;
    public Vector3 position;
    public Quaternion rotation;
    public string prefabName;

    public PlacedItemData(ItemType type, Vector3 position, Quaternion rotation, string prefabName)
    {
        this.type = type;
        this.position = position;
        this.rotation = rotation;
        this.prefabName = prefabName;
    }
}