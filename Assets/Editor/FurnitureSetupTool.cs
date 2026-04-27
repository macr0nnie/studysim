using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class FurnitureSetupTool : EditorWindow
{
    private List<GameObject> furniturePrefabs = new List<GameObject>();
    private Vector2 scrollPos;
    private string iconOutputPath = "Assets/FurnitureIcons";
    private int iconResolution = 256;
    private Color backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    private float cameraDistance = 3f;
    private Vector3 cameraAngle = new Vector3(20f, -30f, 0f);

    [MenuItem("Tools/Furniture Setup Assistant")]
    public static void ShowWindow()
    {
        GetWindow<FurnitureSetupTool>("Furniture Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Furniture Setup Assistant", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Icon Settings
        GUILayout.Label("Icon Generation Settings", EditorStyles.boldLabel);
        iconOutputPath = EditorGUILayout.TextField("Icon Output Path", iconOutputPath);
        iconResolution = EditorGUILayout.IntSlider("Icon Resolution", iconResolution, 64, 512);
        backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
        cameraDistance = EditorGUILayout.Slider("Camera Distance", cameraDistance, 1f, 10f);
        cameraAngle = EditorGUILayout.Vector3Field("Camera Angle", cameraAngle);

        EditorGUILayout.Space();

        // Prefab List
        GUILayout.Label("Furniture Prefabs", EditorStyles.boldLabel);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
        
        for (int i = 0; i < furniturePrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            furniturePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(
                furniturePrefabs[i], typeof(GameObject), false);
            
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                furniturePrefabs.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Prefab Slot"))
        {
            furniturePrefabs.Add(null);
        }
        
        if (GUILayout.Button("Add Selected Objects"))
        {
            AddSelectedPrefabs();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Action Buttons
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Setup All Furniture", GUILayout.Height(40)))
        {
            SetupAllFurniture();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool will:\n" +
            "1. Add FurnitureItem script to prefabs\n" +
            "2. Generate icon screenshots\n" +
            "3. Assign icons to the FurnitureItem component",
            MessageType.Info);
    }

    private void AddSelectedPrefabs()
    {
        foreach (Object obj in Selection.objects)
        {
            GameObject prefab = obj as GameObject;
            if (prefab != null && !furniturePrefabs.Contains(prefab))
            {
                furniturePrefabs.Add(prefab);
            }
        }
    }

    private void SetupAllFurniture()
    {
        if (!Directory.Exists(iconOutputPath))
        {
            Directory.CreateDirectory(iconOutputPath);
        }

        int successCount = 0;
        int totalCount = furniturePrefabs.Count;

        for (int i = 0; i < furniturePrefabs.Count; i++)
        {
            GameObject prefab = furniturePrefabs[i];
            
            if (prefab == null)
            {
                Debug.LogWarning($"Skipping null prefab at index {i}");
                continue;
            }

            EditorUtility.DisplayProgressBar("Setting up Furniture", 
                $"Processing {prefab.name}...", (float)i / totalCount);

            try
            {
                SetupFurniturePrefab(prefab);
                successCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to setup {prefab.name}: {e.Message}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Setup Complete", 
            $"Successfully set up {successCount} out of {totalCount} furniture items!", "OK");
    }

    private void SetupFurniturePrefab(GameObject prefab)
    {
        // Add FurnitureItem component if it doesn't exist
        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        
        FurnitureItem furnitureItem = prefabInstance.GetComponent<FurnitureItem>();
        if (furnitureItem == null)
        {
            furnitureItem = prefabInstance.AddComponent<FurnitureItem>();
        }

        // Set default values
        furnitureItem.furnitureName = prefab.name;

        // Generate icon
        Sprite icon = GenerateIcon(prefabInstance);
        if (icon != null)
        {
            furnitureItem.icon = icon;
        }

        // Save changes to prefab
        PrefabUtility.SaveAsPrefabAsset(prefabInstance, AssetDatabase.GetAssetPath(prefab));
        PrefabUtility.UnloadPrefabContents(prefabInstance);

        Debug.Log($"Successfully set up furniture: {prefab.name}");
    }

    private Sprite GenerateIcon(GameObject furnitureObj)
    {
        // Create temporary camera
        GameObject camObj = new GameObject("TempIconCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.orthographic = true;
        cam.cullingMask = 1 << LayerMask.NameToLayer("Default");

        // Calculate bounds
        Bounds bounds = CalculateBounds(furnitureObj);
        
        // Position camera
        cam.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * 0.6f;
        camObj.transform.position = bounds.center - Quaternion.Euler(cameraAngle) * Vector3.forward * cameraDistance;
        camObj.transform.LookAt(bounds.center);
        camObj.transform.rotation = Quaternion.Euler(cameraAngle);

        // Render to texture
        RenderTexture rt = new RenderTexture(iconResolution, iconResolution, 24);
        cam.targetTexture = rt;
        
        RenderTexture.active = rt;
        cam.Render();

        // Save as PNG
        Texture2D screenshot = new Texture2D(iconResolution, iconResolution, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, iconResolution, iconResolution), 0, 0);
        screenshot.Apply();

        byte[] bytes = screenshot.EncodeToPNG();
        string iconPath = $"{iconOutputPath}/{furnitureObj.name}_Icon.png";
        File.WriteAllBytes(iconPath, bytes);

        // Cleanup
        RenderTexture.active = null;
        DestroyImmediate(camObj);
        DestroyImmediate(rt);
        DestroyImmediate(screenshot);

        // Import and convert to sprite
        AssetDatabase.ImportAsset(iconPath);
        TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = iconResolution;
            AssetDatabase.WriteImportSettingsIfDirty(iconPath);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
    }

    private Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(obj.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}

// The Furniture Item component that will be attached to each prefab
[System.Serializable]
public class FurnitureItem : MonoBehaviour
{
    [Header("Furniture Information")]
    public string furnitureName;
    public Sprite icon;
    
    [TextArea(3, 5)]
    public string description;
    
    [Header("Gameplay Properties")]
    public FurnitureCategory category = FurnitureCategory.General;
    public Vector2Int gridSize = Vector2Int.one;
    public bool canRotate = true;
    public int cost = 100;
    
    [Header("Placement Settings")]
    public bool snapToGrid = true;
    public float placementHeight = 0f;
    public bool requiresFloorSpace = true;
    
    private void OnDrawGizmos()
    {
        if (snapToGrid)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * placementHeight, 
                new Vector3(gridSize.x, 0.1f, gridSize.y)
            );
        }
    }
}

public enum FurnitureCategory
{
    General,
    Seating,
    Tables,
    Storage,
    Decoration,
    Lighting,
    Kitchen,
    Bathroom,
    Bedroom,
    Outdoor
}