using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Collects floor and wall materials for the Paint drawer into Resources/RoomSurfaces.asset.
public static class RoomSurfacesTools
{
    const string AssetPath = "Assets/Resources/RoomSurfaces.asset";
    const string Pack = "Assets/Fries and Seagull/Ultimate Interior Pack/Prefabs";
    static readonly string[] FloorSources = { Pack + "/Door, Floor, Carpet/Floor", "Assets/ART/Mats/Wood.mat", "Assets/ART/Mats/Floor.mat" };
    static readonly string[] WallSources = { Pack + "/Wall", "Assets/ART/Mats/Wall.mat" };

    public static void BuildIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<RoomSurfaces>(AssetPath) == null) Build();
    }

    [MenuItem("Study Sim/Build Room Surfaces")]
    public static void Build()
    {
        var surfaces = AssetDatabase.LoadAssetAtPath<RoomSurfaces>(AssetPath);
        if (surfaces == null)
        {
            surfaces = ScriptableObject.CreateInstance<RoomSurfaces>();
            AssetDatabase.CreateAsset(surfaces, AssetPath);
        }
        surfaces.floors = Collect(FloorSources);
        surfaces.walls = Collect(WallSources);
        EditorUtility.SetDirty(surfaces);
        AssetDatabase.SaveAssets();
        Debug.Log($"Room surfaces: {surfaces.floors.Length} floor and {surfaces.walls.Length} wall materials.");
    }

    // Materials from .mat files and from every prefab in the given folders, URP shaders only
    // (the pack's leftover built-in materials would render pink).
    static Material[] Collect(string[] sources)
    {
        var found = new List<Material>();
        foreach (string source in sources)
        {
            if (source.EndsWith(".mat")) { found.Add(AssetDatabase.LoadAssetAtPath<Material>(source)); continue; }
            if (!AssetDatabase.IsValidFolder(source)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { source }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true)) found.AddRange(r.sharedMaterials);
            }
        }
        return found.Where(m => m != null && m.shader != null && m.shader.name.StartsWith("Universal Render Pipeline/"))
            .Distinct().ToArray();
    }
}
