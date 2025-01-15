using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    [Header("Placement Settings")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private LayerMask furnitureLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask shelfLayer;
    [SerializeField] private float snapThreshold = 0.5f;

    [Header("Visual Feedback")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;

    private Dictionary<Vector2Int, GameObject> placedObjects = new Dictionary<Vector2Int, GameObject>();
    private GameObject currentPreview;
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false;

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
    }

    private void Update()
    {
        if (currentPreview != null)
        {
            UpdatePreviewPosition();
            HandlePlacement();
            HandleRotationAndFlipping();
        }
        else if (isEditMode)
        {
            HandleEditMode();
        }
        else
        {
            HandleObjectSelection();
        }
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

        if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
        {
            Vector3 position = hit.point;
            Quaternion rotation = Quaternion.identity;
            Furniture furniture = currentPreview.GetComponent<Furniture>();

            if (furniture != null)
            {
                switch (furniture.Type)
                {
                    case Furniture.FurnitureType.Wall:
                        if (((1 << hit.collider.gameObject.layer) & wallLayer) != 0)
                        {
                            position = SnapToWall(position, hit.normal);
                            rotation = Quaternion.LookRotation(-hit.normal);
                        }
                        else
                        {
                            isPlacementValid = false;
                            SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                    case Furniture.FurnitureType.Shelf:
                        if (((1 << hit.collider.gameObject.layer) & shelfLayer) != 0)
                        {
                            position = SnapToShelf(position);
                        }
                        else
                        {
                            position = hit.point;
                        }
                        break;
                    case Furniture.FurnitureType.Floor:
                    default:
                        position = SnapToNearbyObjects(position);
                        position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
                        break;
                }
            }

            currentPreview.transform.position = position;
            currentPreview.transform.rotation = rotation;
            isPlacementValid = IsValidPlacement(position);
            SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
        }
        else
        {
            Vector3 position = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            currentPreview.transform.position = position;
            isPlacementValid = false;
            SetPreviewMaterial(invalidPlacementMaterial);
        }
    }

    private Vector3 SnapToWall(Vector3 position, Vector3 normal)
    {
        position += normal * currentPreview.GetComponent<Collider>().bounds.extents.z;
        return position;
    }

    private Vector3 SnapToShelf(Vector3 position)
    {
        position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
        return position;
    }

    private Vector3 SnapToNearbyObjects(Vector3 position)
    {
        foreach (GameObject placedObject in placedObjects.Values)
        {
            Collider collider = placedObject.GetComponent<Collider>();
            if (collider != null)
            {
                Vector3 closestPoint = collider.ClosestPoint(position);
                if (Vector3.Distance(position, closestPoint) <= snapThreshold)
                {
                    position = closestPoint;
                    break;
                }
            }
        }
        return position;
    }

    private void HandlePlacement()
    {
        if (Input.GetMouseButtonDown(0) && isPlacementValid)
        {
            PlaceObject();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    private void HandleRotationAndFlipping()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (currentPreview != null)
            {
                currentPreview.transform.Rotate(Vector3.up, 90f);
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (currentPreview != null)
            {
                currentPreview.transform.Rotate(Vector3.forward, 180f);
            }
        }
    }

    private void PlaceObject()
    {
        Vector3 position = currentPreview.transform.position;
        Vector2Int gridPos = GetGridPosition(position);

        if (placedObjects.ContainsKey(gridPos))
        {
            SwapObject(gridPos);
        }
        else
        {
            GameObject placedObject = Instantiate(currentPreview, position, currentPreview.transform.rotation);
            placedObjects[gridPos] = placedObject;
            ResetPreviewMaterial(placedObject);
        }

        Destroy(currentPreview);
        currentPreview = null;
        Debug.Log("Object placed successfully.");
    }

    private void SwapObject(Vector2Int gridPos)
    {
        GameObject existingObject = placedObjects[gridPos];
        Destroy(existingObject);

        GameObject placedObject = Instantiate(currentPreview, currentPreview.transform.position, currentPreview.transform.rotation);
        placedObjects[gridPos] = placedObject;
        ResetPreviewMaterial(placedObject);
        Debug.Log("Object swapped successfully.");
    }

    private void CancelPlacement()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
            Debug.Log("Placement canceled.");
        }
    }

    private bool IsValidPlacement(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapBox(position, currentPreview.GetComponent<Collider>().bounds.extents, currentPreview.transform.rotation, furnitureLayer);
        bool isValid = colliders.Length == 0;

        foreach (GameObject placedObject in placedObjects.Values)
        {
            if (placedObject.GetComponent<Collider>().bounds.Intersects(currentPreview.GetComponent<Collider>().bounds))
            {
                isValid = false;
                break;
            }
        }

        Debug.Log("Placement valid: " + isValid);
        return isValid;
    }

    private void SetPreviewMaterial(Material material)
    {
        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = material;
        }
    }

    private void ResetPreviewMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = validPlacementMaterial;
        }
    }

    private Vector2Int GetGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / snapThreshold);
        int y = Mathf.FloorToInt(worldPosition.z / snapThreshold);
        return new Vector2Int(x, y);
    }

    private void HandleEditMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
            {
                GameObject hitObject = hit.collider.gameObject;
                if (placedObjects.ContainsValue(hitObject))
                {
                    Vector3 position = hit.point;
                    Quaternion rotation = hitObject.transform.rotation;

                    if (hitObject.GetComponent<Furniture>().Type == Furniture.FurnitureType.Wall)
                    {
                        position = SnapToWall(position, hit.normal);
                        rotation = Quaternion.LookRotation(-hit.normal);
                    }
                    else if (hitObject.GetComponent<Furniture>().Type == Furniture.FurnitureType.Shelf)
                    {
                        position = SnapToShelf(position);
                    }
                    else
                    {
                        position = SnapToNearbyObjects(position);
                        position.y += hitObject.GetComponent<Collider>().bounds.extents.y;
                    }

                    hitObject.transform.position = position;
                    hitObject.transform.rotation = rotation;
                }
            }
        }
    }

    private void HandleObjectSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
            {
                GameObject hitObject = hit.collider.gameObject;
                if (placedObjects.ContainsValue(hitObject))
                {
                    isEditMode = true;
                }
            }
        }
    }
}