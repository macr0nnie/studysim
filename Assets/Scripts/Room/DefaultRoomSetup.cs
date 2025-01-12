using UnityEngine;
using Events;

/// <summary>
/// Handles the initial setup of the bedroom scene
/// </summary>
public class DefaultRoomSetup : MonoBehaviour
{
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private GameObject deskPrefab;
    [SerializeField] private GameObject chairPrefab;
    [SerializeField] private GameObject computerPrefab;
    [SerializeField] private GameObject bedPrefab;
    [SerializeField] private GameObject dresserPrefab;
    [SerializeField] private GameObject lampPrefab;

    private void Start()
    {
        if (!ValidatePrefabs()) return;
        SetupDefaultRoom();
    }

    private bool ValidatePrefabs()
    {
        if (roomManager == null)
        {
            Debug.LogError("DefaultRoomSetup: RoomManager reference is missing!");
            return false;
        }
        if (deskPrefab == null)
        {
            Debug.LogError("DefaultRoomSetup: Desk prefab is missing!");
            return false;
        }
        if (chairPrefab == null)
        {
            Debug.LogError("DefaultRoomSetup: Chair prefab is missing!");
            return false;
        }
        if (computerPrefab == null)
        {
            Debug.LogError("DefaultRoomSetup: Computer prefab is missing!");
            return false;
        }
        if (bedPrefab == null)
        {
            Debug.LogError("DefaultRoomSetup: Bed prefab is missing!");
            return false;
        }
        if (dresserPrefab == null)
        {
            Debug.LogError("DefaultRoomSetup: Dresser prefab is missing!");
            return false;
        }
        if (lampPrefab == null)
        {
            Debug.LogError("DefaultRoomSetup: Lamp prefab is missing!");
            return false;
        }
        return true;
    }

    private void SetupDefaultRoom()
    {
        // Place desk against the wall
        roomManager.PlaceItem(deskPrefab, ItemType.Desk, new Vector3(2f, 0f, 1f));
        
        // Place chair at the desk
        roomManager.PlaceItem(chairPrefab, ItemType.Chair, new Vector3(2f, 0f, 1.5f));
        
        // Place computer on the desk
        roomManager.PlaceItem(computerPrefab, ItemType.Computer, new Vector3(2f, 0.75f, 1f));
        
        // Place bed along another wall
        roomManager.PlaceItem(bedPrefab, ItemType.Bed, new Vector3(-2f, 0f, -2f));
        
        // Place dresser
        roomManager.PlaceItem(dresserPrefab, ItemType.Dresser, new Vector3(-2f, 0f, 2f));
        
        // Place lamp on the desk
        roomManager.PlaceItem(lampPrefab, ItemType.Lamp, new Vector3(2.5f, 0.75f, 1f));
    }
}