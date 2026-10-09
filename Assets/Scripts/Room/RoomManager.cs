using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
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
    private Renderer[] previewRenderers = new Renderer[0];
    private MaterialPropertyBlock previewTint;
    private bool? previewTintValid; // last tint applied, so the block is only rewritten when validity changes

    // Reused by GetBounds so placement checks don't allocate every frame.
    private static readonly Collider[] overlapBuffer = new Collider[1]; // only need to know if anything overlaps
    private static readonly List<Collider> colliderBuffer = new List<Collider>();
    private static readonly List<Renderer> rendererBuffer = new List<Renderer>();
    static readonly Color ValidTint = new Color(0.55f, 1f, 0.6f, 1f);
    static readonly Color InvalidTint = new Color(1f, 0.45f, 0.45f, 1f);
    private GameObject selectedObject;
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false;

    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();

    private float lastClickTime;
    private const float doubleClickThreshold = 0.3f;

    // Bought pieces and what was paid, so removing one refunds it and the room can be saved and rebuilt.
    private readonly Dictionary<GameObject, FurnitureItem> boughtItems = new Dictionary<GameObject, FurnitureItem>();
    private readonly Dictionary<string, GameObject> sceneFurniture = new Dictionary<string, GameObject>();
    private FurnitureItem previewItem;
    private PlayerCurrency currency;
    private const string SaveKey = "RoomLayout";
    private bool dragging, grabbed;
    private Vector3 grabOffset;
    private MaterialPropertyBlock highlight;
    static readonly Color SelectedTint = new Color(1f, 0.9f, 0.6f, 1f);

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
            enabled = false;
            return;
        }

        // The preview is tinted with a property block now and the particle effect is optional,
        // so neither missing asset should switch off building and edit mode.
        if (placementParticlePrefab == null) Debug.LogWarning("RoomManager: no placement particle effect assigned.");

        currency = FindFirstObjectByType<PlayerCurrency>();
        // Furniture already in the room at startup can be moved and deleted like bought furniture.
        foreach (Furniture f in FindObjectsByType<Furniture>(FindObjectsSortMode.None))
        {
            placedObjects.Add(f.gameObject);
            sceneFurniture[ScenePath(f.transform)] = f.gameObject;
        }
        LoadRoom();
    }

    private void OnApplicationQuit() => SaveRoom();

    private void Update()
    {
        // Letters typed into a text field (planner, etc.) must not also fire room shortcuts.
        bool typing = UIKit.Typing();

        if (currentPreview != null)
        {
            UpdatePreviewPosition();
            HandlePlacement();
            if (!typing) HandleRotationAndFlipping();
        }
        else if (isEditMode)
        {
            HandleEditMode();
            if (typing) return;
            HandleRotationAndFlipping();
            if (selectedObject != null && (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)))
                DeleteObject(selectedObject);
            if (Input.GetKeyDown(KeyCode.Escape)) EnterEditMode();
        }

        else
        {
            HandleObjectSelection();
        }

        if (typing) return;

        // Save once a drag or rotation in edit mode is finished, not every frame.
        if (isEditMode && selectedObject != null && (Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.R))) SaveRoom();

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

    // Store purchases: charged when placed, refunded when removed, and remembered between sessions.
    public void StartPlacingFurniture(FurnitureItem item)
    {
        if (item == null) return;
        StartPlacingFurniture(item.prefab, null);
        previewItem = item;
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
        previewRenderers = currentPreview.GetComponentsInChildren<Renderer>();
        previewTintValid = null;

        if (currentPreview == null)
        {
            Debug.LogError("Failed to instantiate furniture preview!");
            return;
        }
        SetPreviewTint(true);
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
                            SetPreviewTint(false);
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
                            SetPreviewTint(false);
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
                            SetPreviewTint(false);
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
            SetPreviewTint(isPlacementValid);
        }
        else
        {
            Vector3 position = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            currentPreview.transform.position = position;
            isPlacementValid = false;
            SetPreviewTint(false);
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
        Bounds previewBounds = GetBounds(currentPreview);
        if (Physics.OverlapBoxNonAlloc(position, previewBounds.extents, overlapBuffer, currentPreview.transform.rotation, furnitureLayer) > 0)
            return false;
        foreach (GameObject placedObj in placedObjects)
        {
            if (placedObj.activeInHierarchy && GetBounds(placedObj).Intersects(previewBounds))
                return false;
        }
        return true;
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
        if (previewItem != null && previewItem.price > 0 && (currency == null || !currency.SpendCoins(previewItem.price)))
            return; // can't afford it (any more): keep the preview so the player can cancel
        Vector3 position = currentPreview.transform.position;
        GameObject placedObject = Instantiate(currentPreview, position, currentPreview.transform.rotation);
        placedObject.name = currentPreview.name;
        placedObjects.Add(placedObject);
        if (previewItem != null) boughtItems[placedObject] = previewItem;
        previewItem = null;
        RecordAction(placedObject);
        ResetPreviewMaterial(placedObject);
        StartCoroutine(PopIn(placedObject.transform));
        PlayPlacementEffect(position);
        Destroy(currentPreview);
        currentPreview = null;
        Action placed = onPreviewPlaced;
        onPreviewPlaced = null;
        placed?.Invoke();
        SaveRoom();

        Debug.Log("Placed object: " + placedObject.name + "; total placed: " + placedObjects.Count);
    }
    
    private void CancelPlacement()
    {
        onPreviewPlaced = null;
        previewItem = null;
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    // Tints the preview green/red with a property block, so the furniture keeps its own materials and textures.
    private void SetPreviewTint(bool valid)
    {
        if (previewTintValid == valid) return;
        previewTintValid = valid;
        if (previewTint == null) previewTint = new MaterialPropertyBlock();
        Color tint = valid ? ValidTint : InvalidTint;
        previewTint.SetColor("_BaseColor", tint); // URP Lit
        previewTint.SetColor("_Color", tint);     // built-in/legacy shaders
        foreach (Renderer renderer in previewRenderers)
            if (renderer != null) renderer.SetPropertyBlock(previewTint);
    }

    // Clears the preview tint from a placed object.
    private void ResetPreviewMaterial(GameObject obj)
    {
        foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>())
            renderer.SetPropertyBlock(null);
    }

    // Small squash-and-settle so a placed piece feels like it lands.
    private static IEnumerator PopIn(Transform target)
    {
        Vector3 scale = target.localScale;
        for (float t = 0; t < 1 && target != null; t += Time.deltaTime / 0.22f)
        {
            float s = 1 + Mathf.Sin(t * Mathf.PI) * 0.12f * (1 - t); // up to ~6% overshoot, back to 1
            target.localScale = new Vector3(scale.x * (2 - s), scale.y * s, scale.z * (2 - s));
            yield return null;
        }
        if (target != null) target.localScale = scale;
    }

    // Edit mode: click a piece to select it (it glows), drag it to move it along its surface,
    // R rotates, Delete removes, Esc leaves. Clicking empty space deselects.
    private void HandleEditMode()
    {
        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI()) return;
            GameObject hitRoot = null;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f)) hitRoot = FurnitureRoot(hit.collider.gameObject);
            Select(hitRoot != null && placedObjects.Contains(hitRoot) ? hitRoot : null);
            // Only a press that starts on the piece drags it, so clicking elsewhere can't teleport it.
            dragging = selectedObject != null;
            grabbed = false;
        }
        if (dragging && selectedObject != null && Input.GetMouseButton(0)) DragSelected();
    }

    private void DragSelected()
    {
        Furniture furniture = selectedObject.GetComponent<Furniture>();
        Furniture.FurnitureType type = furniture != null ? furniture.Type : Furniture.FurnitureType.Floor;
        int mask = type == Furniture.FurnitureType.Wall ? wallLayer
            : type == Furniture.FurnitureType.Shelf ? shelfLayer | placementLayer
            : placementLayer;
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, mask)) return;

        Vector3 position = hit.point;
        Bounds bounds = GetBounds(selectedObject);
        if (type == Furniture.FurnitureType.Wall)
            position += hit.normal * bounds.extents.z;
        else
            position.y += selectedObject.transform.position.y - bounds.min.y; // rest its bottom on the surface
        // Keep the point you grabbed under the cursor instead of jumping the piece's centre to it.
        if (!grabbed)
        {
            grabOffset = selectedObject.transform.position - position;
            grabOffset.y = 0;
            grabbed = true;
        }
        position += grabOffset;
        if (useGridPlacement) position = SnapToGrid(position);
        selectedObject.transform.position = position;
    }

    // Warm glow on the selected piece so it's obvious what edit mode will move.
    private void Select(GameObject obj)
    {
        if (selectedObject == obj) return;
        if (selectedObject != null)
            foreach (Renderer r in selectedObject.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(null);
        selectedObject = obj;
        if (obj == null) return;
        if (highlight == null)
        {
            highlight = new MaterialPropertyBlock();
            highlight.SetColor("_BaseColor", SelectedTint);
            highlight.SetColor("_Color", SelectedTint);
        }
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(highlight);
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
                GameObject rootObject = FurnitureRoot(hitObject);
                if (placedObjects.Contains(rootObject))
                {
                    // If double-clicked within threshold, enter edit mode.
                    if (Time.time - lastClickTime <= doubleClickThreshold)
                    {
                        isEditMode = true;
                        Select(rootObject);
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
                DeleteObject(FurnitureRoot(hitObject));
            }
        }
    }

    public bool IsEditMode => isEditMode;
    public void ToggleEditMode() => EnterEditMode(); // for the HUD's Edit button

    // Attempts to enter edit mode based on a raycast hit from the mouse position.
    private void EnterEditMode()
    {
        isEditMode = !isEditMode; // Toggle edit mode state
        if (!isEditMode) Select(null);
        Debug.Log("Edit Mode: " + (isEditMode ? "Enabled" : "Disabled"));


    }

    private void DeleteObject(GameObject obj)
    {
        if (placedObjects.Contains(obj))
        {
            obj.SetActive(false);
            placedObjects.Remove(obj);
            if (selectedObject == obj) Select(null);
            Refund(obj);
            RecordAction(obj);
            SaveRoom();
        }
    }

    public void Undo()
    {
        if (undoStack.Count > 0 && ToggleActive(undoStack.Peek()))
            redoStack.Push(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count > 0 && ToggleActive(redoStack.Peek()))
            undoStack.Push(redoStack.Pop());
    }

    // Every place/delete is reversed by flipping the object's active state; keep placedObjects in sync
    // so hidden objects stop blocking placement and restored ones can be selected again. Bringing a bought
    // piece back charges for it again (and fails if the player can't pay); hiding one refunds it.
    private bool ToggleActive(GameObject obj)
    {
        bool show = !obj.activeSelf;
        if (show && boughtItems.TryGetValue(obj, out FurnitureItem item) && item.price > 0
            && (currency == null || !currency.SpendCoins(item.price)))
            return false;
        if (!show) Refund(obj);
        obj.SetActive(show);
        if (show) placedObjects.Add(obj);
        else placedObjects.Remove(obj);
        if (selectedObject == obj && !show) Select(null);
        SaveRoom();
        return true;
    }

    private void Refund(GameObject obj)
    {
        if (currency != null && boughtItems.TryGetValue(obj, out FurnitureItem item)) currency.AddCoins(item.price);
    }

    // ---------- saving the room ----------

    [Serializable]
    private class RoomSave
    {
        public List<SavedPiece> scene = new List<SavedPiece>();
        public List<SavedPiece> bought = new List<SavedPiece>();
    }

    [Serializable]
    private class SavedPiece
    {
        public string id;   // scene path, or the FurnitureItem asset name
        public int index;   // position in the catalog, to tell apart items that share a name
        public Vector3 position;
        public Quaternion rotation;
        public bool active;
    }

    private void SaveRoom()
    {
        var save = new RoomSave();
        foreach (var pair in sceneFurniture)
            if (pair.Value != null)
                save.scene.Add(Piece(pair.Key, -1, pair.Value));
        FurnitureCatalog catalog = Resources.Load<FurnitureCatalog>("FurnitureCatalog");
        foreach (var pair in boughtItems)
            if (pair.Key != null && pair.Key.activeSelf) // undone purchases were refunded
                save.bought.Add(Piece(pair.Value.name, catalog != null ? catalog.items.IndexOf(pair.Value) : -1, pair.Key));
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }

    private static SavedPiece Piece(string id, int index, GameObject go) => new SavedPiece
    {
        id = id, index = index, position = go.transform.position, rotation = go.transform.rotation, active = go.activeSelf,
    };

    private void LoadRoom()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return; // first run: keep the room as designed
        var save = JsonUtility.FromJson<RoomSave>(PlayerPrefs.GetString(SaveKey));
        if (save == null) return;

        foreach (SavedPiece piece in save.scene)
        {
            if (!sceneFurniture.TryGetValue(piece.id, out GameObject go)) continue;
            go.transform.SetPositionAndRotation(piece.position, piece.rotation);
            go.SetActive(piece.active);
            if (!piece.active) placedObjects.Remove(go);
        }

        FurnitureCatalog catalog = Resources.Load<FurnitureCatalog>("FurnitureCatalog");
        if (catalog == null) return;
        foreach (SavedPiece piece in save.bought)
        {
            FurnitureItem item = piece.index >= 0 && piece.index < catalog.items.Count && catalog.items[piece.index] != null
                && catalog.items[piece.index].name == piece.id
                ? catalog.items[piece.index]
                : catalog.items.Find(i => i != null && i.name == piece.id);
            if (item == null || item.prefab == null) continue; // removed from the store since
            GameObject go = Instantiate(item.prefab, piece.position, piece.rotation);
            go.name = item.prefab.name;
            placedObjects.Add(go);
            boughtItems[go] = item;
        }
    }

    // Stable id for furniture that ships in the scene.
    private static string ScenePath(Transform t)
    {
        // sibling index keeps two same-named pieces (e.g. two candles) apart
        string path = $"{t.name}#{t.GetSiblingIndex()}";
        for (Transform p = t.parent; p != null; p = p.parent) path = $"{p.name}#{p.GetSiblingIndex()}/{path}";
        return path;
    }

    private void RecordAction(GameObject obj)
    {
        undoStack.Push(obj);
        redoStack.Clear();
    }

    // The furniture piece a click hit, even when the collider is on one of its children.
    private static GameObject FurnitureRoot(GameObject hit)
    {
        Furniture furniture = hit.GetComponentInParent<Furniture>();
        return furniture != null ? furniture.gameObject : hit;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // World bounds from colliders (falling back to renderers) anywhere in the object's hierarchy.
    private static Bounds GetBounds(GameObject obj)
    {
        obj.GetComponentsInChildren(colliderBuffer);
        if (colliderBuffer.Count > 0)
        {
            Bounds b = colliderBuffer[0].bounds;
            foreach (Collider c in colliderBuffer) b.Encapsulate(c.bounds);
            return b;
        }
        obj.GetComponentsInChildren(rendererBuffer);
        if (rendererBuffer.Count > 0)
        {
            Bounds b = rendererBuffer[0].bounds;
            foreach (Renderer r in rendererBuffer) b.Encapsulate(r.bounds);
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
