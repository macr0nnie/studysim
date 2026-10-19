using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Swaps the open scene's room model (bedroom1's Walls and Floor) for bedroom_base.fbx, or, when BedroomBase is
// already in the scene (e.g. after the model was replaced), sets it up again: fit, layers, RoomManager lists.
// The room is fitted to the old floor's footprint so Floor_Plane, WallPlane and placed furniture still line up;
// the old Walls and Floor are switched off, not deleted, so it can be undone by turning them back on.
// Model parts: Wall_* = paintable walls (the whole thick wall chunk; decor hangs on it), Floor + Base = paintable
// floor chunk, anything else (the wood Trim) is left out of Paint but kept on the wall layer with a collider so placed
// pieces bump into it.
public static class RoomSwapTools
{
    const string NewRoomPath = "Assets/ART/Models/bedroom_base.fbx";
    const string OldRoomPath = "Assets/ART/Models/bedroom1.fbx";
    const string ScenePath = "Assets/Scenes/Protoype_2.unity";

    [MenuItem("Study Sim/Swap In Bedroom Base")]
    public static void SwapInBedroomBase()
    {
        // RoomManager hangs decor on MeshColliders built from the wall meshes, which needs Read/Write.
        var importer = (ModelImporter)AssetImporter.GetAtPath(NewRoomPath);
        if (importer == null) { Debug.LogError($"Missing {NewRoomPath}"); return; }
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

        if (EditorApplication.isPlaying) { Debug.LogError("Leave Play mode first, then run Swap In Bedroom Base again."); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        Renderer oldWalls = null, oldFloor = null;
        foreach (MeshFilter f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            if (f.sharedMesh == null || AssetDatabase.GetAssetPath(f.sharedMesh) != OldRoomPath) continue;
            if (f.name == "Walls") oldWalls = f.GetComponent<Renderer>();
            else if (f.name == "Floor") oldFloor = f.GetComponent<Renderer>();
        }
        if (oldWalls == null || oldFloor == null) { Debug.LogError($"Couldn't find bedroom1's Walls and Floor in {ScenePath}."); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewRoomPath);
        // Exactly one room: keep the first instance of bedroom_base, delete any others.
        GameObject room = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
            if (AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) != NewRoomPath) continue;
            if (room == null) room = t.gameObject;
            else { Debug.Log($"Deleted duplicate room {t.name}"); Undo.DestroyObjectImmediate(t.gameObject); }
        }
        bool fresh = room == null;
        if (fresh)
        {
            room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, oldWalls.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(room, "Swap in bedroom base");
            room.transform.SetParent(oldWalls.transform.parent, true);
        }
        else
        {
            Undo.RecordObject(room.transform, "Set up bedroom base");
            room.transform.localRotation = prefab.transform.localRotation;
            room.transform.localScale = prefab.transform.localScale;
        }
        room.name = "BedroomBase";
        room.SetActive(true);

        Material neutral = NeutralMaterial();
        int wallLayer = LayerMask.NameToLayer("Wall");
        var walls = new List<Renderer>();
        var floors = new List<Renderer>();
        Renderer newFloor = null;
        foreach (Renderer r in room.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Floor") { newFloor = r; floors.Add(r); r.gameObject.layer = oldFloor.gameObject.layer; }
            else if (r.name == "Base") { floors.Add(r); r.gameObject.layer = oldFloor.gameObject.layer; } // painted with the floor as one chunk
            else if (r.name.StartsWith("Wall_")) { walls.Add(r); r.gameObject.layer = oldWalls.gameObject.layer; }
            else
            {
                r.gameObject.layer = wallLayer >= 0 ? wallLayer : oldWalls.gameObject.layer;
                if (r.GetComponent<Collider>() == null && r.TryGetComponent(out MeshFilter mf))
                    Undo.AddComponent<MeshCollider>(r.gameObject).sharedMesh = mf.sharedMesh;
            }
        }
        if (newFloor == null || walls.Count == 0) { Debug.LogError($"{NewRoomPath} needs a 'Floor' and 'Wall_*' pieces."); return; }

        // Paint tints the material colour, so the paintable walls and floor start from a near-white neutral base.
        foreach (Renderer r in walls.Concat(floors))
        {
            Undo.RecordObject(r, "Neutral room");
            var slots = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = neutral;
            r.sharedMaterials = slots;
        }

        // The old floor is switched off after the first swap and an inactive renderer has no bounds: use its mesh.
        Bounds target = WorldBounds(oldFloor.GetComponent<MeshFilter>());

        // The room has only two walls: turn it (in quarter turns) so their corner is the one away from the camera.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Bounds wallBounds = walls[0].bounds;
            foreach (Renderer w in walls) wallBounds.Encapsulate(w.bounds);
            Vector3 corner = Vector3.ProjectOnPlane(wallBounds.center - newFloor.bounds.center, Vector3.up);
            Vector3 away = Vector3.ProjectOnPlane(target.center - cam.transform.position, Vector3.up);
            float turn = Mathf.Round(Vector3.SignedAngle(corner, away, Vector3.up) / 90f) * 90f;
            room.transform.Rotate(Vector3.up, turn, Space.World);
        }
        else Debug.LogWarning("No main camera: the new walls may face the wrong way. Turn BedroomBase a quarter at a time to fix.");

        // Fit: same footprint as the old floor, new floor top level with the old one.
        float scale = Mathf.Max(target.size.x, target.size.z) / Mathf.Max(newFloor.bounds.size.x, newFloor.bounds.size.z);
        room.transform.localScale *= scale;
        Bounds now = newFloor.bounds;
        room.transform.position += new Vector3(target.center.x - now.center.x, target.max.y - now.max.y, target.center.z - now.center.z);

        Undo.RecordObject(oldWalls.gameObject, "Swap in bedroom base");
        Undo.RecordObject(oldFloor.gameObject, "Swap in bedroom base");
        oldWalls.gameObject.SetActive(false);
        oldFloor.gameObject.SetActive(false);

        var manager = Object.FindAnyObjectByType<RoomManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            SetArray(so.FindProperty("wallMeshes"), walls);
            SetArray(so.FindProperty("floorMeshes"), floors);
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("No RoomManager in the scene: wallMeshes / floorMeshes not set.");

        EditorSceneManager.MarkSceneDirty(room.scene);
        EditorSceneManager.SaveScene(room.scene);
        Debug.Log($"{(fresh ? "Swapped in" : "Set up")} bedroom_base (scale {scale:0.###}): {walls.Count} wall meshes, {floors.Count} floor mesh. Old Walls/Floor switched off. Scene saved.");
    }

    const string NeutralPath = "Assets/ART/Mats/Room_Neutral.mat";

    static Material NeutralMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(NeutralPath);
        if (m != null) return m;
        Shader lit = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(lit) { name = "Room_Neutral" };
        Color c = new Color32(0xF2, 0xF0, 0xEC, 0xFF);
        m.SetColor("_BaseColor", c);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(m, NeutralPath);
        return m;
    }

    static Bounds WorldBounds(MeshFilter f)
    {
        Bounds local = f.sharedMesh.bounds;
        Matrix4x4 m = f.transform.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            b.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
        return b;
    }

    static void SetArray(SerializedProperty array, List<Renderer> items)
    {
        array.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
