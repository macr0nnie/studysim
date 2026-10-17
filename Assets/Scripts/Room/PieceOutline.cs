using UnityEngine;
using UnityEngine.Rendering;

// Edit-mode outline: a slightly larger copy of each mesh drawn inside-out in one flat colour, so only a rim shows
// around the piece while its own materials and paint stay visible (the old highlight tinted the whole piece).
public class PieceOutline : MonoBehaviour
{
    public const string HullName = "Outline";
    private const float Grow = 0.07f;
    private static Shader shader;
    private Material material;
    private float built; // rim size the hulls were made with, 0 = none. Not serialized: an Instantiate copy rebuilds its own

    // null removes the outline. grow: how much bigger the rim hull is (the placement ghost uses a thicker one).
    public static void Show(GameObject piece, Color? color, float grow = Grow)
    {
        if (piece == null) return;
        var outline = piece.GetComponent<PieceOutline>();
        if (color == null) { if (outline != null) outline.RemoveHulls(); return; }
        if (outline == null) outline = piece.AddComponent<PieceOutline>();
        outline.Set(color.Value, grow);
    }

    private void Set(Color color, float grow)
    {
        if (material == null)
        {
            // URP/Unlit is stripped from builds when no material uses it; Particles/Unlit is in Always Included Shaders.
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return;
            material = new Material(shader) { name = HullName };
            material.SetFloat("_Cull", (float)CullMode.Front); // draw the inside, so only the rim past the piece shows
            material.SetFloat("_ZWrite", 0); // the rim never hides the piece or its neighbours' edges
        }
        material.SetColor("_BaseColor", color * 1.8f); // past the bloom threshold: the rim glows a little and stays readable through tone mapping
        if (built == grow) return;
        RemoveHulls();
        built = grow;
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || filter.name == HullName) continue;
            var hull = new GameObject(HullName, typeof(MeshFilter), typeof(MeshRenderer));
            hull.transform.SetParent(filter.transform, false);
            float s = 1f + grow;
            hull.transform.localScale = Vector3.one * s;
            hull.transform.localPosition = filter.sharedMesh.bounds.center * (1f - s); // grow about the mesh's centre, not its pivot
            hull.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var r = hull.GetComponent<MeshRenderer>();
            var slots = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            r.sharedMaterials = slots;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    // Also removes hulls copied along with the piece.
    public void RemoveHulls()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name == HullName && t != transform) Destroy(t.gameObject);
        built = 0f;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
