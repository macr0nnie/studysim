using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages loading and caching of game resources like materials and prefabs
/// </summary>
public class ResourceManager : MonoBehaviour
{
    private static ResourceManager instance;
    public static ResourceManager Instance => instance;

    [SerializeField] private Material[] wallpaperMaterials;
    [SerializeField] private Material[] flooringMaterials;
    [SerializeField] private GameObject[] furniturePrefabs;
    [SerializeField] private GameObject[] decorationPrefabs;

    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            CacheResources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CacheResources()
    {
        // Cache wallpapers
        foreach (var material in wallpaperMaterials)
        {
            if (material != null)
                materialCache[material.name] = material;
        }

        // Cache flooring materials
        foreach (var material in flooringMaterials)
        {
            if (material != null)
                materialCache[material.name] = material;
        }

        // Cache furniture prefabs
        foreach (var prefab in furniturePrefabs)
        {
            if (prefab != null)
                prefabCache[prefab.name] = prefab;
        }

        // Cache decoration prefabs
        foreach (var prefab in decorationPrefabs)
        {
            if (prefab != null)
                prefabCache[prefab.name] = prefab;
        }
    }

    /// <summary>
    /// Gets a material by its name from the cache
    /// </summary>
    /// <param name="materialName">Name of the material to retrieve</param>
    /// <returns>The requested material or null if not found</returns>
    public Material GetMaterial(string materialName)
    {
        return materialCache.TryGetValue(materialName, out Material material) ? material : null;
    }

    /// <summary>
    /// Gets a prefab by its name from the cache
    /// </summary>
    /// <param name="prefabName">Name of the prefab to retrieve</param>
    /// <returns>The requested prefab or null if not found</returns>
    public GameObject GetPrefab(string prefabName)
    {
        return prefabCache.TryGetValue(prefabName, out GameObject prefab) ? prefab : null;
    }
}