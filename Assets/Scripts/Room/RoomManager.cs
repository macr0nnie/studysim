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

    [Tooltip("Visible wall meshes. Each gets a wall-layer collider so decor can hang on every wall. Found by name (Room_Walls) when empty.")]
    [SerializeField] private Renderer[] wallMeshes = new Renderer[0];
    [Tooltip("Visible floor meshes for the Paint drawer. When empty: floor-layer meshes without their own collider (the collider planes are helpers).")]
    [SerializeField] private Renderer[] floorMeshes = new Renderer[0];

    private List<GameObject> placedObjects = new List<GameObject>();
    private GameObject currentPreview;
    private Action onPreviewPlaced; // e.g. charge the player only once the item is actually placed
    private Renderer[] previewRenderers = new Renderer[0];
    private MaterialPropertyBlock previewTint;
    private bool? previewTintValid; // last tint applied, so the block is only rewritten when validity changes

    // Reused by GetBounds so placement checks don't allocate every frame.
    private static readonly Collider[] overlapBuffer = new Collider[16];
    private static readonly List<Collider> colliderBuffer = new List<Collider>();
    private static readonly List<Renderer> rendererBuffer = new List<Renderer>();
    static readonly Color ValidTint = new Color(0.55f, 1f, 0.6f, 1f);
    static readonly Color InvalidTint = new Color(1f, 0.45f, 0.45f, 1f);
    private GameObject selectedObject;
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false;

    // One undoable step: a piece put down or removed (states null: flip it back), or pieces moved, turned,
    // raised or resized (states: swap them back, which also makes the step redo-able).
    private class Step { public GameObject obj; public PieceState[] states; }
    private struct PieceState { public GameObject go; public Vector3 position; public Quaternion rotation; public float lift, size; }
    private Stack<Step> undoStack = new Stack<Step>();
    private Stack<Step> redoStack = new Stack<Step>();
    private Step pendingEdit; // the selected piece, and what stands on it, as they were when the current edit began

    // Pieces standing on the selected one while it's edited (a lamp on a desk): kept relative to it so they move,
    // turn and scale with it. foot/pivot/rotation are in the selected piece's frame at riderSize.
    private struct Rider { public GameObject go; public Vector3 foot, pivot; public Quaternion rotation; }
    private readonly List<Rider> riders = new List<Rider>();
    private float riderSize;
    private GameObject selectedSupport, hovered;
    private bool selectedValid = true;
    private Color shownTint;
    static readonly Color HoverTint = new Color(0.85f, 0.95f, 1f, 1f);


    // Bought pieces and what was paid, so removing one refunds it and the room can be saved and rebuilt.
    private readonly Dictionary<GameObject, FurnitureItem> boughtItems = new Dictionary<GameObject, FurnitureItem>();
    private readonly Dictionary<string, GameObject> sceneFurniture = new Dictionary<string, GameObject>();
    private FurnitureItem previewItem;
    private PlayerCurrency currency;
    private const string SaveKey = "RoomLayout";
    private bool dragging, grabbed;
    private Vector3 grabOffset;
    // Smooth following: the preview and dragged pieces glide toward where the cursor puts them instead of
    // jumping there. The target is what placement, validity and saving use.
    private Vector3 previewTarget, dragTarget;
    private bool previewOnSurface;
    private const float FollowRate = 22f;
    private static float Follow => 1f - Mathf.Exp(-FollowRate * Time.deltaTime);
    private MaterialPropertyBlock highlight;
    static readonly Color SelectedTint = new Color(1f, 0.9f, 0.6f, 1f);
    private float ceilingY;
    private Quaternion previewBase;
    private float previewSpin; // wall pieces: turn about the wall normal (R flips them)
    private Vector3 dragNormal;
    private GameObject previewSupport; // the desk, counter or shelf the preview stands on, if any
    private Bounds roomFloor;
    private bool hasRoomFloor;
    private static readonly RaycastHit[] surfaceHits = new RaycastHit[16];
    // The desk and chair a new game starts with: they can be moved and rotated but not deleted.
    // Per piece: extra height above where it rests, and size relative to its original scale.
    private readonly Dictionary<GameObject, float> lifts = new Dictionary<GameObject, float>();
    private readonly Dictionary<GameObject, float> sizes = new Dictionary<GameObject, float>();
    private readonly Dictionary<GameObject, Vector3> baseScales = new Dictionary<GameObject, Vector3>();
    private const float MaxLift = 3f, MinSize = 0.5f, MaxSize = 2f;
    private readonly HashSet<GameObject> starterPieces = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, Color> pieceColors = new Dictionary<GameObject, Color>();
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

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
        SetUpSurfaces();
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
        // While the room is falling apart (distracted), it can't be rearranged.
        if (Distraction.Busy)
        {
            if (currentPreview != null) CancelPlacement();
            if (isEditMode) EnterEditMode();
            dragging = false;
            return;
        }

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
            if (!typing)
            {
                HandleRotationAndFlipping();
                HandleHeightAndSize();
                if (selectedObject != null && Controls.Pressed(Controls.Act.Delete))
                    DeleteObject(selectedObject);
            }
            CarryRiders();
            RefreshGlow();
        }

        else
        {
            HandleObjectSelection();
        }

        if (typing) return;

        // Save once a drag or rotation in edit mode is finished, not every frame.
        if (isEditMode && selectedObject != null && (Controls.ClickUp || Controls.Released(Controls.Act.Raise)
            || Controls.Released(Controls.Act.Lower) || Controls.Released(Controls.Act.Grow) || Controls.Released(Controls.Act.Shrink)))
        {
            EndEdit();
            SaveRoom();
        }

        if (Controls.Pressed(Controls.Act.Undo))
        {
            Undo();
        }
        if (Controls.Pressed(Controls.Act.Redo))
        {
            Redo();
        }
        if (Controls.Pressed(Controls.Act.Edit))
        {
            EnterEditMode();
        }
        if (Controls.Pressed(Controls.Act.Grid))
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
        previewOnSurface = false;
        // Wall pieces get turned to face out of whichever wall they're on. A piece that is thin along x
        // (its face points along x) needs a quarter turn so its back lies against the wall.
        previewBase = currentPreview.transform.rotation;
        Physics.SyncTransforms();
        Bounds start = GetBounds(currentPreview);
        previewSpin = start.extents.x < start.extents.z * 0.8f ? 90f : 0f;

        if (currentPreview == null)
        {
            Debug.LogError("Failed to instantiate furniture preview!");
            return;
        }
        SetPreviewTint(true);
    }

    private void UpdatePreviewPosition()
    {
        Furniture.FurnitureType type = TypeOf(currentPreview);
        Transform preview = currentPreview.transform;
        Vector3 shown = preview.position;
        if (FindSurface(currentPreview, type, out RaycastHit hit, out previewSupport))
        {
            if (type == Furniture.FurnitureType.Wall)
                preview.rotation = Quaternion.LookRotation(Flat(hit.normal)) * Quaternion.Euler(0, previewSpin, 0) * previewBase;
            if (Pose(currentPreview, type, hit, Vector3.zero))
            {
                isPlacementValid = IsValidPlacement(currentPreview, previewSupport);
                SetPreviewTint(isPlacementValid);
                previewTarget = preview.position;
                if (previewOnSurface) preview.position = Vector3.Lerp(shown, previewTarget, Follow); // snap on arrival, glide after
                previewOnSurface = true;
                return;
            }
        }
        previewOnSurface = false;
        // Not over a surface this piece can go on: follow the cursor, red.
        currentPreview.transform.position = mainCamera.ScreenToWorldPoint(new Vector3(Controls.PointerPosition.x, Controls.PointerPosition.y, 10f));
        isPlacementValid = false;
        SetPreviewTint(false);
    }

    // The surface under the cursor for this piece. Floor and shelf pieces can also stand on top of another
    // placed piece (desk, counter, shelf) when they fit on it. Hits on the piece itself, walls in front
    // and anything else are skipped. support is the piece it stands on, or null.
    private bool FindSurface(GameObject piece, Furniture.FurnitureType type, out RaycastHit hit, out GameObject support)
    {
        support = null;
        hit = default;
        Ray ray = mainCamera.ScreenPointToRay(Controls.PointerPosition);
        int count = Physics.RaycastNonAlloc(ray, surfaceHits, 100f);
        Array.Sort(surfaceHits, 0, count, HitDistance);
        bool canStack = type == Furniture.FurnitureType.Floor || type == Furniture.FurnitureType.Shelf;
        Bounds own = canStack ? GetBounds(piece) : default;
        int mask = SurfaceMask(type);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = surfaceHits[i];
            if (h.collider.transform.IsChildOf(piece.transform)) continue;
            if (((1 << h.collider.gameObject.layer) & mask) != 0) { hit = h; return true; }
            if (!canStack || h.normal.y < 0.7f) continue;
            GameObject other = FurnitureRoot(h.collider.gameObject);
            if (!placedObjects.Contains(other) || other == piece || IsRider(other)) continue;
            Bounds top = GetBounds(other);
            if (own.extents.x > top.extents.x + 0.01f || own.extents.z > top.extents.z + 0.01f) continue; // too big to stand on it
            hit = h;
            support = other;
            return true;
        }
        return false;
    }

    private static readonly System.Collections.Generic.Comparer<RaycastHit> HitDistance =
        System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    private static Furniture.FurnitureType TypeOf(GameObject obj) =>
        obj.TryGetComponent(out Furniture furniture) ? furniture.Type : Furniture.FurnitureType.Floor;

    // Which surfaces a piece can be put on. Ceiling pieces aim at the floor and hang above that point.
    private int SurfaceMask(Furniture.FurnitureType type) => type switch
    {
        Furniture.FurnitureType.Wall => wallLayer.value,
        Furniture.FurnitureType.Shelf => shelfLayer.value | placementLayer.value,
        _ => placementLayer.value,
    };

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    // Moves a piece onto the surface a ray hit, centred on the hit point plus offset: floor and shelf pieces
    // rest their bottom on it, wall pieces sit flush with their back on the wall, ceiling pieces hang with
    // their top at the ceiling. The grid only snaps along the surface, never into or out of a wall.
    // Returns false where the piece can't go (the top edge of a wall).
    private bool Pose(GameObject obj, Furniture.FurnitureType type, RaycastHit hit, Vector3 offset)
    {
        Transform t = obj.transform;
        t.position = hit.point;
        Physics.SyncTransforms(); // collider bounds otherwise lag a frame behind the move (and any rotation)
        Bounds b = GetBounds(obj);
        Vector3 target = hit.point + offset;
        Vector3 position;
        switch (type)
        {
            case Furniture.FurnitureType.Wall:
                Vector3 n = hit.normal;
                if (Mathf.Abs(n.y) > 0.3f) return false;
                position = t.position + (target + n * Depth(b, n) - b.center);
                if (useGridPlacement)
                {
                    Vector3 snapped = SnapToGrid(position);
                    position = snapped - n * Vector3.Dot(snapped - position, n);
                }
                break;
            case Furniture.FurnitureType.Ceiling:
                position = t.position + new Vector3(target.x - b.center.x, ceilingY - b.max.y, target.z - b.center.z);
                if (useGridPlacement) position = SnapToGrid(position);
                break;
            default:
                position = t.position + new Vector3(target.x - b.center.x, hit.point.y - b.min.y, target.z - b.center.z);
                if (useGridPlacement) position = SnapToGrid(position);
                break;
        }
        if (lifts.TryGetValue(obj, out float lift)) position.y += lift;
        t.position = position;
        Physics.SyncTransforms();
        return true;
    }

    // How far a box reaches from its centre along a direction (half its thickness against a wall with that normal).
    private static float Depth(Bounds b, Vector3 n) => Mathf.Abs(n.x) * b.extents.x + Mathf.Abs(n.y) * b.extents.y + Mathf.Abs(n.z) * b.extents.z;

    // The wall a hanging piece is on: the nearest wall straight behind one of its four sides. normal points out of it.
    private bool WallBehind(Bounds b, out Vector3 normal)
    {
        normal = Vector3.zero;
        float best = float.MaxValue;
        foreach (Vector3 dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            if (Physics.Raycast(b.center, dir, out RaycastHit hit, b.extents.magnitude + 0.5f, wallLayer) && hit.distance < best)
            {
                best = hit.distance;
                normal = hit.normal;
            }
        return best < float.MaxValue;
    }

    // Decor needs a wall collider on every wall (the scene's WallPlane covers one), and hanging pieces
    // need to know how high the ceiling is.
    private void SetUpSurfaces()
    {
        if (wallMeshes.Length == 0)
        {
            GameObject walls = GameObject.Find("Room_Walls");
            if (walls != null && walls.TryGetComponent(out Renderer wallRenderer)) wallMeshes = new[] { wallRenderer };
        }
        int layer = LayerIndex(wallLayer);
        foreach (Renderer wall in wallMeshes)
        {
            if (wall == null || !wall.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
            if (!filter.sharedMesh.isReadable) { Debug.LogWarning($"{wall.name}: enable Read/Write on its model so decor can hang on it"); continue; }
            var surface = new GameObject("WallSurface") { layer = layer };
            surface.transform.SetParent(wall.transform, false);
            surface.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }

        ceilingY = float.MinValue;
        foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            if (((1 << c.gameObject.layer) & wallLayer) != 0) ceilingY = Mathf.Max(ceilingY, c.bounds.max.y);
        if (ceilingY == float.MinValue) ceilingY = 3f; // shortcut: no walls found, so assume a 3 m room; assign wallMeshes to fix

        if (floorMeshes.Length == 0)
        {
            var floors = new List<Renderer>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (((1 << r.gameObject.layer) & placementLayer) != 0 && r.GetComponent<Collider>() == null) floors.Add(r);
            floorMeshes = floors.ToArray();
        }
        foreach (Renderer floor in floorMeshes)
        {
            if (floor == null) continue;
            if (!hasRoomFloor) roomFloor = floor.bounds;
            else roomFloor.Encapsulate(floor.bounds);
            hasRoomFloor = true;
        }
    }

    private static int LayerIndex(LayerMask mask)
    {
        for (int i = 0; i < 32; i++)
            if ((mask.value & (1 << i)) != 0) return i;
        return 0;
    }

    // Overlap check on the piece's own bounds, shrunk a little so touching a neighbour, the wall or the
    // piece it stands on is fine. Floor and ceiling pieces must also stay over the room's floor.
    private bool IsValidPlacement(GameObject piece, GameObject support)
    {
        Bounds bounds = GetBounds(piece);
        if (hasRoomFloor && TypeOf(piece) != Furniture.FurnitureType.Wall
            && (bounds.min.x < roomFloor.min.x - 0.05f || bounds.max.x > roomFloor.max.x + 0.05f
                || bounds.min.z < roomFloor.min.z - 0.05f || bounds.max.z > roomFloor.max.z + 0.05f))
            return false;
        bounds.Expand(-0.04f);
        int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, overlapBuffer, Quaternion.identity, furnitureLayer);
        for (int i = 0; i < count; i++)
            if (!overlapBuffer[i].transform.IsChildOf(piece.transform)
                && (support == null || !overlapBuffer[i].transform.IsChildOf(support.transform))
                && !IsRider(FurnitureRoot(overlapBuffer[i].gameObject))) return false;
        foreach (GameObject placedObj in placedObjects)
        {
            if (placedObj != piece && placedObj != support && placedObj.activeInHierarchy && !IsRider(placedObj) && GetBounds(placedObj).Intersects(bounds))
                return false;
        }
        return true;
    }
    // Handles placement input.
    private void HandlePlacement()
    {
        if (IsPointerOverUI()) return;
        if (Controls.ClickDown && isPlacementValid)
        {
            PlaceObject();
        }
        else if (Controls.ClickDown)
        {
            UISound.Play(UISound.Cue.Denied); // red preview: nowhere to put it here
        }
        else if (Controls.RightClickDown)
        {
            CancelPlacement();
        }
    }
    // Handles rotation for the preview or selected object.
    private void HandleRotationAndFlipping()
    {
        bool alt = Controls.Alt;
        if (Controls.Pressed(Controls.Act.Rotate) && !alt)
        {
            // Wall pieces flip to face the other way; a quarter turn would stick them out of the wall.
            if (currentPreview != null)
            {
                if (TypeOf(currentPreview) == Furniture.FurnitureType.Wall) previewSpin += 180f;
                else currentPreview.transform.Rotate(Vector3.up, -90f, Space.World);
            }
            else RotateSelected();
        }

        if (alt && Controls.Pressed(Controls.Act.Rotate) && currentPreview == null && selectedObject != null)
            Turn(Quaternion.AngleAxis(90f, selectedObject.transform.right));
    }

    // Floor pieces turn a quarter; wall pieces flip to face the other way (a quarter turn would stick them out of the wall).
    public void RotateSelected()
    {
        if (selectedObject == null) return;
        Turn(Quaternion.AngleAxis(TypeOf(selectedObject) == Furniture.FurnitureType.Wall ? 180f : -90f, Vector3.up));
    }

    private void Turn(Quaternion turn)
    {
        bool own = pendingEdit == null; // turning mid-drag stays part of that drag's undo step
        BeginEdit();
        selectedObject.transform.rotation = turn * selectedObject.transform.rotation;
        Changed();
        if (!own) return;
        EndEdit();
        SaveRoom();
    }

    public bool SelectedHasLights => selectedObject != null && HasLights(selectedObject);

    // Edit mode swallows clicks (they select and drag), so a lamp is switched from the panel there.
    public void ToggleSelectedLights()
    {
        if (!SelectedHasLights) return;
        SetLights(selectedObject, !LightsOn(selectedObject));
        SaveRoom();
    }

    public void DeleteSelected()
    {
        if (selectedObject != null) DeleteObject(selectedObject);
    }

    public string SelectedName => selectedObject == null ? ""
        : boughtItems.TryGetValue(selectedObject, out FurnitureItem item) && !string.IsNullOrEmpty(item.displayName) ? item.displayName : selectedObject.name;

    // False while the selected piece overlaps something after a move, turn or resize (it glows red).
    public bool SelectedFits => selectedValid;
    // Hold the keys to raise/lower or grow/shrink the selected piece; the buttons in the HUD step it.
    private void HandleHeightAndSize()
    {
        if (selectedObject == null) return;
        if (Controls.Pressed(Controls.Act.Raise) || Controls.Pressed(Controls.Act.Lower)
            || Controls.Pressed(Controls.Act.Grow) || Controls.Pressed(Controls.Act.Shrink)) BeginEdit(); // ends when the key is let go
        float dt = Time.deltaTime;
        if (Controls.Held(Controls.Act.Raise)) NudgeHeight(0.6f * dt, false);
        if (Controls.Held(Controls.Act.Lower)) NudgeHeight(-0.6f * dt, false);
        if (Controls.Held(Controls.Act.Grow)) Resize(Mathf.Pow(1.8f, dt), false);
        if (Controls.Held(Controls.Act.Shrink)) Resize(Mathf.Pow(1.8f, -dt), false);
    }

    public float SelectedLift => selectedObject != null && lifts.TryGetValue(selectedObject, out float l) ? l : 0f;
    public float SelectedSize => selectedObject != null && sizes.TryGetValue(selectedObject, out float z) ? z : 1f;

    public void NudgeHeight(float delta, bool save = true)
    {
        if (selectedObject == null) return;
        bool wall = TypeOf(selectedObject) == Furniture.FurnitureType.Wall;
        float now = SelectedLift;
        float next = Mathf.Clamp(now + delta, wall ? -MaxLift : 0f, MaxLift);
        if (Mathf.Approximately(next, now)) return;
        if (save) BeginEdit();
        lifts[selectedObject] = next;
        selectedObject.transform.position += Vector3.up * (next - now);
        Changed();
        if (save) { EndEdit(); SaveRoom(); }
    }

    public void ResetHeight() => NudgeHeight(-SelectedLift);

    // Uniform scale about the piece's footing: floor and shelf pieces keep resting where they were, the rest keep their centre.
    public void Resize(float factor, bool save = true)
    {
        if (selectedObject == null) return;
        if (save) BeginEdit();
        SetSize(selectedObject, SelectedSize * factor);
        Changed();
        if (save) { EndEdit(); SaveRoom(); }
    }

    public void ResetSize() => Resize(1f / SelectedSize);

    private void SetSize(GameObject go, float size, bool keepFooting = true)
    {
        size = Mathf.Clamp(size, MinSize, MaxSize);
        if (!baseScales.ContainsKey(go)) baseScales[go] = go.transform.localScale;
        Physics.SyncTransforms();
        Bounds before = GetBounds(go);
        sizes[go] = size;
        go.transform.localScale = baseScales[go] * size;
        if (!keepFooting) return;
        Physics.SyncTransforms();
        Bounds after = GetBounds(go);
        Furniture.FurnitureType type = TypeOf(go);
        Vector3 shift = before.center - after.center;
        if (type == Furniture.FurnitureType.Floor || type == Furniture.FurnitureType.Shelf) shift.y = before.min.y - after.min.y;
        else if (type == Furniture.FurnitureType.Ceiling) shift.y = before.max.y - after.max.y;
        else if (type == Furniture.FurnitureType.Wall && WallBehind(before, out Vector3 n))
            shift += n * (Depth(after, n) - Depth(before, n)); // keep its back on the wall instead of growing into it
        go.transform.position += shift;
        Physics.SyncTransforms();
    }

    // Instantiates the preview as a placed object.
    private void PlaceObject()
    {
        if (previewItem != null && previewItem.price > 0 && (currency == null || !currency.SpendCoins(previewItem.price)))
        {
            UISound.Play(UISound.Cue.Denied);
            return; // can't afford it (any more): keep the preview so the player can cancel
        }
        Vector3 position = previewTarget; // where it was heading, not where the glide has got to
        GameObject placedObject = Instantiate(currentPreview, position, currentPreview.transform.rotation);
        placedObject.name = currentPreview.name;
        placedObjects.Add(placedObject);
        if (previewItem != null) boughtItems[placedObject] = previewItem;
        previewItem = null;
        RecordAction(placedObject);
        ResetPreviewMaterial(placedObject);
        if (HasLights(placedObject)) SetLights(placedObject, true, false); // the pack's lamps start switched off
        UISound.Play(boughtItems.TryGetValue(placedObject, out FurnitureItem paid) && paid.price > 0 ? UISound.Cue.Coin : UISound.Cue.Place);
        StartCoroutine(PopIn(placedObject.transform));
        PlayPlacementEffect(position);
        Destroy(currentPreview);
        currentPreview = null;
        Action placed = onPreviewPlaced;
        onPreviewPlaced = null;
        placed?.Invoke();
        SaveRoom();
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
    // R rotates, Delete removes, Tab leaves. Clicking empty space deselects.
    private void HandleEditMode()
    {
        if (Controls.ClickUp)
        {
            // Let go: settle exactly where the drag was heading before the room is saved. A spot where it
            // overlaps something sends it (and what stands on it) back where the drag started.
            if (dragging && grabbed && selectedObject != null)
            {
                bool moved = pendingEdit != null && pendingEdit.states[0].position != dragTarget;
                selectedObject.transform.position = dragTarget;
                Physics.SyncTransforms();
                CarryRiders();
                if (!selectedValid)
                {
                    EndEdit(true);
                    UISound.Play(UISound.Cue.Denied);
                }
                else if (moved) UISound.Play(UISound.Cue.Place);
            }
            dragging = false;
        }
        if (Controls.ClickDown)
        {
            if (IsPointerOverUI()) return;
            Select(PlacedObjectUnderCursor());
            // Only a press that starts on the piece drags it, so clicking elsewhere can't teleport it.
            dragging = selectedObject != null;
            grabbed = false;
            BeginEdit();
        }
        if (dragging && selectedObject != null && Controls.ClickHeld) DragSelected();
    }

    private void DragSelected()
    {
        Transform t = selectedObject.transform;
        Vector3 shown = t.position;
        if (grabbed) t.position = dragTarget; // work out the move from where the piece is heading
        MoveSelected(t);
        dragTarget = t.position;
        Physics.SyncTransforms();
        selectedValid = IsValidPlacement(selectedObject, selectedSupport);
        if (grabbed) t.position = Vector3.Lerp(shown, dragTarget, Follow);
    }

    private void MoveSelected(Transform t)
    {
        Furniture.FurnitureType type = TypeOf(selectedObject);
        if (!FindSurface(selectedObject, type, out RaycastHit hit, out GameObject support)) return;

        Vector3 before = t.position;
        Quaternion beforeRotation = t.rotation;
        // Dragged onto another wall: turn with it so it still faces into the room.
        if (type == Furniture.FurnitureType.Wall && grabbed && Vector3.Angle(dragNormal, hit.normal) > 1f)
            t.rotation = Quaternion.FromToRotation(Flat(dragNormal), Flat(hit.normal)) * t.rotation;
        if (!grabbed)
        {
            // Keep the point you grabbed under the cursor instead of jumping the piece's centre to it.
            if (!Pose(selectedObject, type, hit, Vector3.zero)) { t.SetPositionAndRotation(before, beforeRotation); return; }
            grabOffset = before - t.position;
            grabbed = true;
        }
        Vector3 offset = type == Furniture.FurnitureType.Wall
            ? grabOffset - hit.normal * Vector3.Dot(grabOffset, hit.normal) // slide along the wall
            : new Vector3(grabOffset.x, 0, grabOffset.z);
        if (!Pose(selectedObject, type, hit, offset)) t.SetPositionAndRotation(before, beforeRotation);
        else
        {
            dragNormal = hit.normal;
            selectedSupport = support;
        }
    }

    // Warm glow on the selected piece so it's obvious what edit mode will move.
    private void Select(GameObject obj)
    {
        if (selectedObject == obj) return;
        EndEdit();
        if (selectedObject != null) Glow(selectedObject, null);
        selectedObject = obj;
        selectedValid = true;
        selectedSupport = obj != null ? SupportOf(obj) : null;
        if (obj == null) return;
        if (obj == hovered) hovered = null;
        Glow(obj, shownTint = SelectedTint);
    }

    // Tints a piece with a property block (null clears it), leaving its own materials and paint alone.
    private void Glow(GameObject obj, Color? tint)
    {
        if (tint.HasValue)
        {
            if (highlight == null) highlight = new MaterialPropertyBlock();
            highlight.SetColor("_BaseColor", tint.Value);
            highlight.SetColor("_Color", tint.Value);
        }
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(tint.HasValue ? highlight : null);
    }

    // Edit mode: a faint glow on the piece under the cursor (what a click would pick up), and the selected
    // piece glows red while it overlaps something.
    private void RefreshGlow()
    {
        GameObject hover = dragging || IsPointerOverUI() ? null : PlacedObjectUnderCursor();
        if (hover == selectedObject) hover = null;
        if (hover != hovered)
        {
            if (hovered != null) Glow(hovered, null);
            hovered = hover;
            if (hover != null) Glow(hover, HoverTint);
        }
        if (selectedObject == null) return;
        Color tint = selectedValid ? SelectedTint : InvalidTint;
        if (tint != shownTint) Glow(selectedObject, shownTint = tint);
    }

    // After the selected piece moved, turned, rose or changed size: bring what stands on it along and re-check the fit.
    private void Changed()
    {
        Physics.SyncTransforms();
        CarryRiders();
        selectedValid = IsValidPlacement(selectedObject, selectedSupport);
    }

    // A floor or shelf piece standing on top of support (a lamp on a desk, books on a shelf). s is support's bounds.
    private bool StandsOn(GameObject piece, GameObject support, Bounds s)
    {
        if (piece == support || !piece.activeInHierarchy) return false;
        Furniture.FurnitureType type = TypeOf(piece);
        if (type != Furniture.FurnitureType.Floor && type != Furniture.FurnitureType.Shelf) return false;
        Bounds b = GetBounds(piece);
        return b.min.y > s.min.y + 0.05f && b.min.y < s.max.y + 0.05f
            && b.center.x > s.min.x && b.center.x < s.max.x && b.center.z > s.min.z && b.center.z < s.max.z;
    }

    // Whatever stood on a piece that is being removed falls to the floor below it instead of hanging in the air.
    private void DropRiders(GameObject parent)
    {
        Bounds s = GetBounds(parent);
        foreach (GameObject p in new List<GameObject>(placedObjects))
        {
            if (p == parent || !StandsOn(p, parent, s)) continue;
            Bounds b = GetBounds(p);
            if (Physics.Raycast(new Vector3(b.center.x, s.min.y + 0.1f, b.center.z), Vector3.down, out RaycastHit hit, 5f, placementLayer))
                p.transform.position += Vector3.up * (hit.point.y - b.min.y);
        }
        Physics.SyncTransforms();
    }

    private GameObject SupportOf(GameObject piece)
    {
        foreach (GameObject p in placedObjects)
            if (p != piece && p.activeInHierarchy && StandsOn(piece, p, GetBounds(p))) return p;
        return null;
    }

    private bool IsRider(GameObject go)
    {
        foreach (Rider r in riders)
            if (r.go == go) return true;
        return false;
    }

    private PieceState StateOf(GameObject go) => new PieceState
    {
        go = go, position = go.transform.position, rotation = go.transform.rotation,
        lift = lifts.TryGetValue(go, out float l) ? l : 0f, size = sizes.TryGetValue(go, out float z) ? z : 1f,
    };

    // Puts pieces back as recorded and returns how they were, so the same step can go the other way.
    private PieceState[] Swap(PieceState[] states)
    {
        var was = new PieceState[states.Length];
        for (int i = 0; i < states.Length; i++)
        {
            PieceState s = states[i];
            was[i] = s;
            if (s.go == null) continue;
            was[i] = StateOf(s.go);
            lifts[s.go] = s.lift;
            SetSize(s.go, s.size, false);
            s.go.transform.SetPositionAndRotation(s.position, s.rotation);
        }
        Physics.SyncTransforms();
        return was;
    }

    // Before the selected piece is moved, turned, raised or resized: remember it, and whatever stands on it, for
    // undo, and carry those pieces along until EndEdit.
    private void BeginEdit()
    {
        if (pendingEdit != null || selectedObject == null) return;
        riders.Clear();
        Physics.SyncTransforms();
        Transform t = selectedObject.transform;
        Quaternion inv = Quaternion.Inverse(t.rotation);
        Bounds s = GetBounds(selectedObject);
        var states = new List<PieceState> { StateOf(selectedObject) };
        // Pieces on the piece, then pieces on those (a lamp on a book on a desk).
        var carriers = new List<GameObject> { selectedObject };
        for (int c = 0; c < carriers.Count; c++)
        {
            Bounds cb = c == 0 ? s : GetBounds(carriers[c]);
            foreach (GameObject p in placedObjects)
            {
                if (p == selectedObject || carriers.Contains(p) || !StandsOn(p, carriers[c], cb)) continue;
                Bounds b = GetBounds(p);
                Vector3 foot = new Vector3(b.center.x, b.min.y, b.center.z);
                riders.Add(new Rider { go = p, foot = inv * (foot - t.position), pivot = inv * (p.transform.position - foot), rotation = inv * p.transform.rotation });
                states.Add(StateOf(p));
                carriers.Add(p);
            }
        }
        riderSize = SelectedSize;
        pendingEdit = new Step { states = states.ToArray() };
    }

    // Ends the current edit with an undo step if anything changed; revert puts everything back instead.
    private void EndEdit(bool revert = false)
    {
        if (pendingEdit == null) return;
        Step step = pendingEdit;
        pendingEdit = null;
        riders.Clear();
        if (revert)
        {
            Swap(step.states);
            selectedValid = true;
            return;
        }
        foreach (PieceState s in step.states)
        {
            if (s.go == null) continue;
            PieceState now = StateOf(s.go);
            if (now.position == s.position && now.rotation == s.rotation && now.lift == s.lift && now.size == s.size) continue;
            undoStack.Push(step);
            redoStack.Clear();
            return;
        }
    }

    private void CarryRiders()
    {
        if (riders.Count == 0 || selectedObject == null) return;
        Transform t = selectedObject.transform;
        float k = SelectedSize / riderSize;
        foreach (Rider r in riders)
        {
            if (r.go == null) continue;
            Vector3 foot = t.position + t.rotation * (r.foot * k);
            r.go.transform.SetPositionAndRotation(foot + t.rotation * r.pivot, t.rotation * r.rotation);
        }
    }

    // Handle normal object selection: click a lamp to switch it, right-click a piece to delete it.
    private void HandleObjectSelection()
    {
        if (IsPointerOverUI()) return;
        if (Controls.ClickDown)
        {
            GameObject rootObject = PlacedObjectUnderCursor();
            if (rootObject != null)
            {
                // Clicking a lamp switches it. Edit mode is entered only with Tab or the Edit button.
                if (HasLights(rootObject))
                {
                    SetLights(rootObject, !LightsOn(rootObject));
                    SaveRoom();
                }
            }
        }
        // Right-click to delete an object.
        else if (Controls.RightClickDown)
        {
            GameObject rootObject = PlacedObjectUnderCursor();
            if (rootObject != null) DeleteObject(rootObject);
        }
    }

    public bool IsEditMode => isEditMode;
    public bool IsDragging => dragging && selectedObject != null && Controls.ClickHeld; // a piece is being dragged right now
    public void ToggleEditMode() => EnterEditMode(); // for the HUD's Edit button

    // ---------- paint (used by the Paint drawer) ----------

    public GameObject SelectedPiece => selectedObject;
    public Renderer[] WallRenderers => wallMeshes;
    public Renderer[] FloorRenderers => floorMeshes;

    // Plain-coloured pieces can be painted; textured artwork (paintings, posters) and pieces marked
    // not colourable in the store can't.
    public bool CanPaint(GameObject piece)
    {
        if (piece == null) return false;
        if (boughtItems.TryGetValue(piece, out FurnitureItem item) && !item.colorable) return false;
        return Paintable(piece.GetComponentsInChildren<Renderer>());
    }

    public static bool Paintable(Renderer[] renderers)
    {
        if (renderers.Length == 0) return false;
        foreach (Renderer r in renderers)
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !(m.HasProperty("_BaseColor") || m.HasProperty("_Color"))) return false;
                Texture texture = m.mainTexture;
                if (texture != null && texture.width > 256) return false; // detailed artwork, not a small colour palette
            }
        return true;
    }

    public void PaintPiece(GameObject piece, Color? color)
    {
        if (!CanPaint(piece)) return;
        ApplyPieceColor(piece, color);
        SaveRoom();
    }

    public bool IsPainted(GameObject piece) => piece != null && pieceColors.ContainsKey(piece);

    private void ApplyPieceColor(GameObject piece, Color? color)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
        Tint(renderers, color);
        if (color.HasValue) pieceColors[piece] = color.Value;
        else pieceColors.Remove(piece);
        foreach (Renderer r in renderers) r.SetPropertyBlock(null); // drop the selection glow so the new colour shows
    }

    // Colours instance copies of the materials; null puts the original materials back.
    public void Tint(Renderer[] renderers, Color? color)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!originalMaterials.ContainsKey(r)) originalMaterials[r] = r.sharedMaterials;
            if (color == null)
            {
                r.sharedMaterials = originalMaterials[r];
                continue;
            }
            foreach (Material m in r.materials)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color.Value);
                if (m.HasProperty("_Color")) m.SetColor("_Color", color.Value);
            }
        }
    }

    // Swaps every material slot for one surface material (floors and walls); Tint(null) undoes it.
    public void SetSurfaceMaterial(Renderer[] renderers, Material material)
    {
        foreach (Renderer r in renderers)
        {
            if (r == null || material == null) continue;
            if (!originalMaterials.ContainsKey(r)) originalMaterials[r] = r.sharedMaterials;
            var slots = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            r.sharedMaterials = slots;
        }
    }

    // Attempts to enter edit mode based on a raycast hit from the mouse position.
    private void EnterEditMode()
    {
        isEditMode = !isEditMode; // Toggle edit mode state
        if (!isEditMode)
        {
            Select(null);
            if (hovered != null) Glow(hovered, null);
            hovered = null;
        }


    }

    private void DeleteObject(GameObject obj)
    {
        if (!CanRemove(obj)) return;
        if (placedObjects.Contains(obj))
        {
            DropRiders(obj);
            obj.SetActive(false);
            placedObjects.Remove(obj);
            if (selectedObject == obj) Select(null);
            Refund(obj);
            RecordAction(obj);
            SaveRoom();
        }
    }

    // A room always keeps at least one desk and one chair: any piece named or tagged so counts.
    private bool IsKind(GameObject go, string kind) => FurnitureRole.Is(go, ItemOf(go), kind);

    private bool CanRemove(GameObject obj)
    {
        foreach (string kind in new[] { "desk", "chair" })
        {
            if (!IsKind(obj, kind)) continue;
            bool another = false;
            foreach (GameObject other in placedObjects)
                if (other != obj && other.activeInHierarchy && IsKind(other, kind)) { another = true; break; }
            if (another) continue;
            FindFirstObjectByType<GameHUD>()?.ShowToast($"Your room needs a {kind}. Put another one down first, then you can remove this one.");
            return false;
        }
        return true;
    }

    public void Undo()
    {
        EndEdit();
        if (undoStack.Count > 0 && Apply(undoStack.Peek()))
            redoStack.Push(undoStack.Pop());
    }

    public void Redo()
    {
        EndEdit();
        if (redoStack.Count > 0 && Apply(redoStack.Peek()))
            undoStack.Push(redoStack.Pop());
    }

    private bool Apply(Step step)
    {
        if (step.states == null) return ToggleActive(step.obj);
        step.states = Swap(step.states);
        selectedValid = true;
        if (selectedObject != null) selectedSupport = SupportOf(selectedObject);
        SaveRoom();
        return true;
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
        if (!show && !CanRemove(obj)) return false;
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
        public string color; // hex RGB, empty when unpainted
        public bool lightsOff;
        public float lift;   // extra height from edit mode (0 in older saves)
        public float size;   // scale relative to the original (0 in older saves means 1)
        public bool starter; // desk or chair a new game starts with: can't be deleted
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

    private SavedPiece Piece(string id, int index, GameObject go)
    {
        Distraction.RestPose(go.transform, out Vector3 position, out Quaternion rotation); // never save a fallen-over piece
        return new SavedPiece
        {
            id = id, index = index, position = position, rotation = rotation, active = go.activeSelf,
            color = pieceColors.TryGetValue(go, out Color c) ? ColorUtility.ToHtmlStringRGB(c) : "",
            lightsOff = HasLights(go) && !LightsOn(go),
            starter = starterPieces.Contains(go),
            lift = lifts.TryGetValue(go, out float l) ? l : 0f,
            size = sizes.TryGetValue(go, out float z) ? z : 1f,
        };
    }

    // Saved paint colour and lamp switch.
    private void LoadColor(GameObject go, SavedPiece piece)
    {
        if (piece.lift != 0f) lifts[go] = piece.lift;
        if (piece.size > 0f && !Mathf.Approximately(piece.size, 1f)) SetSize(go, piece.size, false);
        if (HasLights(go)) SetLights(go, !piece.lightsOff, false);
        if (!string.IsNullOrEmpty(piece.color) && ColorUtility.TryParseHtmlString("#" + piece.color, out Color c) && CanPaint(go))
            ApplyPieceColor(go, c);
    }

    private void EmptyRoom()
    {
        GameObject desk = null;
        foreach (GameObject go in sceneFurniture.Values)
        {
            if (go.name == "desk") { desk = go; starterPieces.Add(go); continue; }
            go.SetActive(false);
            placedObjects.Remove(go);
        }
        SpawnStarterChair(desk);
        SaveRoom(); // writes the layout, so the room stays as it is and later saves carry it
    }

    // A chair on the camera side of the desk, facing it.
    private void SpawnStarterChair(GameObject desk)
    {
        if (desk == null) { Debug.LogWarning("RoomManager: no desk in the scene to put a starter chair at."); return; }
        FurnitureCatalog catalog = Resources.Load<FurnitureCatalog>("FurnitureCatalog");
        FurnitureItem item = catalog != null ? catalog.items.Find(i => i != null && i.prefab != null && i.name == "Chair 1") : null;
        if (item == null) { Debug.LogWarning("RoomManager: no 'Chair 1' in the furniture catalog; no starter chair."); return; }
        Physics.SyncTransforms();
        Bounds b = GetBounds(desk);
        Vector3 toCamera = Vector3.ProjectOnPlane(mainCamera.transform.position - b.center, Vector3.up).normalized;
        Vector3 position = new Vector3(b.center.x, b.min.y, b.center.z) + toCamera * (Mathf.Max(b.extents.x, b.extents.z) + 0.6f);
        GameObject chair = Instantiate(item.prefab, position, Quaternion.identity);
        chair.name = item.prefab.name;
        chair.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(ChairFront(chair, item.frontYaw), -toCamera, Vector3.up), Vector3.up);
        placedObjects.Add(chair);
        boughtItems[chair] = item;
        starterPieces.Add(chair);
    }

    // The way a sitter faces on a chair standing unrotated: the item's frontYaw if set, otherwise opposite
    // the backrest, found as the side the upper part of the mesh leans towards.
    public static Vector3 ChairFront(GameObject chair, float frontYaw)
    {
        if (frontYaw != 0f) return Quaternion.AngleAxis(frontYaw, Vector3.up) * chair.transform.rotation * Vector3.forward;
        Physics.SyncTransforms();
        Bounds b = GetBounds(chair);
        Vector3 back = Vector3.zero;
        float from = b.min.y + b.size.y * 0.7f;
        try
        {
            foreach (MeshFilter mf in chair.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 w = mf.transform.TransformPoint(v);
                    if (w.y >= from) back += new Vector3(w.x - b.center.x, 0, w.z - b.center.z);
                }
            }
        }
        catch (UnityException) { return chair.transform.forward; } // mesh not readable: assume the front is +Z
        if (back.sqrMagnitude < 1e-6f) return chair.transform.forward;
        return -back.normalized;
    }

    private void LoadRoom()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            if (PlayerPrefs.GetInt("EmptyRoom", 0) == 1) EmptyRoom(); // new game: a bare bedroom to decorate
            return; // otherwise keep the room as designed
        }
        var save = JsonUtility.FromJson<RoomSave>(PlayerPrefs.GetString(SaveKey));
        if (save == null) return;

        foreach (SavedPiece piece in save.scene)
        {
            if (!sceneFurniture.TryGetValue(piece.id, out GameObject go)) continue;
            go.transform.SetPositionAndRotation(piece.position, piece.rotation);
            go.SetActive(piece.active);
            if (!piece.active) placedObjects.Remove(go);
            if (piece.starter) starterPieces.Add(go);
            LoadColor(go, piece);
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
            if (piece.starter) starterPieces.Add(go);
            LoadColor(go, piece);
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

    // ---------- lamps ----------

    public IReadOnlyList<GameObject> PlacedPieces => placedObjects;

    // The store entry behind a placed piece: bought ones directly, scene pieces by matching the prefab's name.
    public FurnitureItem ItemOf(GameObject piece)
    {
        if (boughtItems.TryGetValue(piece, out FurnitureItem item)) return item;
        FurnitureCatalog catalog = Resources.Load<FurnitureCatalog>("FurnitureCatalog");
        return catalog == null ? null : catalog.items.Find(i => i != null && i.prefab != null && i.prefab.name == piece.name);
    }

    private static bool HasLights(GameObject piece) => piece.GetComponentInChildren<Light>(true) != null;

    private static bool LightsOn(GameObject piece)
    {
        foreach (Light light in piece.GetComponentsInChildren<Light>(true))
            if (light.enabled) return true;
        return false;
    }

    // Lights on or off, using the Interior pack's own switch (which also dims the bulb) where a lamp has one.
    private static void SetLights(GameObject piece, bool on, bool sound = true)
    {
        foreach (Light light in piece.GetComponentsInChildren<Light>(true)) light.enabled = on;
        foreach (Seagull.Interior_I1.SceneProps.GlowLight glow in piece.GetComponentsInChildren<Seagull.Interior_I1.SceneProps.GlowLight>(true))
        {
            if (!glow.gameObject.activeInHierarchy) continue; // not awake yet: it has no renderer to light
            if (on) glow.turnOn();
            else glow.turnOff();
        }
        // Its Start would otherwise switch a loaded lamp back to the prefab's setting.
        foreach (Seagull.Interior_I1.SceneProps.LightSourceObject source in piece.GetComponentsInChildren<Seagull.Interior_I1.SceneProps.LightSourceObject>(true))
            source.isOn = on;
        if (sound) UISound.Play(UISound.Cue.Light);
    }

    private void RecordAction(GameObject obj)
    {
        undoStack.Push(new Step { obj = obj });
        redoStack.Clear();
    }

    // The furniture piece a click hit, even when the collider is on one of its children.
    // The room's wall collider sits in front of the furniture from the camera's view, so take the
    // nearest hit that is actually a placed piece rather than the first thing the ray touches.
    private GameObject PlacedObjectUnderCursor()
    {
        RaycastHit[] hits = Physics.RaycastAll(mainCamera.ScreenPointToRay(Controls.PointerPosition), 100f);
        Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit hit in hits)
        {
            GameObject root = FurnitureRoot(hit.collider.gameObject);
            if (placedObjects.Contains(root)) return root;
        }
        return null;
    }

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
    }
}
