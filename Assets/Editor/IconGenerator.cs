using UnityEngine;
using UnityEditor;
using System.IO;

public class IconGenerator
{
    const int ICON_SIZE = 512;
    const float DEFAULT_ZOOM_FACTOR = 0.4f; // <1 = zoom in, >1 = zoom out

    [MenuItem("Tools/Generate Item Icons")]
    static void GenerateIcons()
    {
        string iconFolder = "Assets/Icons";
        if (!Directory.Exists(iconFolder))
            Directory.CreateDirectory(iconFolder);

        // Create temporary camera
        GameObject camGO = new GameObject("IconCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0); // transparent
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;

        // Light
        Light light = new GameObject("IconLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        RenderTexture rt = new RenderTexture(ICON_SIZE, ICON_SIZE, 0, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ItemPrefabs" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            GameObject item = GameObject.Instantiate(prefab);

            // Calculate bounds
            Bounds bounds = GetBounds(item);

            // Center prefab at origin
            item.transform.position = -bounds.center;

            // Optional: scale very small objects
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize < 1f)
                item.transform.localScale *= 1f / maxSize;

            // Set rotation (isometric)
            item.transform.rotation = Quaternion.Euler(30, 45, 0);

            // Orthographic zoom to fit object
            float zoomFactor = DEFAULT_ZOOM_FACTOR; // tweak this to zoom in/out
            cam.orthographicSize = maxSize * 0.5f * zoomFactor;

            // Position camera in front
            cam.transform.position = new Vector3(0, 0, -10);
            cam.transform.LookAt(Vector3.zero);

            // Render to texture
            RenderTexture.active = rt;
            cam.Render();

            Texture2D tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, ICON_SIZE, ICON_SIZE), 0, 0);
            tex.Apply();

            // Save PNG
            byte[] png = tex.EncodeToPNG();
            string fileName = prefab.name + "_Icon.png";
            string savePath = Path.Combine(iconFolder, fileName);
            File.WriteAllBytes(savePath, png);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(item);
        }

        // Cleanup
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(light.gameObject);

        AssetDatabase.Refresh();
        Debug.Log("Item icons generated!");
    }

    static Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        return bounds;
    }
}
