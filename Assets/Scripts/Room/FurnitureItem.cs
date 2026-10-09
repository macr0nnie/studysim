using UnityEngine;

// What the store shows and sells. Placement rules (floor/wall/shelf) live on the prefab's Furniture component.
public enum StoreCategory
{
    Furniture,
    Decor,
    Lighting,
    Plants,
    Ceiling // hanging lights and plants
}

// One entry in the furniture store. Create via Assets > Study Sim > Add To Furniture Store
// (select a model or prefab first) or Create > Study Sim > Furniture Item.
[CreateAssetMenu(menuName = "Study Sim/Furniture Item", fileName = "FurnitureItem")]
public class FurnitureItem : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public StoreCategory category = StoreCategory.Furniture;
    public string[] tags = new string[0];
    public int price = 10;
    public GameObject prefab;
    [Tooltip("Optional. Left empty, the store renders an icon from the prefab.")]
    public Sprite icon;
    [Tooltip("Untick for pieces whose look comes from their texture (paintings, posters) so the paint tools leave them alone.")]
    public bool colorable = true;
}
