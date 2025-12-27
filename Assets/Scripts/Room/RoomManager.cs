using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    //color picker

    [Header("Layers")]
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private LayerMask furnitureLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask shelfLayer;

    [Header("Placement")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private bool useGridPlacement = true;

    [Header("Visuals")]
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private GameObject placementParticlePrefab;

    private Camera mainCamera;

    private GameObject currentPreview;
    private GameObject selectedObject;

    private bool isPlacementValid;
    private bool isEditMode;

    private Material originalMaterial;

    private readonly List<GameObject> placedObjects = new();
    private readonly Stack<GameObject> undoStack = new();
    private readonly Stack<GameObject> redoStack = new();

    private float lastClickTime;
    private const float DoubleClickThreshold = 0.3f;


    private void Awake()
    {
        mainCamera = Camera.main;

        if (!ValidateSetup())
        {
            enabled = false;
        }
    }

    private void Update()
    {
        HandleInput();

        if (currentPreview != null)
        {
            UpdatePreview();
            HandlePlacementInput();
        }
        else if (isEditMode)
        {
            HandleEditMode();
        }
        else
        {
            HandleSelection();
        }
    }

    private bool ValidateSetup()
    {
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera not found.");
            return false;
        }

        if (!validPlacementMaterial || !invalidPlacementMaterial)
        {
            Debug.LogError("Placement materials missing.");
            return false;
        }

        if (!placementParticlePrefab)
        {
            Debug.LogError("Placement particle prefab missing.");
            return false;
        }

        return true;
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Z)) Undo();
        if (Input.GetKeyDown(KeyCode.Y)) Redo();
        if (Input.GetKeyDown(KeyCode.E)) ToggleEditMode();
        if (Input.GetKeyDown(KeyCode.G)) ToggleGridPlacement();
        if (Input.GetKeyDown(KeyCode.R)) RotateTarget();
    }



    public void StartPlacingFurniture(GameObject prefab)
    {
        CancelPlacement();

        currentPreview = Instantiate(prefab);
        originalMaterial = currentPreview.GetComponentInChildren<Renderer>().material;
    }

    private void UpdatePreview()
    {
        if (!RaycastFromMouse(out RaycastHit hit, placementLayer | wallLayer | shelfLayer))
        {
            isPlacementValid = false;
            return;
        }

        Vector3 position = hit.point;
        Furniture furniture = currentPreview.GetComponent<Furniture>();
        Collider previewCollider = currentPreview.GetComponent<Collider>();

        if (!IsSurfaceValid(furniture, hit))
        {
            SetPreviewMaterial(invalidPlacementMaterial);
            isPlacementValid = false;
            return;
        }

        position = ApplySurfaceSnap(furniture, hit, previewCollider);

        if (useGridPlacement)
            position = SnapToGrid(position);

        currentPreview.transform.position = position;

        isPlacementValid = IsValidPlacement(previewCollider);
        SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
    }

    private bool IsSurfaceValid(Furniture furniture, RaycastHit hit)
    {
        int layerBit = 1 << hit.collider.gameObject.layer;

        return furniture.Type switch
        {
            Furniture.FurnitureType.Wall => (layerBit & wallLayer) != 0,
            Furniture.FurnitureType.Shelf => (layerBit & shelfLayer) != 0,
            _ => (layerBit & placementLayer) != 0,
        };
    }

    private Vector3 ApplySurfaceSnap(Furniture furniture, RaycastHit hit, Collider col)
    {
        Vector3 pos = hit.point;

        switch (furniture.Type)
        {
            case Furniture.FurnitureType.Wall:
                pos += hit.normal * col.bounds.extents.z;
                break;

            case Furniture.FurnitureType.Shelf:
            case Furniture.FurnitureType.Floor:
                pos.y += col.bounds.extents.y;
                break;
        }

        return pos;
    }

    private bool IsValidPlacement(Collider previewCollider)
    {
        Collider[] overlaps = Physics.OverlapBox(
            previewCollider.bounds.center,
            previewCollider.bounds.extents,
            previewCollider.transform.rotation,
            furnitureLayer
        );

        return overlaps.Length == 0;
    }

    private void HandlePlacementInput()
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
        GameObject placed = Instantiate(
            currentPreview,
            currentPreview.transform.position,
            currentPreview.transform.rotation
        );
        placed.layer = LayerMask.NameToLayer("Furniture");
        ResetMaterial(placed);
        placedObjects.Add(placed);
        undoStack.Push(placed);
        PlayPlacementEffect(placed.transform.position);
        Destroy(currentPreview);
        currentPreview = null;
    }

    private void CancelPlacement()
    {
        if (currentPreview)
            Destroy(currentPreview);
        currentPreview = null;
    }


    private void HandleSelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (!RaycastFromMouse(out RaycastHit hit, ~0)) return;

        GameObject root = hit.collider.transform.root.gameObject;
        if (!placedObjects.Contains(root)) return;

        if (Time.time - lastClickTime <= DoubleClickThreshold)
        {
            selectedObject = root;
            isEditMode = true;
        }

        lastClickTime = Time.time;
    }

    private void HandleEditMode()
    {
        if (selectedObject == null) return;

        if (Input.GetMouseButton(0) &&
            RaycastFromMouse(out RaycastHit hit, placementLayer))
        {
            Vector3 pos = hit.point;
            if (useGridPlacement)
                pos = SnapToGrid(pos);

            selectedObject.transform.position = pos;
        }
    }

    private void ToggleEditMode()
    {
        isEditMode = !isEditMode;
        selectedObject = null;
    }


    private void Undo()
    {
        if (undoStack.Count == 0) return;

        GameObject obj = undoStack.Pop();
        obj.SetActive(!obj.activeSelf);
        redoStack.Push(obj);
    }

    private void Redo()
    {
        if (redoStack.Count == 0) return;

        GameObject obj = redoStack.Pop();
        obj.SetActive(!obj.activeSelf);
        undoStack.Push(obj);
    }


    private bool RaycastFromMouse(out RaycastHit hit, LayerMask mask)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out hit, 100f, mask);
    }

    private Vector3 SnapToGrid(Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
        pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        return pos;
    }

    private void RotateTarget()
    {
        Transform target = currentPreview
            ? currentPreview.transform
            : selectedObject?.transform;

        if (target != null)
            target.Rotate(Vector3.up, -90f);
    }

    private void ToggleGridPlacement()
    {
        useGridPlacement = !useGridPlacement;
    }

    private void SetPreviewMaterial(Material mat)
    {
        foreach (Renderer r in currentPreview.GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    private void ResetMaterial(GameObject obj)
    {
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            r.material = originalMaterial;
    }

    private void PlayPlacementEffect(Vector3 position)
    {
        GameObject fx = Instantiate(placementParticlePrefab, position, Quaternion.identity);
        Destroy(fx, 2f);
    }

    //color change script update the materials
    private void ColorPickerUpdateWalls()
    {
        
    }
    private string GetColorRGB()
    {
        
        return "";
    }
}

