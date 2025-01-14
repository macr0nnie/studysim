using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    [Header("Placement Settings")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private float snapThreshold = 0.5f;

    [Header("Visual Feedback")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;

    private List<GameObject> placedObjects = new List<GameObject>();
    private GameObject currentPreview;
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false;
    private GameObject selectedObject;

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
        }
        else if (isEditMode && selectedObject != null)
        {
            UpdateSelectedObjectPosition();
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

        if (Physics.Raycast(ray, out hit, 100f, placementLayer))
        {
            Vector3 position = hit.point;
            position = SnapToNearbyObjects(position);

            // Adjust the Y position to place the object on top of the placement plane
            position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;

            currentPreview.transform.position = position;
            isPlacementValid = IsValidPlacement(position);
            SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
        }
        else
        {
            Debug.LogWarning("Raycast did not hit any placement surface.");
        }
    }

    private Vector3 SnapToNearbyObjects(Vector3 position)
    {
        foreach (GameObject placedObject in placedObjects)
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

    private void PlaceObject()
    {
        Vector3 position = currentPreview.transform.position;

        GameObject placedObject = Instantiate(currentPreview, position, currentPreview.transform.rotation);
        placedObjects.Add(placedObject);
        ResetPreviewMaterial(placedObject);
        Destroy(currentPreview);
        currentPreview = null;
        Debug.Log("Object placed successfully.");
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
        Collider[] colliders = Physics.OverlapBox(position, currentPreview.GetComponent<Collider>().bounds.extents, Quaternion.identity, placementLayer);
        bool isValid = colliders.Length == 0;

        // Additional check to ensure no intersection with other placed objects
        foreach (GameObject placedObject in placedObjects)
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

    public void EnterEditMode(GameObject selectedObject)
    {
        isEditMode = true;
        this.selectedObject = selectedObject;
    }

    private void UpdateSelectedObjectPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, placementLayer))
        {
            Vector3 position = hit.point;
            position = SnapToNearbyObjects(position);

            // Adjust the Y position to place the object on top of the placement plane
            position.y += selectedObject.GetComponent<Collider>().bounds.extents.y;

            selectedObject.transform.position = position;
        }
    }

    private void HandleEditMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlaceSelectedObject();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelEditMode();
        }
    }

    private void PlaceSelectedObject()
    {
        selectedObject = null;
        isEditMode = false;
    }

    private void CancelEditMode()
    {
        selectedObject = null;
        isEditMode = false;
    }

    private void HandleObjectSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer))
            {
                GameObject hitObject = hit.collider.gameObject;
                if (placedObjects.Contains(hitObject))
                {
                    EnterEditMode(hitObject);
                }
            }
        }
    }
}