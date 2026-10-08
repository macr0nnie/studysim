using UnityEngine;

// One entry in the furniture store. Create via Assets > Study Sim > Add To Furniture Store
// (select a model or prefab first) or Create > Study Sim > Furniture Item.
[CreateAssetMenu(menuName = "Study Sim/Furniture Item", fileName = "FurnitureItem")]
public class FurnitureItem : ScriptableObject
{
    public string displayName;
    public Sprite icon;
    public GameObject prefab;
    public int price = 10;
    public Furniture.FurnitureType category = Furniture.FurnitureType.Floor;
}
