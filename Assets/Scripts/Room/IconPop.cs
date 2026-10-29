using UnityEngine;
using UnityEngine.Rendering;

// Little pixel shapes (hearts, sparkles, music notes) that float up off something and fade. Sized and moved by
// the camera distance, so they read the same on screen whatever the room's scale.
public static class IconPop
{
    public static readonly string[] Heart = { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X...", "......." };
    public static readonly string[] Sparkle = { "...X...", "...X...", "..XXX..", "XXXXXXX", "..XXX..", "...X...", "...X..." };
    public static readonly string[] Note = { "...XXX.", "...X.XX", "...X...", "...X...", ".XXX...", "XXXX...", ".XX...." };
    public static readonly Color[] Sweets = { new Color(1f, 0.62f, 0.78f), new Color(1f, 0.9f, 0.7f), new Color(0.82f, 0.74f, 1f), Color.white };

    // One particle system per use, each particle a random one of the given 7x7 shapes (white, tinted in Burst).
    // It sits at the scene root so no parent's scale or UI layer touches it.
    public static ParticleSystem Make(string name, float lifetime, params string[][] shapes)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = lifetime;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = 200;
        var emission = ps.emission;
        emission.enabled = false; // only Emit() from Burst
        var drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.limit = 0f;
        drag.dampen = 0.06f; // shoot up, then hang and drift
        var grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(0.15f, 1.15f), new Keyframe(0.3f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1, 0)));
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = shapes.Length;
        sheet.numTilesY = 1;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, shapes.Length - 0.01f); // a random shape each

        // Shapes side by side, each in a 9x9 tile with a clear border so neighbours don't bleed.
        var tex = new Texture2D(9 * shapes.Length, 9, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(new Color[tex.width * tex.height]); // clear
        for (int i = 0; i < shapes.Length; i++)
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 7; x++)
                    if (shapes[i][y][x] == 'X') tex.SetPixel(i * 9 + 1 + x, 7 - y, Color.white); // rows read top-down, texture rows go bottom-up
        tex.Apply();

        var material = new Material(Resources.Load<Shader>("Effects/ParticleSprite")); // Resources, so builds have it
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.mainTexture = tex;
        var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // Emits count shapes from inside the top part of b, each tinted a random one of tints. speed 1 is a quick pop;
    // lower drifts.
    public static void Burst(ParticleSystem ps, Bounds b, int count, float speed, params Color[] tints)
    {
        if (ps == null) return;
        Camera cam = Camera.main;
        float dist = cam != null ? Vector3.Distance(cam.transform.position, b.center) : 10f;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            p.position = new Vector3(b.center.x + Random.Range(-0.4f, 0.4f) * b.size.x, b.min.y + Random.Range(0.3f, 1f) * b.size.y, b.center.z + Random.Range(-0.4f, 0.4f) * b.size.z);
            p.velocity = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.12f, 0.24f) * speed, Random.Range(-0.08f, 0.08f)) * dist;
            p.startSize = dist * Random.Range(0.012f, 0.02f);
            p.startColor = tints[Random.Range(0, tints.Length)];
            ps.Emit(p, 1);
        }
    }

    // World bounds of everything drawn under go, or null when it draws nothing.
    public static Bounds? BoundsOf(GameObject go)
    {
        if (go == null) return null;
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return null;
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
