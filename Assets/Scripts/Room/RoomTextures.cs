using System;
using UnityEngine;

// Textured wall and floor materials for the Paint drawer (wallpaper, brick, panels, planks, tiles...), drawn in code
// so they need no image assets. PaintUI lists them after the pack's plain materials in Resources/RoomSurfaces.
// Made once per run and shared; a colour picked in the drawer tints them like any other material.
public static class RoomTextures
{
    private const int Size = 128;
    private static Material[] walls, floors;

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
