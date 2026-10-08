using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assets > Study Sim > Add To Furniture Store: turn selected models/prefabs into store items.
// Each gets a prefab (with Furniture + collider) and a FurnitureItem in the catalog; fill in the
// description, category, tags and price in the Inspector. Icons are rendered by the store at runtime.
public static class FurnitureStoreTools
{
    const string PrefabFolder = "Assets/ART/Models/Furniture";
    const string ItemFolder = "Assets/Data/Furniture";
    const string CatalogPath = "Assets/Data/FurnitureCatalog.asset";
    const int DefaultPrice = 10;

    [MenuItem("Assets/Study Sim/Add To Furniture Store", true)]
    static bool CanAddSelection()
    {
        return Selection.GetFiltered<GameObject>(SelectionMode.Assets).Length > 0;
    }

    [MenuItem("Assets/Study Sim/Add To Furniture Store")]
    static void AddSelection()
    {
        GameObject[] sources = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        FurnitureCatalog catalog = LoadOrCreateCatalog();
        var created = sources.Select(src => AddToStore(src, catalog)).Where(i => i != null).ToArray();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.objects = created;
        Debug.Log($"Added {created.Length} item(s) to {AssetDatabase.GetAssetPath(catalog)}. Fill in description, category, tags and price in the Inspector.");
    }

    static FurnitureItem AddToStore(GameObject source, FurnitureCatalog catalog)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath)) return null;

        GameObject prefab = PrepareFurniturePrefab(source, sourcePath);
        if (prefab == null) return null;

        EnsureFolder(ItemFolder);
        string itemPath = $"{ItemFolder}/{prefab.name}.asset";
        FurnitureItem item = AssetDatabase.LoadAssetAtPath<FurnitureItem>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<FurnitureItem>();
            item.displayName = ObjectNames.NicifyVariableName(prefab.name);
            item.price = DefaultPrice;
            AssetDatabase.CreateAsset(item, itemPath);
        }
        item.prefab = prefab;
        EditorUtility.SetDirty(item);

        if (!catalog.items.Contains(item)) catalog.items.Add(item);
        return item;
    }

    // Models get a prefab variant in the furniture folder; prefabs are fixed up in place.
    // Either way the result has a Furniture component and a collider RoomManager can place with.
    static GameObject PrepareFurniturePrefab(GameObject source, string sourcePath)
    {
        if (PrefabUtility.GetPrefabAssetType(source) == PrefabAssetType.Model)
        {
            EnsureFolder(PrefabFolder);
            string prefabPath = $"{PrefabFolder}/{source.name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) return FixPrefabInPlace(existing);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            EnsureFurnitureSetup(instance);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return saved;
        }
        return FixPrefabInPlace(source);
    }

    static GameObject FixPrefabInPlace(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (EnsureFurnitureSetup(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // Returns true if anything was added.
    static bool EnsureFurnitureSetup(GameObject root)
    {
        bool changed = false;
        if (root.GetComponent<Furniture>() == null)
        {
            root.AddComponent<Furniture>();
            changed = true;
        }
        if (root.GetComponentInChildren<Collider>() == null)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds world = renderers[0].bounds;
                foreach (Renderer r in renderers) world.Encapsulate(r.bounds);
                BoxCollider box = root.AddComponent<BoxCollider>();
                // InverseTransformVector handles the rotated/scaled roots FBX imports often have.
                Vector3 size = root.transform.InverseTransformVector(world.size);
                box.center = root.transform.InverseTransformPoint(world.center);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            else
            {
                root.AddComponent<BoxCollider>();
            }
            changed = true;
        }
        return changed;
    }

    static FurnitureCatalog LoadOrCreateCatalog()
    {
        string guid = AssetDatabase.FindAssets("t:FurnitureCatalog").FirstOrDefault();
        if (guid != null) return AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(AssetDatabase.GUIDToAssetPath(guid));

        EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
        var catalog = ScriptableObject.CreateInstance<FurnitureCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
