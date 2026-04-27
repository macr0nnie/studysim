
using UnityEngine;
using UnityEditor;
using System.IO;

public class IconGenerator
{
    const int ICON_SIZE = 512;

    [MenuItem("Tools/Generate Item Icons")]
    static void GenerateIcons()
    {
        // Temporary camera
        GameObject camGO = new GameObject("IconCamera");
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1.8f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 10f;

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
            item.transform.position = Vector3.zero;
            item.transform.rotation = Quaternion.Euler(20, -30, 0);

            cam.transform.position = new Vector3(0, 0, -3);
            cam.transform.LookAt(item.transform);

            RenderTexture.active = rt;
            cam.Render();

            Texture2D tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, ICON_SIZE, ICON_SIZE), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            string fileName = prefab.name + "_Icon.png";
            string savePath = "Assets/Icons/" + fileName;

            File.WriteAllBytes(savePath, png);

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(item);
        }

        RenderTexture.active = null;
        cam.targetTexture = null;

        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(light.gameObject);

        AssetDatabase.Refresh();
        Debug.Log("Item icons generated!");
    }
}


//IconGenerator