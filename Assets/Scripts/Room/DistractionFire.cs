using System.Collections.Generic;
using UnityEngine;

// Small flames on a few pieces of furniture while the player is distracted: particles drawn with Fire.shadergraph
// (Resources/Effects), a few embers drifting up, and a dim flickering warm light on each. They grow with the
// distraction level, follow the piece as it tilts, and die down when the room mends. Distraction owns the lifetime.
public class DistractionFire
{
    private const int MaxFires = 4;

    private class Fire
    {
        public Transform piece;
        public Vector3 localTop;   // top of the piece in its own space, so the fire rides along as it keels over
        public Transform root;
        public ParticleSystem flames, embers;
        public Light light;
        public float size, delay, phase;
    }

    private static Material material;
    private readonly List<Fire> fires = new List<Fire>();

    public bool Lit => fires.Count > 0;

    public void Ignite(IEnumerable<Transform> pieces)
    {
        Clear();
        var candidates = new List<Transform>(pieces);
        for (int i = candidates.Count - 1; i > 0; i--) // shuffle, so a different few catch each time
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        foreach (Transform piece in candidates)
        {
            if (fires.Count >= MaxFires) break;
            if (piece == null || !TryBounds(piece, out Bounds b)) continue;
            fires.Add(Make(piece, b, fires.Count));
        }
    }

    // amount: 0 (out) to 1 (fully burning), the distraction level.
    public void Update(float amount)
    {
        float time = Time.time;
        foreach (Fire f in fires)
        {
            if (f.root == null) continue;
            if (f.piece != null) f.root.position = f.piece.TransformPoint(f.localTop);
            float a = Mathf.Clamp01((amount - f.delay) / (1 - f.delay));
            var flames = f.flames.emission;
            flames.rateOverTime = 14f * a;
            var embers = f.embers.emission;
            embers.rateOverTime = 5f * a;
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(time * 9f, f.phase);
            // Inverse-square falloff: about 0.5 brightness a piece-width away, so it warms the furniture without blowing out.
            f.light.intensity = 0.5f * f.size * f.size * a * flicker;
            f.light.enabled = a > 0;
        }
    }

    public void Clear()
    {
        foreach (Fire f in fires)
            if (f.root != null) Object.Destroy(f.root.gameObject);
        fires.Clear();
    }

    private static bool TryBounds(Transform piece, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            if (any) bounds.Encapsulate(r.bounds); else { bounds = r.bounds; any = true; }
        }
        return any;
    }

    private static Fire Make(Transform piece, Bounds b, int index)
    {
        float size = Mathf.Max(0.05f, Mathf.Min(b.size.x, b.size.z));
        Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
        var root = new GameObject("Distraction Fire").transform;
        root.position = top;

        var fire = new Fire
        {
            piece = piece,
            localTop = piece.InverseTransformPoint(top),
            root = root,
            size = size,
            delay = index * 0.12f + Random.Range(0f, 0.08f), // they catch one after another
            phase = Random.Range(0f, 10f),
        };
        fire.flames = Particles(root, "Flames", size, b, false);
        fire.embers = Particles(root, "Embers", size, b, true);

        var lightGo = new GameObject("Fire Light");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = Vector3.up * size * 0.3f;
        fire.light = lightGo.AddComponent<Light>();
        fire.light.type = LightType.Point;
        fire.light.color = new Color(1f, 0.52f, 0.22f);
        fire.light.range = size * 5f;
        fire.light.intensity = 0;
        fire.light.shadows = LightShadows.None;
        return fire;
    }

    private static ParticleSystem Particles(Transform root, string name, float size, Bounds b, bool embers)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0;
        main.maxParticles = embers ? 30 : 40;
        main.startLifetime = embers ? new ParticleSystem.MinMaxCurve(1.4f, 2.4f) : new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSize = embers ? new ParticleSystem.MinMaxCurve(size * 0.04f, size * 0.07f) : new ParticleSystem.MinMaxCurve(size * 0.35f, size * 0.55f);
        // The shader reads red as a per-flame random offset, so flames never move in step; alpha is the fade.
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0, 1, 1, 1), new Color(1, 1, 1, 1));

        var emission = ps.emission;
        emission.rateOverTime = 0;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(b.size.x * 0.55f, 0.001f, b.size.z * 0.55f);
        shape.rotation = Vector3.zero;

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(embers ? -size * 0.15f : 0f, embers ? size * 0.15f : 0f);
        velocity.y = embers ? new ParticleSystem.MinMaxCurve(size * 0.5f, size * 0.9f) : new ParticleSystem.MinMaxCurve(size * 0.12f, size * 0.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(embers ? -size * 0.15f : 0f, embers ? size * 0.15f : 0f);

        var grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = embers
            ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.4f))
            : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1, 0.35f)));

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0, 1) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = embers ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.VerticalBillboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = FireMaterial();
        renderer.sortingFudge = embers ? 1 : 0;

        ps.Play();
        return ps;
    }

    private static Material FireMaterial()
    {
        if (material != null) return material;
        Shader shader = Resources.Load<Shader>("Effects/Fire");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        material = new Material(shader) { name = "Distraction Fire" };
        return material;
    }
}
