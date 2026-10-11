using System.Collections.Generic;
using UnityEngine;

// Paints only a piece's main part instead of flooding every part with one colour. The pack models colour their
// parts either with one material per part or with UVs into a palette atlas of flat swatches (Mnostva's
// Cartoon_Texture). So: measure which material and swatch covers the most surface, then recolour that material,
// or, for an atlas, a copy of the texture with only that swatch changed (shading kept). Needs readable meshes
// (Read/Write on in the model import); returns false otherwise and the caller tints the whole piece as before.
public static class PieceRecolor
{
    const int MaxSize = 256;  // the atlases are flat swatches, so a small copy loses nothing
    const int Tolerance = 24; // per channel, out of 255: texels this close to the main swatch count as it
    static readonly Dictionary<Texture, Texture2D> readable = new Dictionary<Texture, Texture2D>();
    static readonly Dictionary<string, Material> painted = new Dictionary<string, Material>();

    public static bool Apply(Renderer[] renderers, Color color)
    {
        // Surface area per (material, swatch) over every triangle.
        var area = new Dictionary<KeyValuePair<Material, Color32>, float>();
        foreach (Renderer r in renderers)
        {
            if (r == null || r.name == PieceOutline.HullName) continue;
            Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent(out MeshFilter f) ? f.sharedMesh : null;
            if (mesh == null) continue;
            if (!mesh.isReadable) return false;
            Vector3[] v = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Material[] mats = r.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount && sub < mats.Length; sub++)
            {
                Material m = mats[sub];
                if (m == null || !(m.HasProperty("_BaseColor") || m.HasProperty("_Color"))) continue;
                Texture2D tex = uv.Length == v.Length ? Readable(m.mainTexture) : null;
                int[] tris = mesh.GetTriangles(sub);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    float size = Vector3.Cross(v[b] - v[a], v[c] - v[a]).magnitude;
                    Color32 swatch = tex == null ? new Color32(255, 255, 255, 255) : Quantize(tex.GetPixelBilinear(
                        Mathf.Repeat((uv[a].x + uv[b].x + uv[c].x) / 3f, 1f), Mathf.Repeat((uv[a].y + uv[b].y + uv[c].y) / 3f, 1f)));
                    var key = new KeyValuePair<Material, Color32>(m, swatch);
                    area[key] = (area.TryGetValue(key, out float sum) ? sum : 0f) + size;
                }
            }
        }
        if (area.Count == 0) return false;

        var main = default(KeyValuePair<Material, Color32>);
        float best = -1f;
        foreach (var e in area) if (e.Value > best) { best = e.Value; main = e.Key; }

        Material recoloured = Painted(main.Key, main.Value, color);
        foreach (Renderer r in renderers)
        {
            if (r == null || r.name == PieceOutline.HullName) continue;
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == main.Key) { mats[i] = recoloured; changed = true; }
            if (changed) r.sharedMaterials = mats;
        }
        return true;
    }

    // Shared between pieces painted the same, so a room of red chairs holds one material, not one each.
    static Material Painted(Material source, Color32 swatch, Color color)
    {
        string key = source.GetInstanceID() + "/" + ColorUtility.ToHtmlStringRGB(Quantize(swatch)) + "/" + ColorUtility.ToHtmlStringRGB(color);
        if (painted.TryGetValue(key, out Material done) && done != null) return done;
        var m = new Material(source) { name = source.name + " (painted)" };
        Texture2D tex = Readable(source.mainTexture);
        if (tex == null)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }
        else
        {
            Color32[] pixels = tex.GetPixels32();
            Recolour(pixels, swatch, color);
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, true) { name = tex.name + " (painted)", wrapMode = tex.wrapMode, filterMode = tex.filterMode };
            copy.SetPixels32(pixels);
            copy.Apply(true, true);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", copy);
            m.mainTexture = copy;
        }
        return painted[key] = m;
    }

    // Texels near the swatch take the new colour, keeping their brightness relative to it (edges, baked shading).
    public static void Recolour(Color32[] pixels, Color32 swatch, Color color)
    {
        float baseLum = Mathf.Max(Lum(swatch), 0.05f);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (Mathf.Abs(p.r - swatch.r) > Tolerance || Mathf.Abs(p.g - swatch.g) > Tolerance || Mathf.Abs(p.b - swatch.b) > Tolerance) continue;
            float k = Lum(p) / baseLum;
            Color c = new Color(Mathf.Clamp01(color.r * k), Mathf.Clamp01(color.g * k), Mathf.Clamp01(color.b * k), p.a / 255f);
            pixels[i] = c;
        }
    }

    static float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

    // Swatch keys ignore the last bits, so bilinear blur at a UV doesn't split one swatch into many.
    static Color32 Quantize(Color c) { Color32 q = c; return new Color32((byte)(q.r & 0xF0), (byte)(q.g & 0xF0), (byte)(q.b & 0xF0), 255); }

    // A small CPU copy of a (usually unreadable, compressed) texture, via the GPU.
    static Texture2D Readable(Texture source)
    {
        if (source == null) return null;
        if (readable.TryGetValue(source, out Texture2D done) && done != null) return done;
        int w = Mathf.Min(source.width, MaxSize), h = Mathf.Min(source.height, MaxSize);
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = source.name, wrapMode = source.wrapMode, filterMode = source.filterMode };
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return readable[source] = copy;
    }
}
