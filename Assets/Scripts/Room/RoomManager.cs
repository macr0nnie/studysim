using UnityEngine;
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
    [SerializeField] private bool useGridPlacement = true; // Toggle for grid-based placement

    // Materials and effects
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;

    [SerializeField] private GameObject placementParticlePrefab; // Particle effect prefab

    // Runtime data
    private List<GameObject> placedObjects = new List<GameObject>(); // Tracks placed objects
    private GameObject currentPreview;
    private GameObject selectedObject;
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false; // Start in placement mode

    // Undo/Redo stacks
    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();

    // For double-click detection in object selection
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
        // If a preview object exists, update its position and allow placement/rotation.
        if (currentPreview != null)
        {
            UpdatePreviewPosition();
            HandlePlacement();
            HandleRotationAndFlipping();
        }
        // Else, if in edit mode, allow editing (selection & dragging).
        else if (isEditMode)
        {
            HandleEditMode();
        }
        // Otherwise, handle standard object selection.
        else
        {
            HandleObjectSelection();
        }

        // Undo/Redo input
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Undo();
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            Redo();
        }

        // Press E to attempt to enter edit mode
        if (Input.GetKeyDown(KeyCode.E))
        {
            EnterEditMode();
        }

        // Toggle grid placement
        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleGridPlacement();
        }
    }

    // Called by UI or other scripts to start placing a furniture object.
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
    }

    // Updates the preview object's position based on a raycast from the mouse.
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
                           // SetPreviewMaterial(invalidPlacementMaterial);
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
                           // SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                    case Furniture.FurnitureType.Floor:
                    default:
                        if (((1 << hit.collider.gameObject.layer) & placementLayer) != 0)
                        {
                            position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
                        }
                        else
                        {
                            isPlacementValid = false;
                          // SetPreviewMaterial(invalidPlacementMaterial);
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
           // SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
        }
        else
        {
            Vector3 position = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            currentPreview.transform.position = position;
            isPlacementValid = false;
          //  SetPreviewMaterial(invalidPlacementMaterial);
        }
    }

    private Vector3 SnapToWall(Vector3 position, Vector3 normal)
    {
        // Shift the object so that it aligns with the wall.
        position += normal * currentPreview.GetComponent<Collider>().bounds.extents.z;
        return position;
    }

    private Vector3 SnapToShelf(Vector3 position)
    {
        // Adjust position to stand straight on a shelf.
        position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
        return position;
    }

    // Checks placement validity using overlap detection.
    private bool IsValidPlacement(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapBox(
            position,
            currentPreview.GetComponent<Collider>().bounds.extents,
            currentPreview.transform.rotation,
            furnitureLayer
        );

        bool isValid = colliders.Length == 0;
        foreach (GameObject placedObj in placedObjects)
        {
            if (placedObj.GetComponent<Collider>().bounds.Intersects(currentPreview.GetComponent<Collider>().bounds))
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
        if (Input.GetKeyDown(KeyCode.R))
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

        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.R))
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
        placedObjects.Add(placedObject);
        undoStack.Push(placedObject);
        //ResetPreviewMaterial(placedObject);
        PlayPlacementEffect(position);
        Destroy(currentPreview);
        currentPreview = null;

        Debug.Log("Placed object: " + placedObject.name + "; total placed: " + placedObjects.Count);
    }

    private void CancelPlacement()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

  

    // Handle edit mode input: selection and dragging.
    private void HandleEditMode()
    {
        Debug.Log("Edit Mode Active");

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
                Debug.Log("Moving object to: " + newPosition);
            }
        }
    }

    // Handle normal object selection (supports double-click to trigger edit mode).
    private void HandleObjectSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            int mask = placementLayer | wallLayer | shelfLayer;
            if (Physics.Raycast(ray, out hit, 100f, mask))
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
            int mask = placementLayer | wallLayer | shelfLayer;
            if (Physics.Raycast(ray, out hit, 100f, mask))
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
        Debug.Log("Edit Mode: " + (isEditMode ? "Enabled" : "Disabled"));


    }

    private void DeleteObject(GameObject obj)
    {
        if (placedObjects.Contains(obj))
        {
            placedObjects.Remove(obj);
            undoStack.Push(obj);
            obj.SetActive(false);
        }
    }

    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            GameObject lastObject = undoStack.Pop();
            redoStack.Push(lastObject);
            lastObject.SetActive(!lastObject.activeSelf);
        }
    }

    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            GameObject lastObject = redoStack.Pop();
            undoStack.Push(lastObject);
            lastObject.SetActive(!lastObject.activeSelf);
        }
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
