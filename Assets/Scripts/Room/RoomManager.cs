using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the room decoration system including furniture placement and wall/floor customization
/// Setup: Attach to an empty GameObject that serves as the room container
/// Dependencies: Requires GridSystem component and furniture prefabs
/// </summary>
public class RoomManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private Vector2 gridSize = new Vector2(10, 10);
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private LayerMask placementLayer;
    
    [Header("Visual Feedback")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    
    private Dictionary<Vector2Int, GameObject> placedObjects = new Dictionary<Vector2Int, GameObject>();
    private GameObject currentPreview;
    private bool isPlacementValid;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
            enabled = false;
            return;
        }

        if (validPlacementMaterial == null || invalidPlacementMaterial == null)
        {
            Debug.LogError("Placement materials not assigned!");
            enabled = false;
            return;
        }

        InitializeGrid();
    }

    private void Update()
    {
        if (currentPreview != null)
        {
            UpdatePreviewPosition();
            HandlePlacement();
        }
    }

    private void InitializeGrid()
    {
        if (gridSize.x <= 0 || gridSize.y <= 0)
        {
            Debug.LogError("Invalid grid size!");
            enabled = false;
            return;
        }

        if (cellSize <= 0)
        {
            Debug.LogError("Invalid cell size!");
            enabled = false;
            return;
        }

        placedObjects.Clear();
        // Additional grid initialization can be added here if needed
    }

    public void StartPlacingFurniture(GameObject furniturePrefab)
    {
        if (furniturePrefab == null)
        {
            Debug.LogError("Furniture prefab is null!");
            return;
        }

        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(furniturePrefab);
        if (currentPreview == null)
        {
            Debug.LogError("Failed to instantiate furniture preview!");
            return;
        }

        SetPreviewMaterial(validPlacementMaterial);
    }

    private void UpdatePreviewPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, placementLayer))
        {
            Vector3 position = hit.point;
            Vector2Int gridPosition = GetGridPosition(position);
            position = SnapToGrid(position);
            
            currentPreview.transform.position = position;
            isPlacementValid = IsValidPlacement(gridPosition);
            SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
        }
    }

    private void HandlePlacement()
    {
        if (Input.GetMouseButtonDown(0) && isPlacementValid)
        {
            Debug.Log("Placing object...");
            Vector2Int gridPos = GetGridPosition(currentPreview.transform.position);
            
            if (!placedObjects.ContainsKey(gridPos))
            {
                GameObject placedObject = Instantiate(currentPreview, currentPreview.transform.position, currentPreview.transform.rotation);
                placedObjects[gridPos] = placedObject;
                ResetPreviewMaterial(placedObject);
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    private Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / cellSize),
            Mathf.RoundToInt(worldPosition.z / cellSize)
        );
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / cellSize) * cellSize,
            0f,
            Mathf.Round(position.z / cellSize) * cellSize
        );
    }

    private bool IsValidPlacement(Vector2Int gridPosition)
    {
        return !placedObjects.ContainsKey(gridPosition) &&
               gridPosition.x >= 0 && gridPosition.x < gridSize.x &&
               gridPosition.y >= 0 && gridPosition.y < gridSize.y;
    }

    private void SetPreviewMaterial(Material material)
    {
        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = new Material[renderer.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }
            renderer.materials = materials;
        }
    }

    private void ResetPreviewMaterial(GameObject obj)
    {
        // Reset materials to original if needed
    }

    public void CancelPlacement()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    public void ChangeMaterial(MaterialType type, Material newMaterial)
    {
        switch (type)
        {
            case MaterialType.Wall:
                // Apply wall material

                break;
            case MaterialType.Floor:
                // Apply floor material
                break;
        }
    }
}

public enum MaterialType
{
    Wall,
    Floor
}