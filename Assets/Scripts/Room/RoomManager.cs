using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    // Settings
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private LayerMask furnitureLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask shelfLayer;
    [SerializeField] private float snapThreshold = 0.5f;
    [SerializeField] private float gridSize = 1.0f;
    [SerializeField] private bool useGridPlacement = true;

    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;

    [SerializeField] private GameObject placementParticlePrefab;

    private List<GameObject> placedObjects = new List<GameObject>();
    private GameObject currentPreview;
    private Action onPreviewPlaced; // e.g. charge the player only once the item is actually placed
    private GameObject selectedObject;
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false;

    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();

    private float lastClickTime;
    private const float doubleClickThreshold = 0.3f;

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

        if (placementParticlePrefab == null)
        {
            Debug.LogError("Placement particle prefab not assigned!");
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
            HandleRotationAndFlipping();
            if (Input.GetKeyDown(KeyCode.Escape)) EnterEditMode();
        }

        else
        {
            HandleObjectSelection();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            Undo();
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            Redo();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            EnterEditMode();
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleGridPlacement();
        }
    }

    // Kept for the existing store buttons wired in the inspector.
    public void StartPlacingFurniture(GameObject furniturePrefab)
    {
        StartPlacingFurniture(furniturePrefab, null);
    }

    public void StartPlacingFurniture(GameObject furniturePrefab, Action onPlaced)
    {
        // Inspector-wired buttons can be left without a prefab.
        if (furniturePrefab == null)
        {
            Debug.LogError("Furniture prefab is null!");
            return;
        }
        CancelPlacement();
        currentPreview = Instantiate(furniturePrefab);
        currentPreview.name = furniturePrefab.name;
        onPreviewPlaced = onPlaced;

        if (currentPreview == null)
        {
            Debug.LogError("Failed to instantiate furniture preview!");
            return;
        }
        SetPreviewMaterial(validPlacementMaterial);
    }

    //update the current 
    private void UpdatePreviewPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        int mask = placementLayer | wallLayer | shelfLayer;

        if (Physics.Raycast(ray, out hit, 100f, mask))
        {
            Vector3 position = hit.point;
            Quaternion rotation = currentPreview.transform.rotation;
            Furniture furniture = currentPreview.GetComponent<Furniture>();

            // Adjust position based on furniture type.
            if (furniture != null)
            {
                switch (furniture.Type)
                {
                    case Furniture.FurnitureType.Wall:
                        if (((1 << hit.collider.gameObject.layer) & wallLayer) != 0)
                        {
                            position = SnapToWall(position, hit.normal);
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
                            isPlacementValid = false;
                            SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                    case Furniture.FurnitureType.Floor:
                    default:
                        if (((1 << hit.collider.gameObject.layer) & placementLayer) != 0)
                        {
                            position.y += GetBounds(currentPreview).extents.y;
                        }
                        else
                        {
                            isPlacementValid = false;
                            SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                }
            }

            if (useGridPlacement)
            {
                position = SnapToGrid(position);
            }

            currentPreview.transform.position = position;
            currentPreview.transform.rotation = rotation;
            isPlacementValid = IsValidPlacement(position);
            //SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
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
        // Shift the object so that it aligns with the wall.
        position += normal * GetBounds(currentPreview).extents.z;
        return position;
    }

    private Vector3 SnapToShelf(Vector3 position)
    {
        // Adjust position to stand straight on a shelf.
        position.y += GetBounds(currentPreview).extents.y;
        return position;
    }

    // Checks placement validity using overlap detection.
    private bool IsValidPlacement(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapBox(
            position,
            GetBounds(currentPreview).extents,
            currentPreview.transform.rotation,
            furnitureLayer
        );

        bool isValid = colliders.Length == 0;
        foreach (GameObject placedObj in placedObjects)
        {
            if (GetBounds(placedObj).Intersects(GetBounds(currentPreview)))
            {
                isValid = false;
                break;
            }
        }
        return isValid;
    }
    // Handles placement input.
    private void HandlePlacement()
    {
        if (IsPointerOverUI()) return;
        if (Input.GetMouseButtonDown(0) && isPlacementValid)
        {
            PlaceObject();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }
    // Handles rotation for the preview or selected object.
    private void HandleRotationAndFlipping()
    {
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (Input.GetKeyDown(KeyCode.R) && !alt)
        {
            if (currentPreview != null)
            {
                currentPreview.transform.Rotate(Vector3.up, -90f);
            }
            else if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.up, -90f);
            }
        }

        if (alt && Input.GetKeyDown(KeyCode.R))
        {
            if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.right, 90f);
            }
        }
    }
    // Instantiates the preview as a placed object.
    private void PlaceObject()
    {
        Vector3 position = currentPreview.transform.position;
        GameObject placedObject = Instantiate(currentPreview, position, currentPreview.transform.rotation);
        placedObject.name = currentPreview.name;
        placedObjects.Add(placedObject);
        RecordAction(placedObject);
        ResetPreviewMaterial(placedObject);
        PlayPlacementEffect(position);
        Destroy(currentPreview);
        currentPreview = null;
        Action placed = onPreviewPlaced;
        onPreviewPlaced = null;
        placed?.Invoke();

        Debug.Log("Placed object: " + placedObject.name + "; total placed: " + placedObjects.Count);
    }
    
    private void CancelPlacement()
    {
        onPreviewPlaced = null;
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    private void SetPreviewMaterial(Material material)
    {
        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            //renderer.material = material;
        }
    }

    // Resets the material of a placed object (after placement).
    private void ResetPreviewMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            //renderer.material = validPlacementMaterial;
        }
    }

    // Handle edit mode input: selection and dragging.
    private void HandleEditMode()
    {
        if (IsPointerOverUI()) return;

        // Selection: On mouse button down, try to select an object.
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                GameObject hitObject = hit.collider.gameObject;
                GameObject rootObject = hitObject.transform.root.gameObject;
                if (placedObjects.Contains(rootObject))
                {
                    selectedObject = rootObject;
                    Debug.Log("Selected object for editing: " + selectedObject.name);
                }
            }
        }
        // Dragging: While holding down the mouse, move the selected object.
        if (selectedObject != null && Input.GetMouseButton(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, placementLayer))
            {
                Vector3 newPosition = hit.point;
                if (useGridPlacement)
                {
                    newPosition = SnapToGrid(newPosition);
                }
                selectedObject.transform.position = newPosition;
            }
        }
    }

    // Handle normal object selection (supports double-click to trigger edit mode).
    private void HandleObjectSelection()
    {
        if (IsPointerOverUI()) return;
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                GameObject hitObject = hit.collider.gameObject;
                GameObject rootObject = hitObject.transform.root.gameObject;
                if (placedObjects.Contains(rootObject))
                {
                    // If double-clicked within threshold, enter edit mode.
                    if (Time.time - lastClickTime <= doubleClickThreshold)
                    {
                        isEditMode = true;
                        selectedObject = rootObject;
                        Debug.Log("Entering Edit Mode on object: " + rootObject.name);
                    }
                    lastClickTime = Time.time;
                }
            }
        }
        // Right-click to delete an object.
        else if (Input.GetMouseButtonDown(1))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                GameObject hitObject = hit.collider.gameObject;
                DeleteObject(hitObject.transform.root.gameObject);
            }
        }
    }

    // Attempts to enter edit mode based on a raycast hit from the mouse position.
    private void EnterEditMode()
    {
        isEditMode = !isEditMode; // Toggle edit mode state
        if (!isEditMode) selectedObject = null;
        Debug.Log("Edit Mode: " + (isEditMode ? "Enabled" : "Disabled"));


    }

    private void DeleteObject(GameObject obj)
    {
        if (placedObjects.Contains(obj))
        {
            obj.SetActive(false);
            placedObjects.Remove(obj);
            RecordAction(obj);
        }
    }

    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            GameObject lastObject = undoStack.Pop();
            redoStack.Push(lastObject);
            ToggleActive(lastObject);
        }
    }

    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            GameObject lastObject = redoStack.Pop();
            undoStack.Push(lastObject);
            ToggleActive(lastObject);
        }
    }

    // Every place/delete is reversed by flipping the object's active state; keep placedObjects in sync
    // so hidden objects stop blocking placement and restored ones can be selected again.
    private void ToggleActive(GameObject obj)
    {
        obj.SetActive(!obj.activeSelf);
        if (obj.activeSelf) placedObjects.Add(obj);
        else placedObjects.Remove(obj);
        if (selectedObject == obj && !obj.activeSelf) selectedObject = null;
    }

    private void RecordAction(GameObject obj)
    {
        undoStack.Push(obj);
        redoStack.Clear();
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // World bounds from colliders (falling back to renderers) anywhere in the object's hierarchy.
    private static Bounds GetBounds(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            foreach (Collider c in colliders) b.Encapsulate(c.bounds);
            return b;
        }
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
        return new Bounds(obj.transform.position, Vector3.zero);
    }

    // Plays a particle effect at the specified position.
    private void PlayPlacementEffect(Vector3 position)
    {
        if (placementParticlePrefab != null)
        {
            GameObject particleEffect = Instantiate(placementParticlePrefab, position, Quaternion.identity);
            ParticleSystem particleSystem = particleEffect.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
            Destroy(particleEffect, 2f);
        }
    }

    // Snaps the given position to the defined grid.
    private Vector3 SnapToGrid(Vector3 position)
    {
        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;
        return position;
    }

    // Toggles the grid-based placement.
    private void ToggleGridPlacement()
    {
        useGridPlacement = !useGridPlacement;
        Debug.Log("Grid placement " + (useGridPlacement ? "enabled" : "disabled"));
    }
}
