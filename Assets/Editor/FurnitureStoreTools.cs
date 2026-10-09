using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assets > Study Sim > Add To Furniture Store: turn selected models/prefabs (or whole folders of them) into
// store items. Each gets a prefab (with Furniture + collider) and a FurnitureItem in the catalog with a
// category, tags, price and description guessed from its name and folder; adjust them in the Inspector.
// Icons are rendered by the store at runtime. Items already in the store are left as they are.
//
// The pieces below only exist inside WITCHYfURNITURE.fbx (their mesh IDs are made by Unity's importer),
// so they are turned into prefabs + store items automatically the first time the project opens.
[InitializeOnLoad]
public static class FurnitureStoreTools
{
    const string PrefabFolder = "Assets/ART/Models/Furniture";
    const string ItemFolder = "Assets/Data/Furniture";
    const string CatalogPath = "Assets/Resources/FurnitureCatalog.asset"; // Resources so the store can find it in any scene

    const string WitchyModelPath = "Assets/ART/Models/WITCHYfURNITURE.fbx";

    // (FBX node, asset key, display name, category, tags, price, description)
    static readonly (string node, string key, string name, StoreCategory category, string[] tags, int price, string description)[] FromModel =
    {
        ("Alter", "Altar", "Witchy Altar", StoreCategory.Decor, new[] { "witchy", "ritual", "new" }, 75,
            "A little altar for crystals, candles and good-luck charms before a big exam."),
        ("Books", "Books", "Books", StoreCategory.Decor, new[] { "books", "study", "small", "new" }, 15,
            "A few well-loved books to scatter across a desk or shelf."),
        ("LeafPile", "LeafPile", "Leaf Pile", StoreCategory.Plants, new[] { "autumn", "seasonal", "cozy", "new" }, 15,
            "A crunchy pile of autumn leaves for a seasonal corner."),
    };

    static FurnitureStoreTools()
    {
        // Wait until the asset database is ready; cheap no-op once the items exist.
        EditorApplication.delayCall += AddModelPieces;
    }

    [MenuItem("Study Sim/Run Furniture Setup")]
    public static void AddModelPieces()
    {
        var missing = FromModel.Where(p => AssetDatabase.LoadAssetAtPath<FurnitureItem>($"{ItemFolder}/{p.key}.asset") == null).ToArray();
        if (missing.Length == 0) return;
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(WitchyModelPath);
        if (model == null) return;

        FurnitureCatalog catalog = LoadOrCreateCatalog();
        EnsureFolder(PrefabFolder);
        EnsureFolder(ItemFolder);
        foreach (var piece in missing)
        {
            Transform node = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == piece.node);
            if (node == null)
            {
                Debug.LogWarning($"Furniture setup: no '{piece.node}' in {WitchyModelPath}; skipped.");
                continue;
            }
            // Unlinked copy keeps the node's own rotation/scale, matching the other furniture prefabs.
            GameObject copy = Object.Instantiate(node.gameObject);
            copy.name = piece.key;
            copy.transform.position = Vector3.zero;
            EnsureFurnitureSetup(copy);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, $"{PrefabFolder}/{piece.key}.prefab");
            Object.DestroyImmediate(copy);

            var item = ScriptableObject.CreateInstance<FurnitureItem>();
            item.displayName = piece.name;
            item.description = piece.description;
            item.category = piece.category;
            item.tags = piece.tags;
            item.price = piece.price;
            item.prefab = prefab;
            AssetDatabase.CreateAsset(item, $"{ItemFolder}/{piece.key}.asset");
            catalog.items.Add(item);
            Debug.Log($"Furniture setup: added {piece.name} to the store.");
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Assets/Study Sim/Add To Furniture Store", true)]
    static bool CanAddSelection()
    {
        return Selection.GetFiltered<GameObject>(SelectionMode.DeepAssets).Length > 0;
    }

    [MenuItem("Assets/Study Sim/Add To Furniture Store")]
    static void AddSelection()
    {
        // DeepAssets includes everything inside selected folders. Asset packs usually ship a prefab next to
        // each model, so a model is skipped when a prefab of the same name is also selected.
        GameObject[] all = Selection.GetFiltered<GameObject>(SelectionMode.DeepAssets);
        var prefabNames = all.Where(g => PrefabUtility.GetPrefabAssetType(g) != PrefabAssetType.Model).Select(g => g.name).ToHashSet();
        GameObject[] sources = all.Where(g => PrefabUtility.GetPrefabAssetType(g) != PrefabAssetType.Model || !prefabNames.Contains(g.name)).ToArray();
        FurnitureCatalog catalog = LoadOrCreateCatalog();
        var created = sources.Select(src => AddToStore(src, catalog)).Where(i => i != null).ToArray();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.objects = created;
        Debug.Log($"Added {created.Length} item(s) to {AssetDatabase.GetAssetPath(catalog)}. Check the guessed description, category, tags and price in the Inspector.");
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
            Describe(item, prefab.name, sourcePath);
            AssetDatabase.CreateAsset(item, itemPath);
        }
        item.prefab = prefab;
        EditorUtility.SetDirty(item);

        if (!catalog.items.Contains(item)) catalog.items.Add(item);
        return item;
    }

    // (keyword, category) checked in order against the asset's name, then its folder path.
    // shortcut: substring match, so e.g. "vegetable" reads as a table; fix such items in the Inspector.
    static readonly (string word, StoreCategory category)[] CategoryWords =
    {
        ("lamp", StoreCategory.Lighting), ("light", StoreCategory.Lighting), ("candle", StoreCategory.Lighting),
        ("lantern", StoreCategory.Lighting), ("chandelier", StoreCategory.Lighting),
        ("plant", StoreCategory.Plants), ("tree", StoreCategory.Plants), ("flower", StoreCategory.Plants),
        ("cactus", StoreCategory.Plants), ("leaf", StoreCategory.Plants), ("succulent", StoreCategory.Plants),
        ("bed", StoreCategory.Furniture), ("chair", StoreCategory.Furniture), ("sofa", StoreCategory.Furniture),
        ("couch", StoreCategory.Furniture), ("table", StoreCategory.Furniture), ("desk", StoreCategory.Furniture),
        ("shelf", StoreCategory.Furniture), ("cabinet", StoreCategory.Furniture), ("drawer", StoreCategory.Furniture),
        ("wardrobe", StoreCategory.Furniture), ("stool", StoreCategory.Furniture), ("bench", StoreCategory.Furniture),
        ("counter", StoreCategory.Furniture), ("dresser", StoreCategory.Furniture), ("bathtub", StoreCategory.Furniture),
        ("vanity", StoreCategory.Furniture), ("closet", StoreCategory.Furniture),
    };
    static readonly int[] CategoryPrices = { 40, 15, 25, 20 }; // indexed by StoreCategory

    // First guess at store details for a new item; the Inspector is where they get polished.
    static void Describe(FurnitureItem item, string assetName, string sourcePath)
    {
        string name = ObjectNames.NicifyVariableName(assetName);
        string folders = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
        string lowerName = name.ToLowerInvariant(), lowerFolders = folders.ToLowerInvariant();
        var match = CategoryWords.FirstOrDefault(c => lowerName.Contains(c.word));
        if (match.word == null) match = CategoryWords.FirstOrDefault(c => lowerFolders.Contains(c.word));
        item.category = match.word != null ? match.category : StoreCategory.Decor;

        // Tags: words from the name and the asset's own folder (e.g. "Bed & Bedding"), minus numbers and filler.
        string[] skip = { "assets", "models", "prefabs", "and", "the" };
        item.tags = (name + " " + Path.GetFileName(folders)).ToLowerInvariant()
            .Split(' ', '&', '-', '_', ',', '(', ')')
            .Where(w => w.Length > 1 && !w.All(char.IsDigit) && !skip.Contains(w))
            .Distinct().ToArray();

        item.displayName = name;
        item.price = CategoryPrices[(int)item.category];
        item.description = item.category switch
        {
            StoreCategory.Lighting => $"A {lowerName} to keep your study corner warm and bright.",
            StoreCategory.Plants => $"A {lowerName} to bring a little life into the room.",
            StoreCategory.Furniture => $"A {lowerName} to make your room your own.",
            _ => $"A {lowerName} to add some personality to your space.",
        };
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
