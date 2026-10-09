using UnityEngine;

// Renders a furniture prefab to a sprite so every store item has an icon, including newly added ones.
public static class FurnitureIconRenderer
{
    // Far from the room so nothing else in the scene lands in the shot.
    static readonly Vector3 StagePosition = new Vector3(0, -5000, 0);

    public static Sprite Render(GameObject prefab, int size = 256)
    {
        GameObject model = Object.Instantiate(prefab, StagePosition, prefab.transform.rotation);
        foreach (Behaviour b in model.GetComponentsInChildren<Behaviour>())
            if (!(b is Light)) b.enabled = false; // no Furniture/gameplay scripts running on the stand-in
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) c.enabled = false;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(model);
            return null;
        }
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        var camGO = new GameObject("IconCamera");
        var cam = camGO.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.orthographic = true;
        cam.orthographicSize = bounds.extents.magnitude;
        // Same three-quarter angle as the isometric room camera so icons match what gets placed.
        Vector3 dir = Quaternion.Euler(30, 45, 0) * Vector3.back;
        float distance = bounds.extents.magnitude * 4f;
        cam.transform.position = bounds.center + dir * distance;
        cam.transform.LookAt(bounds.center);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = distance * 2f;

        var lightGO = new GameObject("IconLight");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(40, 20, 0);

        var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        // Immediate so the stand-in and extra light never show up in this frame's main camera render.
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(lightGO);
        Object.DestroyImmediate(model);

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
