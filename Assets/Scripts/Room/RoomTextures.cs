using System;
using System.Collections.Generic;
using UnityEngine;

// Textured wall and floor materials for the Paint drawer (wallpaper, brick, panels, planks, tiles...), drawn in code
// so they need no image assets. PaintUI lists them after the pack's plain materials in Resources/RoomSurfaces.
// Made once per run and shared; a colour picked in the drawer tints a grey copy of them (Grey).
public static class RoomTextures
{
    private const int Size = 128;
    private static Material[] walls, floors;
    private static readonly Dictionary<Material, Material> greys = new Dictionary<Material, Material>();

    public static Material[] Walls => walls ??= new[]
    {
        Make("Striped Wallpaper", 3f, 0f, (x, y) => x % 32 < 16 ? Hex(0xEFE4CF) : Hex(0xC9D6B8)),
        Make("Polka Dot Wallpaper", 3f, 0f, (x, y) =>
        {
            int cx = (x + (y / 32 % 2) * 16) % 32 - 16, cy = y % 32 - 16;
            return cx * cx + cy * cy < 25 ? Hex(0xE8A9A0) : Hex(0xF6EBDD);
        }),
        Make("Brick", 4f, 6f, (x, y) =>
        {
            int row = y / 16, bx = (x + (row % 2) * 16) % 32;
            if (y % 16 < 2 || bx < 2) return Hex(0xD9CFC1); // mortar
            return Shade(Hex(0xB5654A), Noise(x / 32 + row * 7, row) * 0.18f + Noise(x, y) * 0.06f);
        }),
        Make("Wood Panels", 3f, 5f, (x, y) =>
        {
            if (x % 32 < 2) return Hex(0x6E4A32); // groove between boards
            return Shade(Hex(0xA87A55), Mathf.Sin(x * 0.9f + Noise(x / 32, 0) * 6f) * 0.04f + Noise(x, y / 8) * 0.05f + Noise(x / 32, 3) * 0.1f);
        }),
        Make("Plaster", 2f, 3f, (x, y) => Shade(Hex(0xEADFD0), Noise(x / 4, y / 4) * 0.06f + Noise(x, y) * 0.03f)),
        // Soft, flat patterns for the low-poly look: clean shapes, no grain.
        Make("Gingham", 2f, 0f, (x, y) => ((x % 16 < 8 ? 1 : 0) + (y % 16 < 8 ? 1 : 0)) switch
        {
            2 => Hex(0xE3A27A),
            1 => Hex(0xF0CBAA),
            _ => Hex(0xF7EEDF),
        }),
        Make("Tiny Flowers", 3f, 0f, (x, y) =>
        {
            int cx = (x + (y / 32 % 2) * 16) % 32 - 16, cy = y % 32 - 16;
            if (cx * cx + cy * cy < 5) return Hex(0xF2C14E);
            foreach ((int px, int py) in new[] { (5, 0), (-5, 0), (0, 5), (0, -5) })
                if ((cx - px) * (cx - px) + (cy - py) * (cy - py) < 9) return Hex(0xEFA7B5);
            return Hex(0xFAF1E6);
        }),
        Make("Soft Stripes", 3f, 0f, (x, y) => x % 32 < 12 ? Hex(0xDDE7D2) : Hex(0xF6F0E4)),
    };

    public static Material[] Floors => floors ??= new[]
    {
        Make("Wood Planks", 4f, 6f, (x, y) =>
        {
            int row = y / 16, joint = (x + row * 37) % 128;
            if (y % 16 < 1 || joint < 1) return Hex(0x5C3D29);
            return Shade(Hex(0x9C6B45), Noise(row, (x + row * 37) / 128) * 0.15f + Mathf.Sin(x * 0.3f + row) * 0.03f + Noise(x, y) * 0.04f);
        }),
        Make("Checker Tiles", 4f, 3f, (x, y) =>
        {
            if (x % 32 < 1 || y % 32 < 1) return Hex(0xBFB6AA); // grout
            return (x / 32 + y / 32) % 2 == 0 ? Hex(0xF2EDE4) : Hex(0x3F4A5A);
        }),
        Make("Soft Carpet", 6f, 4f, (x, y) => Shade(Hex(0xB89BC4), Noise(x, y) * 0.08f + Noise(x / 8, y / 8) * 0.05f)),
        Make("Terracotta Tiles", 4f, 5f, (x, y) =>
        {
            if (x % 32 < 2 || y % 32 < 2) return Hex(0xD8CBB8);
            return Shade(Hex(0xC27A55), Noise(x / 32, y / 32) * 0.12f + Noise(x, y) * 0.04f);
        }),
        // Low-poly floors: wide boards in a few flat shades, thin soft seams.
        Make("Honey Planks", 2f, 2f, (x, y) =>
        {
            int row = y / 32, joint = (x + row * 48) % 128;
            if (y % 32 < 2 || joint < 2) return Hex(0xB97A4A);
            return row % 3 == 0 ? Hex(0xE2A86E) : row % 3 == 1 ? Hex(0xD99C62) : Hex(0xE8B47E);
        }),
        Make("Pale Planks", 2f, 2f, (x, y) =>
        {
            int row = y / 32, joint = (x + row * 48) % 128;
            if (y % 32 < 2 || joint < 2) return Hex(0xB9A88E);
            return row % 2 == 0 ? Hex(0xE9DCC4) : Hex(0xE1D2B6);
        }),
        Make("Basket Weave", 3f, 2f, (x, y) =>
        {
            bool across = (x / 32 + y / 32) % 2 == 0;
            if (x % 32 < 1 || y % 32 < 1 || (across ? y % 16 < 1 : x % 16 < 1)) return Hex(0xA8743F);
            return across ? Hex(0xD9A066) : Hex(0xCF945B);
        }),
        Make("Sage Tiles", 4f, 2f, (x, y) =>
        {
            if (x % 32 < 2 || y % 32 < 2) return Hex(0xF3EEE3);
            return (x / 32 + y / 32) % 2 == 0 ? Hex(0xA9C2A0) : Hex(0xB7CDAE);
        }),
    };

    // A tiling texture painted pixel by pixel, on a URP Lit material (the shader every room material already uses).
    // bump > 0 adds a normal map from the pattern's brightness, so grooves, grout and grain catch the window light.
    private static Material Make(string name, float tiling, float bump, Func<int, int, Color> pixel)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel(x, y);
        texture.SetPixels(pixels);
        texture.Apply(true, true); // mipmaps, then free the CPU copy

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
        material.mainTexture = texture; // PaintUI's swatch reads mainTexture
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.15f);
        if (bump > 0) Bump(material, pixels, bump);
        return material;
    }

    // A grey copy of a textured material, so a colour picked on top shows as that colour instead of mixing with the
    // pattern's own (ronnie, 2026-10-19). The pattern keeps its light and dark; its lightest pixel becomes white.
    // Read back through the GPU, as the pack's textures and ours aren't CPU-readable.
    public static Material Grey(Material material)
    {
        if (material == null || material.mainTexture == null) return material;
        if (greys.TryGetValue(material, out Material grey)) return grey;
        Texture source = material.mainTexture;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true) { name = source.name + " Grey", wrapMode = TextureWrapMode.Repeat, filterMode = source.filterMode, anisoLevel = 4 };
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] pixels = texture.GetPixels();
        float top = 0.01f;
        foreach (Color c in pixels) top = Mathf.Max(top, c.grayscale);
        for (int i = 0; i < pixels.Length; i++)
        {
            float l = pixels[i].grayscale / top;
            pixels[i] = new Color(l, l, l, pixels[i].a);
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);

        grey = new Material(material) { name = material.name + " Grey" };
        grey.SetTexture("_BaseMap", texture);
        grey.mainTexture = texture;
        greys[material] = grey;
        return grey;
    }

    // Height is the pattern's brightness (grooves and grout are the darker pixels); the slope between neighbours,
    // wrapping at the edges so it tiles, becomes a tangent-space normal.
    private static void Bump(Material material, Color[] pixels, float strength)
    {
        var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true) { name = material.name + " Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
        float H(int x, int y) => pixels[(y + Size) % Size * Size + (x + Size) % Size].grayscale;
        var normals = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector3 n = new Vector3(-(H(x + 1, y) - H(x - 1, y)) * strength, -(H(x, y + 1) - H(x, y - 1)) * strength, 1f).normalized;
                normals[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f); // alpha 1: URP reads it as plain RGB
            }
        normal.SetPixels(normals);
        normal.Apply(true, true);
        material.SetTexture("_BumpMap", normal);
        material.SetTextureScale("_BumpMap", material.GetTextureScale("_BaseMap"));
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
    }

    private static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);

    // Lighter (positive) or darker (negative) by a fraction.
    private static Color Shade(Color c, float by) => by >= 0 ? Color.Lerp(c, Color.white, by) : Color.Lerp(c, Color.black, -by);

    // Repeatable value in -1..1 for a cell, so the pattern tiles the same every run.
    private static float Noise(int x, int y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f) * 2f - 1f;
}
