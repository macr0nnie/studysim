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

// How the character uses a piece (sit on a chair, lie on a bed). The spot is worked out from the piece's real renderer
// bounds, so imported rotations and scales don't matter: the middle of the piece, `height` of the way up it (0 = floor,
// 0.6 = a mattress top), nudged by `offset` (fractions of the half-size on the world X/Z axes), facing turned by `yaw`.
[System.Serializable]
public struct Interaction
{
    public bool enabled;
    [Range(0f, 1f)] public float height;
    public Vector2 offset;
    public float yaw;
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

    [Header("Character")]
    [Tooltip("Where the character sits when studying. The most recently placed piece with this on wins, so a bought chair beats the desk.")]
    public Interaction sit;
    [Tooltip("Where the character lies during breaks.")]
    public Interaction lie;
    [Tooltip("What this piece adds to (or, negative, takes from) the character's hidden happiness.")]
    public int comfort = 1;
}

// Which pieces count as a desk or a chair: store items tagged "desk"/"chair", or anything whose name says so
// (the old scene's furniture). The character and the keep-one-of-each rule both go through here.
public static class FurnitureRole
{
    public static bool Is(GameObject piece, FurnitureItem item, string role)
    {
        // A store item says what it is through its tags ("Desk Lamp" is not a desk); only scene leftovers go by name.
        if (item == null) return piece != null && piece.name.IndexOf(role, System.StringComparison.OrdinalIgnoreCase) >= 0;
        foreach (string tag in item.tags ?? new string[0])
            if (string.Equals(tag, role, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
