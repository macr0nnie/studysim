using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Editor tools for the furniture store:
//  - Assets > Study Sim > Add To Furniture Store: turn selected models/prefabs into store items.
//  - GameObject > Study Sim > Furniture Store UI: build a clean, pre-wired store panel in the open scene.
public static class FurnitureStoreTools
{
    const string PrefabFolder = "Assets/ART/Models/Furniture";
    const string IconFolder = "Assets/ART/UI/FurnitureIcons";
    const string ItemFolder = "Assets/Data/Furniture";
    const string CatalogPath = "Assets/Data/FurnitureCatalog.asset";
    const int DefaultPrice = 10;

    static readonly Color PanelColor = new Color(0.16f, 0.12f, 0.22f, 0.96f);
    static readonly Color CardColor = new Color(0.26f, 0.20f, 0.34f, 1f);
    static readonly Color TabColor = new Color(0.40f, 0.30f, 0.52f, 1f);
    static readonly Color TextColor = new Color(0.96f, 0.93f, 0.88f, 1f);
    static readonly Color AccentColor = new Color(1f, 0.82f, 0.40f, 1f);

    // ---------- 3D asset -> store item ----------

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
        Debug.Log($"Added {created.Length} item(s) to {AssetDatabase.GetAssetPath(catalog)}. Set prices and categories in the Inspector.");
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
        item.category = prefab.GetComponent<Furniture>().Type;
        Sprite icon = CreateIcon(prefab);
        if (icon != null) item.icon = icon;
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

    // Saves Unity's asset preview thumbnail as a sprite. Returns null if no preview could be rendered.
    static Sprite CreateIcon(GameObject prefab)
    {
        Texture2D preview = AssetPreview.GetAssetPreview(prefab);
        // shortcut: previews render asynchronously, so poll briefly; rerun the menu item if an icon comes out empty.
        for (int i = 0; i < 40 && preview == null; i++)
        {
            System.Threading.Thread.Sleep(50);
            preview = AssetPreview.GetAssetPreview(prefab);
        }
        if (preview == null)
        {
            Debug.LogWarning($"No preview for {prefab.name}; assign its store icon by hand.", prefab);
            return null;
        }

        // Preview textures are not CPU-readable, so copy through a RenderTexture before encoding.
        RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(preview, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        EnsureFolder(IconFolder);
        string iconPath = $"{IconFolder}/{prefab.name}.png";
        File.WriteAllBytes(iconPath, readable.EncodeToPNG());
        Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(iconPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
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

    // ---------- Store UI ----------

    [MenuItem("GameObject/Study Sim/Furniture Store UI", false, 10)]
    static void CreateStoreUI()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // Panel docked to the right edge so the room stays visible while browsing.
        GameObject panel = Make("FurnitureStore", canvas.transform, typeof(Image), typeof(VerticalLayoutGroup));
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(1, 0);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 0.5f);
        panelRect.sizeDelta = new Vector2(480, -48);
        panelRect.anchoredPosition = new Vector2(-24, 0);
        Style(panel.GetComponent<Image>(), rounded, PanelColor);
        var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(20, 20, 20, 20);
        panelLayout.spacing = 14;
        panelLayout.childControlWidth = panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        // Header: title, coin balance, close.
        GameObject header = Make("Header", panel.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.GetComponent<LayoutElement>().preferredHeight = 48;
        var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 12;
        headerLayout.childControlWidth = headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        TMP_Text title = Text("Title", header.transform, "Furniture", 34, TextColor, TextAlignmentOptions.Left);
        title.fontStyle = FontStyles.Bold;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        TMP_Text coins = Text("Coins", header.transform, "0", 26, AccentColor, TextAlignmentOptions.Right);
        coins.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;
        Button close = TextButton("Close", header.transform, "X", rounded, TabColor, 48);

        // Category tabs. Values match Furniture.FurnitureType; -1 = all.
        GameObject tabs = Make("Tabs", panel.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tabs.GetComponent<LayoutElement>().preferredHeight = 40;
        var tabsLayout = tabs.GetComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8;
        tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
        tabsLayout.childForceExpandWidth = true;
        var tabButtons = new (string label, int value)[] { ("All", -1), ("Floor", 0), ("Wall", 1), ("Shelf", 2) }
            .Select(t => (button: TextButton(t.label, tabs.transform, t.label, rounded, TabColor, 0), t.value)).ToArray();

        // Scrollable grid of cards.
        GameObject scroll = Make("Scroll", panel.transform, typeof(ScrollRect), typeof(LayoutElement));
        scroll.GetComponent<LayoutElement>().flexibleHeight = 1;
        GameObject viewport = Make("Viewport", scroll.transform, typeof(RectMask2D));
        Stretch((RectTransform)viewport.transform);
        GameObject content = Make("Content", viewport.transform, typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(130, 170);
        grid.spacing = new Vector2(12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewport.transform;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30;

        // Card template (hidden at runtime, cloned per item).
        GameObject card = Make("CardTemplate", content.transform, typeof(Image), typeof(Button));
        Style(card.GetComponent<Image>(), rounded, CardColor);
        GameObject icon = Make("Icon", card.transform, typeof(Image));
        icon.GetComponent<Image>().preserveAspect = true;
        Anchor(icon, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.95f));
        Anchor(Text("Name", card.transform, "Item", 17, TextColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.36f));
        Anchor(Text("Price", card.transform, "10", 20, AccentColor, TextAlignmentOptions.Center).gameObject,
            new Vector2(0, 0.02f), new Vector2(1, 0.18f));

        // Wire it up.
        var store = panel.AddComponent<FurnitureStoreUI>();
        var so = new SerializedObject(store);
        so.FindProperty("catalog").objectReferenceValue = LoadOrCreateCatalog();
        so.FindProperty("cardContainer").objectReferenceValue = content.transform;
        so.FindProperty("cardTemplate").objectReferenceValue = card;
        so.FindProperty("coinsText").objectReferenceValue = coins;
        so.FindProperty("roomManager").objectReferenceValue = Object.FindFirstObjectByType<RoomManager>();
        so.FindProperty("playerCurrency").objectReferenceValue = Object.FindFirstObjectByType<PlayerCurrency>();
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddVoidPersistentListener(close.onClick, store.Close);
        foreach (var tab in tabButtons)
            UnityEventTools.AddIntPersistentListener(tab.button.onClick, store.ShowCategory, tab.value);

        Undo.RegisterCreatedObjectUndo(panel, "Create Furniture Store UI");
        Selection.activeGameObject = panel;
    }

    static GameObject Make(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (var c in components) go.AddComponent(c);
        return go;
    }

    static void Style(Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    static TMP_Text Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tmp = Make(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button TextButton(string name, Transform parent, string label, Sprite sprite, Color color, float width)
    {
        GameObject go = Make(name, parent, typeof(Image), typeof(Button));
        Style(go.GetComponent<Image>(), sprite, color);
        if (width > 0) go.AddComponent<LayoutElement>().preferredWidth = width;
        Stretch((RectTransform)Text("Label", go.transform, label, 20, TextColor, TextAlignmentOptions.Center).transform);
        return go.GetComponent<Button>();
    }

    static void Anchor(GameObject go, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
