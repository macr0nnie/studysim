using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns window_cozy.fbx / window_curtains.fbx into wall pieces for the store. The light and glow come from
// RoomAmbience: it gives every placed piece named "Window" its own WindowLight and turns "WindowGlow*" materials
// (the glass, as imported) into a see-through gap, so the prefabs carry no light of their own.
public static class WindowStoreTools
{
    const string PrefabFolder = "Assets/ART/Models/Furniture";
    const string ItemFolder = "Assets/Data/Furniture";

    static readonly (string model, string prefab, string name, int price, string description)[] Windows =
    {
        ("Assets/ART/Models/window_cozy.fbx", "Window_Cozy", "Cozy Window", 35, "A chunky wooden window with a warm glow. The light shines in wherever you hang it."),
        ("Assets/ART/Models/window_curtains.fbx", "Window_Curtains", "Gingham Curtain Window", 45, "A glowing window with soft gingham curtains, for golden-hour study sessions."),
    };

    [MenuItem("Study Sim/Add Cozy Windows To Store")]
    public static void AddWindows()
    {
        string catalogGuid = AssetDatabase.FindAssets("t:FurnitureCatalog").FirstOrDefault();
        if (catalogGuid == null) { Debug.LogError("No FurnitureCatalog found. Run Study Sim > Run Furniture Setup first."); return; }
        var catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(AssetDatabase.GUIDToAssetPath(catalogGuid));
        int added = 0;
        foreach (var w in Windows)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(w.model);
            if (model == null) { Debug.LogError($"Missing {w.model}"); continue; }
            GameObject prefab = BuildPrefab(model, w.prefab);

            string itemPath = $"{ItemFolder}/{w.prefab}.asset";
            var item = AssetDatabase.LoadAssetAtPath<FurnitureItem>(itemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<FurnitureItem>();
                item.displayName = w.name;
                item.description = w.description;
                item.category = StoreCategory.WallDecor;
                item.tags = new[] { "window", "light", "cozy", "new" };
                item.price = w.price;
                AssetDatabase.CreateAsset(item, itemPath);
            }
            item.prefab = prefab;
            EditorUtility.SetDirty(item);
            if (!catalog.items.Contains(item)) { catalog.items.Add(item); added++; }
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Cozy windows ready ({added} new in the store, Wall Decor tab).");
    }

    static GameObject BuildPrefab(GameObject model, string prefabName)
    {
        var root = new GameObject(prefabName);
        var mesh = (GameObject)PrefabUtility.InstantiatePrefab(model);
        mesh.transform.SetParent(root.transform, false);

        // RoomManager turns wall pieces so their +Z points into the room: face the window's front (the sill side) that way.
        Renderer sill = Find(mesh, "Sill"), top = Find(mesh, "Frame_Top"), glass = Find(mesh, "Glass");
        Vector3 front = Vector3.ProjectOnPlane(sill.bounds.center - top.bounds.center, Vector3.up);
        mesh.transform.rotation = Quaternion.FromToRotation(front.normalized, Vector3.forward) * mesh.transform.rotation;

        Bounds all = Bounds(root);
        mesh.transform.position -= new Vector3(all.center.x, all.min.y, all.min.z); // origin: bottom centre of the back
        all = Bounds(root);

        if (!glass.sharedMaterials.Any(m => m != null && m.name.StartsWith("WindowGlow")))
            Debug.LogWarning($"{model.name}: the glass has no WindowGlow material, so it won't be see-through");

        root.AddComponent<Furniture>().Type = Furniture.FurnitureType.Wall;
        var box = root.AddComponent<BoxCollider>();
        box.center = all.center;
        box.size = all.size;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    static Renderer Find(GameObject go, string name) =>
        go.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == name)
        ?? throw new System.Exception($"{go.name}: no '{name}' piece");

    static Bounds Bounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
