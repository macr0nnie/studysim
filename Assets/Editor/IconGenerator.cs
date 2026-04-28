using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class IconGeneratorWindow : EditorWindow
{
    private string sourceFolder = "Assets/ItemPrefabs";
    private string outputFolder = "Assets/Icons";
    private int iconSize = 512;
    private Vector3 itemRotation = new Vector3(30f, 45f, 0f);
    private float zoomFactor = 0.55f;
    private float lightIntensity = 1.2f;
    private Vector3 lightRotation = new Vector3(50f, -30f, 0f);
    private bool skipExisting = true;
    private bool searchSubfolders = true;

    private List<string> prefabPaths = new List<string>();
    private Vector2 scrollPos;

    [MenuItem("Tools/Icon Generator")]
    static void Open() => GetWindow<IconGeneratorWindow>("Icon Generator");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Icon Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Folders", EditorStyles.boldLabel);
        sourceFolder = EditorGUILayout.TextField("Source Folder", sourceFolder);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        searchSubfolders = EditorGUILayout.Toggle("Search Subfolders", searchSubfolders);
        skipExisting = EditorGUILayout.Toggle("Skip Existing", skipExisting);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Render Settings", EditorStyles.boldLabel);
        iconSize = EditorGUILayout.IntPopup("Icon Size", iconSize,
            new[] { "128", "256", "512", "1024" },
            new[] { 128, 256, 512, 1024 });
        zoomFactor = EditorGUILayout.Slider("Zoom Factor", zoomFactor, 0.1f, 2f);
        itemRotation = EditorGUILayout.Vector3Field("Item Rotation", itemRotation);
        lightIntensity = EditorGUILayout.Slider("Light Intensity", lightIntensity, 0f, 3f);
        lightRotation = EditorGUILayout.Vector3Field("Light Rotation", lightRotation);

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan for Prefabs"))
            ScanPrefabs();

        if (prefabPaths.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found {prefabPaths.Count} prefab(s):", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(150));
            foreach (string p in prefabPaths)
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(p), EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button($"Generate {prefabPaths.Count} Icon(s)"))
                GenerateIcons();
        }
    }

    void ScanPrefabs()
    {
        prefabPaths.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { sourceFolder });
        foreach (string guid in guids)
            prefabPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
        Repaint();
    }

    void GenerateIcons()
    {
        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        // Render items far above the scene so they don't interact with scene lighting/objects
        Vector3 renderOrigin = new Vector3(0f, 5000f, 0f);

        GameObject camGO = new GameObject("__IconCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 500f;

        GameObject lightGO = new GameObject("__IconLight");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = lightIntensity;
        light.transform.rotation = Quaternion.Euler(lightRotation);
        light.transform.position = renderOrigin;

        RenderTexture rt = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        int generated = 0;
        int skipped = 0;

        try
        {
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                string prefabName = Path.GetFileNameWithoutExtension(path);
                string savePath = Path.Combine(outputFolder, prefabName + "_Icon.png");

                bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                    "Generating Icons",
                    $"{prefabName}  ({i + 1} / {prefabPaths.Count})",
                    (float)i / prefabPaths.Count);
                if (cancelled) break;

                if (skipExisting && File.Exists(savePath))
                {
                    skipped++;
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                GameObject item = Object.Instantiate(prefab);

                Bounds bounds = GetRendererBounds(item);
                float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (maxSize < 0.01f) maxSize = 1f;

                item.transform.position = renderOrigin - bounds.center;
                item.transform.rotation = Quaternion.Euler(itemRotation);

                cam.orthographicSize = maxSize * 0.5f * zoomFactor;
                cam.transform.position = renderOrigin + new Vector3(0f, 0f, -(maxSize * 5f));
                cam.transform.LookAt(renderOrigin);

                RenderTexture.active = rt;
                cam.Render();

                Texture2D tex = new Texture2D(iconSize, iconSize, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, iconSize, iconSize), 0, 0);
                tex.Apply();

                File.WriteAllBytes(savePath, tex.EncodeToPNG());

                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(item);
                generated++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO);
        }

        AssetDatabase.Refresh();
        Debug.Log($"Icons done — generated: {generated}, skipped: {skipped}, total: {prefabPaths.Count}");
    }

    static Bounds GetRendererBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
