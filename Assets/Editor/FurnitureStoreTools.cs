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
        ("Alter", "Altar", "Witchy Altar", StoreCategory.Spooky, new[] { "witchy", "ritual", "new" }, 75,
            "A little altar for crystals, candles and good-luck charms before a big exam."),
        ("Books", "Books", "Books", StoreCategory.Study, new[] { "books", "study", "small", "new" }, 15,
            "A few well-loved books to scatter across a desk or shelf."),
        ("LeafPile", "LeafPile", "Leaf Pile", StoreCategory.Plants, new[] { "autumn", "seasonal", "cozy", "new" }, 15,
            "A crunchy pile of autumn leaves for a seasonal corner."),
    };

    // Imported asset packs whose prefabs go in the store automatically. Each pack gets its own item folder so
    // e.g. its "Bookshelf" can't replace ours. To add a pack: import it, then add a line here.
    const string InteriorPack = "Assets/Fries and Seagull/Ultimate Interior Pack/Prefabs";
    static readonly (string items, string[] folders)[] Packs =
    {
        // Room-item groups only (food, clothes, bathroom etc. stay out).
        ("Interior Pack", new[] { "Bed & Bedding", "Chair-like", "Shelf-like & Table-like", "Light Source", "Office Items",
            "Plants", "Clock & Alarms", "Picture Frame", "Racks" }.Select(g => $"{InteriorPack}/{g}").ToArray()),
        ("Mnostva Interiors", new[] { "Assets/Mnostva_Art" }),
        ("Poly Halloween", new[] { "Assets/polyperfect/Poly Halloween" }),
        ("Cute Furniture", new[] { $"{CuteFurniture}/Prefabs" }),
    };
    // Pack prefabs under these paths are demo scenes, characters, terrain or effects, not furniture.
    // Doors, lids and Cute Furniture's lone frame are parts of other pieces.
    static readonly string[] SkipFolders = { "/demo", "/scene", "/render_pipeline", "/character", "/fx", "/particle", "/vfx", "/effect",
        "/terrain", "first person", "_door", "_cover", "/frame/" };

    // ithappy's Cute Furniture ships an icon per piece and names like "Kitchen_D_08", so its store details are
    // written out here instead of guessed. (asset name, display name, category, price, tags, description)
    const string CuteFurniture = "Assets/ithappy/Cute_Furniture_Free";
    static readonly (string key, string name, StoreCategory category, int price, string[] tags, string description)[] CutePieces =
    {
        ("Armchair_02", "Comfy Blue Armchair", StoreCategory.Seating, 55, new[] { "armchair", "comfy", "cute" }, "A squishy blue armchair for reading breaks."),
        ("Armchair_18", "Red Wingback Armchair", StoreCategory.Seating, 60, new[] { "armchair", "comfy", "cute" }, "A cosy red armchair with wooden arms."),
        ("Chair_17", "Wooden Stool", StoreCategory.Seating, 25, new[] { "chair", "stool", "wood", "cute" }, "A simple wooden stool that tucks under any table."),
        ("Chair_PC_04", "Desk Chair", StoreCategory.Seating, 50, new[] { "chair", "office", "cute" }, "A rolling desk chair for long study sessions."),
        ("Couch_08", "Blue Sofa", StoreCategory.Seating, 80, new[] { "sofa", "couch", "comfy", "cute" }, "A roomy three-seater sofa for well-earned breaks."),
        ("Couch_11", "Pink Loveseat", StoreCategory.Seating, 70, new[] { "sofa", "couch", "pink", "cute" }, "A curvy pink loveseat with round cushions."),
        ("Coffee_Table_03", "Round Coffee Table", StoreCategory.Tables, 35, new[] { "table", "coffee", "round", "cute" }, "A low round table for snacks and a mug of tea."),
        ("Kitchen_Table_09", "Long Wooden Table", StoreCategory.Tables, 50, new[] { "table", "wood", "cute" }, "A long dark-wood table with turned legs."),
        ("Kitchen_D_06", "Skirted Side Table", StoreCategory.Tables, 30, new[] { "table", "side", "cute" }, "A little side table with a frilly cloth."),
        ("Work_Table_06", "Study Desk", StoreCategory.Tables, 55, new[] { "desk", "table", "study", "cute" }, "A tidy wooden desk with metal legs, just right for studying."),
        ("Bed_02", "Teal Storage Bed", StoreCategory.Beds, 90, new[] { "bed", "double", "storage", "cute" }, "A double bed with drawers underneath for the clutter."),
        ("Bed_07", "Wooden Single Bed", StoreCategory.Beds, 75, new[] { "bed", "single", "wood", "cute" }, "A snug wooden bed with a pink quilt."),
        ("Closet_01", "Open Bookcase", StoreCategory.Storage, 45, new[] { "shelf", "bookcase", "storage", "cute" }, "A tall wooden bookcase with a drawer at the bottom."),
        ("Closet_02", "Teal Wardrobe", StoreCategory.Storage, 55, new[] { "wardrobe", "closet", "storage", "cute" }, "A rounded teal wardrobe with pink doors."),
        ("Nightstand_02", "Nightstand", StoreCategory.Storage, 25, new[] { "nightstand", "drawer", "bedside", "cute" }, "A small bedside drawer for your phone and a glass of water."),
        ("Kitchen_D_01", "Draped Sideboard", StoreCategory.Storage, 45, new[] { "cabinet", "sideboard", "storage", "cute" }, "A wooden sideboard with a red scalloped cloth."),
        ("Kitchen_D_08", "Wooden Cupboard", StoreCategory.Storage, 30, new[] { "cabinet", "cupboard", "storage", "cute" }, "A chunky little cupboard with one round door."),
        ("Kitchen_D_09", "Low Shelf Cabinet", StoreCategory.Storage, 35, new[] { "cabinet", "shelf", "storage", "cute" }, "A low cabinet with an open shelf and a door."),
        ("Kitchen_D_10", "Double Cabinet", StoreCategory.Storage, 35, new[] { "cabinet", "storage", "cute" }, "A low wooden cabinet with two doors."),
        ("Light_05", "Tripod Floor Lamp", StoreCategory.Lighting, 30, new[] { "lamp", "floor", "light", "cute" }, "A tall tripod lamp to keep your study corner warm and bright."),
        ("Plants_05", "Potted Daisy", StoreCategory.Plants, 15, new[] { "plant", "flower", "pot", "cute" }, "A cheerful pink flower in a terracotta pot."),
        ("Plants_15", "Leafy Sprout", StoreCategory.Plants, 15, new[] { "plant", "pot", "cute" }, "A young leafy plant in a dark pot."),
        ("Plants_19", "Banana Plant", StoreCategory.Plants, 20, new[] { "plant", "tall", "pot", "cute" }, "A tall banana-leaf plant to bring a little life into the room."),
        ("Book_03", "Row of Books", StoreCategory.Study, 15, new[] { "books", "study", "small", "cute" }, "A colourful row of books for a desk or shelf."),
        ("Book_08", "Book Stack", StoreCategory.Study, 10, new[] { "books", "study", "small", "cute" }, "A stack of well-thumbed textbooks."),
        ("Paper_01", "Loose Notes", StoreCategory.Study, 5, new[] { "paper", "notes", "study", "small", "cute" }, "A couple of loose pages of notes."),
        ("Paper_02", "Sheet of Paper", StoreCategory.Study, 5, new[] { "paper", "study", "small", "cute" }, "A blank sheet, ready for the next idea."),
        ("Clock_03", "Grandfather Clock", StoreCategory.Decor, 60, new[] { "clock", "tall", "wood", "cute" }, "A tall pendulum clock that keeps every session on time."),
        ("ExerciseBike_01", "Exercise Bike", StoreCategory.Decor, 70, new[] { "exercise", "bike", "fitness", "cute" }, "For stretching your legs between study sessions."),
        ("Guitar_01", "Acoustic Guitar", StoreCategory.Decor, 40, new[] { "guitar", "music", "cute" }, "A little guitar for strumming on breaks."),
        ("Toy_02", "Toy Car", StoreCategory.Decor, 10, new[] { "toy", "car", "small", "cute" }, "A chunky red toy car."),
        ("Toy_03", "Spinning Top", StoreCategory.Decor, 10, new[] { "toy", "small", "cute" }, "A bright spinning top to fidget with."),
        ("Picture_08", "Flower Print", StoreCategory.WallDecor, 20, new[] { "picture", "art", "frame", "cute" }, "A framed print of a stylised flower."),
        ("Picture_17", "Abstract Print", StoreCategory.WallDecor, 20, new[] { "picture", "art", "frame", "cute" }, "A framed print of soft circles and lines."),
        ("Picture_21", "Triangle Print", StoreCategory.WallDecor, 20, new[] { "picture", "art", "frame", "cute" }, "A wide framed print with a bold pink triangle."),
        ("Computer_01", "Desktop Computer", StoreCategory.Electronics, 60, new[] { "computer", "monitor", "pc", "cute" }, "A cute desktop computer with a keyboard and mouse."),
        ("GameConsole_01", "Game Controller", StoreCategory.Electronics, 25, new[] { "game", "controller", "small", "cute" }, "A controller for a reward round after a long session."),
        ("Keyboard_01", "Keyboard", StoreCategory.Electronics, 15, new[] { "keyboard", "computer", "small", "cute" }, "A spare keyboard for the desk."),
        ("Mouse_01", "Computer Mouse", StoreCategory.Electronics, 10, new[] { "mouse", "computer", "small", "cute" }, "A little computer mouse."),
        ("Fridge_01", "Mini Fridge", StoreCategory.KitchenBath, 60, new[] { "fridge", "kitchen", "snacks", "cute" }, "A pastel fridge to keep study snacks cold."),
        ("Microwave_01", "Microwave", StoreCategory.KitchenBath, 35, new[] { "microwave", "kitchen", "cute" }, "A pink microwave for late-night noodles."),
        ("Cutting_board_02", "Cutting Board", StoreCategory.KitchenBath, 10, new[] { "kitchen", "board", "small", "cute" }, "A wooden chopping board."),
        ("Utensils_01", "Rolling Pin", StoreCategory.KitchenBath, 10, new[] { "kitchen", "baking", "small", "cute" }, "A rolling pin for study-break baking."),
        ("Bath_03", "Bathtub", StoreCategory.KitchenBath, 80, new[] { "bath", "bathtub", "bathroom", "cute" }, "A bathtub with a rain shower for a long soak."),
        ("Toilet_03", "Toilet", StoreCategory.KitchenBath, 40, new[] { "toilet", "bathroom", "cute" }, "Every home needs one."),
        ("Wash_Basin_07", "Pedestal Sink", StoreCategory.KitchenBath, 40, new[] { "sink", "basin", "bathroom", "cute" }, "A pink pedestal sink with a golden tap."),
        ("Mixer_08", "Tap", StoreCategory.KitchenBath, 10, new[] { "tap", "faucet", "bathroom", "small", "cute" }, "A shiny tap for a sink or tub."),
        ("Toothbrush_01", "Toothbrush", StoreCategory.KitchenBath, 5, new[] { "toothbrush", "bathroom", "small", "cute" }, "A red toothbrush."),
        ("Toothpaste_01", "Toothpaste", StoreCategory.KitchenBath, 5, new[] { "toothpaste", "bathroom", "small", "cute" }, "A tube of minty toothpaste."),
    };

    static FurnitureStoreTools()
    {
        // Wait until the asset database is ready; cheap no-op once the items exist.
        EditorApplication.delayCall += RunSetup;
    }

    [MenuItem("Study Sim/Run Furniture Setup")]
    public static void RunSetup()
    {
        AddModelPieces();
        foreach (var pack in Packs) AddPack(pack.items, pack.folders);
        Reclassify();
        RoomSurfacesTools.BuildIfMissing();
    }

    static void AddPack(string packName, string[] packFolders)
    {
        string[] folders = packFolders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (folders.Length == 0) return; // not imported (yet)
        string itemFolder = $"{ItemFolder}/{packName}";
        // Only prefabs without a store item yet, so reopening the project stays fast.
        var newPaths = AssetDatabase.FindAssets("t:Prefab", folders).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !SkipFolders.Any(f => p.ToLowerInvariant().Contains(f)))
            .Where(p => AssetDatabase.LoadAssetAtPath<FurnitureItem>($"{itemFolder}/{Path.GetFileNameWithoutExtension(p)}.asset") == null)
            .ToArray();
        if (newPaths.Length == 0) return;

        FurnitureCatalog catalog = LoadOrCreateCatalog();
        int added = 0;
        try
        {
            for (int i = 0; i < newPaths.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Furniture setup", Path.GetFileNameWithoutExtension(newPaths[i]), (float)i / newPaths.Length);
                if (AddToStore(AssetDatabase.LoadAssetAtPath<GameObject>(newPaths[i]), catalog, itemFolder) != null) added++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Furniture setup: added {added} {packName} item(s) to the store.");
    }

    static void AddModelPieces()
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
        var created = sources.Select(src => AddToStore(src, catalog, ItemFolder)).Where(i => i != null).ToArray();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.objects = created;
        Debug.Log($"Added {created.Length} item(s) to {AssetDatabase.GetAssetPath(catalog)}. Check the guessed description, category, tags and price in the Inspector.");
    }

    static FurnitureItem AddToStore(GameObject source, FurnitureCatalog catalog, string itemFolder)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath)) return null;

        GameObject prefab = PrepareFurniturePrefab(source, sourcePath);
        if (prefab == null) return null;

        EnsureFolder(itemFolder);
        string itemPath = $"{itemFolder}/{prefab.name}.asset";
        FurnitureItem item = AssetDatabase.LoadAssetAtPath<FurnitureItem>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<FurnitureItem>();
            Describe(item, prefab.name, sourcePath);
            DescribeCute(item, prefab.name, sourcePath);
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
        ("sink", StoreCategory.KitchenBath), ("stove", StoreCategory.KitchenBath), ("fridge", StoreCategory.KitchenBath),
        ("refrigerat", StoreCategory.KitchenBath), ("freezer", StoreCategory.KitchenBath), ("toilet", StoreCategory.KitchenBath),
        ("vanity", StoreCategory.KitchenBath), ("kitchen", StoreCategory.KitchenBath), ("counter", StoreCategory.KitchenBath),
        ("bathtub", StoreCategory.KitchenBath), ("washing", StoreCategory.KitchenBath),
        ("pillow", StoreCategory.Textiles), ("blanket", StoreCategory.Textiles), ("quilt", StoreCategory.Textiles),
        ("rug", StoreCategory.Textiles),
        ("bed", StoreCategory.Beds),
        ("chair", StoreCategory.Seating), ("sofa", StoreCategory.Seating), ("couch", StoreCategory.Seating),
        ("stool", StoreCategory.Seating), ("bench", StoreCategory.Seating),
        ("lamp", StoreCategory.Lighting), ("light", StoreCategory.Lighting), ("candle", StoreCategory.Lighting),
        ("lantern", StoreCategory.Lighting), ("chandelier", StoreCategory.Lighting), ("pendant", StoreCategory.Lighting),
        ("desk", StoreCategory.Tables), ("table", StoreCategory.Tables),
        ("plant", StoreCategory.Plants), ("tree", StoreCategory.Plants), ("flower", StoreCategory.Plants),
        ("cactus", StoreCategory.Plants), ("leaf", StoreCategory.Plants), ("succulent", StoreCategory.Plants),
        ("shelf", StoreCategory.Storage), ("cabinet", StoreCategory.Storage), ("drawer", StoreCategory.Storage),
        ("wardrobe", StoreCategory.Storage), ("dresser", StoreCategory.Storage), ("closet", StoreCategory.Storage),
        ("bookcase", StoreCategory.Storage), ("rack", StoreCategory.Storage), ("nightstand", StoreCategory.Storage),
        ("book", StoreCategory.Study), ("binder", StoreCategory.Study), ("clipboard", StoreCategory.Study), // after "bookcase"/"shelf"
        ("tv", StoreCategory.Electronics), ("monitor", StoreCategory.Electronics), ("keyboard", StoreCategory.Electronics),
        ("printer", StoreCategory.Electronics), ("vinyl", StoreCategory.Electronics),
        ("picture", StoreCategory.WallDecor), ("painting", StoreCategory.WallDecor), ("poster", StoreCategory.WallDecor),
        ("mirror", StoreCategory.WallDecor), ("wall clock", StoreCategory.WallDecor),
        ("halloween", StoreCategory.Spooky),
    };
    static readonly int[] CategoryPrices = { 40, 50, 80, 40, 25, 20, 15, 10, 15, 20, 45, 40, 15 }; // indexed by StoreCategory

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
        string[] skip = { "assets", "models", "prefabs", "and", "the", "like" };
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
            StoreCategory.Seating or StoreCategory.Tables or StoreCategory.Beds or StoreCategory.Storage => $"A {lowerName} to make your room your own.",
            _ => $"A {lowerName} to add some personality to your space.",
        };
    }

    // Hand-written details and the pack's own icon for Cute Furniture pieces; other items keep their guesses.
    static void DescribeCute(FurnitureItem item, string assetName, string sourcePath)
    {
        if (!sourcePath.StartsWith(CuteFurniture)) return;
        var piece = CutePieces.FirstOrDefault(p => p.key == assetName);
        if (piece.key == null) return;
        item.displayName = piece.name;
        item.category = piece.category;
        item.price = piece.price;
        item.tags = piece.tags;
        item.description = piece.description;
        item.colorable = piece.category != StoreCategory.WallDecor; // prints get their look from the texture

        // The icons import as plain textures; the store wants sprites.
        string iconPath = $"{CuteFurniture}/Icons/{assetName}.png";
        if (AssetImporter.GetAtPath(iconPath) is TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        }
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

    // Hanging pieces go on the ceiling, frames/clocks/posters on walls, everything else on the floor
    // (change it on the prefab's Furniture component).
    // shortcut: substring match on the name, so odd names land on the floor; fix those on the prefab.
    static readonly string[] CeilingWords = { "hanging", "chandelier", "pendant", "ceiling", "track light" };
    // Bare "wall" or "fence" would catch graveyard walls and fences, which stand on the floor.
    static readonly string[] WallWords = { "wall clock", "wall light", "wall lamp", "wall shelf", "wall art", "wall decor",
        "wall hanging", "wall sign", "wall mirror", "painting", "poster", "mirror", "window", "curtain", "picture",
        "wreath", "banner", "corkboard", "whiteboard", "notice board", "calendar" }; // not "frame": bed frames stand

    static Furniture.FurnitureType GuessPlacement(string assetName)
    {
        string lower = assetName.ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        if (CeilingWords.Any(lower.Contains)) return Furniture.FurnitureType.Ceiling;
        // Alarm, table and grandfather clocks (Cute Furniture's "Clock_03") stand; other clocks hang.
        bool clock = lower.Contains("clock") && !lower.Contains("alarm") && !lower.Contains("table") && !lower.Contains("desk")
            && lower != "clock 03";
        if (clock || WallWords.Any(lower.Contains)) return Furniture.FurnitureType.Wall;
        return Furniture.FurnitureType.Floor;
    }

    // Items added before wall/ceiling detection got better: move them to the right surface once per machine.
    // Only floor pieces are changed, so a type someone chose on purpose (shelf, wall) is kept.
    const string ReclassifiedKey = "StudySim.FurniturePlacement.v3";

    [MenuItem("Study Sim/Re-detect Wall And Ceiling Furniture")]
    static void ReclassifyFromMenu()
    {
        EditorPrefs.DeleteKey(ReclassifiedKey);
        Reclassify();
    }

    static void Reclassify()
    {
        if (EditorPrefs.GetBool(ReclassifiedKey)) return;
        FurnitureCatalog catalog = LoadOrCreateCatalog();
        int moved = 0;
        foreach (FurnitureItem item in catalog.items.Where(i => i != null && i.prefab != null))
        {
            Furniture.FurnitureType guess = GuessPlacement(item.prefab.name);
            Furniture current = item.prefab.GetComponent<Furniture>();
            if (current == null) continue;
            // v2 hung anything named "wall" (graveyard walls, fences) on the walls: put those back on the floor.
            bool looseWallMatch = current.Type == Furniture.FurnitureType.Wall && guess == Furniture.FurnitureType.Floor
                && item.prefab.name.ToLowerInvariant().Contains("wall");
            if (!looseWallMatch && (guess == Furniture.FurnitureType.Floor || current.Type != Furniture.FurnitureType.Floor)) continue;
            string path = AssetDatabase.GetAssetPath(item.prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Furniture furniture = root.GetComponent<Furniture>();
            if (furniture != null)
            {
                furniture.Type = guess;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                moved++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(ReclassifiedKey, true);
        if (moved > 0) Debug.Log($"Furniture setup: changed placement (floor, wall or ceiling) for {moved} item(s).");
    }

    // Returns true if anything was added.
    static bool EnsureFurnitureSetup(GameObject root)
    {
        bool changed = false;
        if (root.GetComponent<Furniture>() == null)
        {
            root.AddComponent<Furniture>().Type = GuessPlacement(root.name);
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
