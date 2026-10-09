using System.Collections.Generic;
using UnityEngine;

// Everything the furniture store sells, in display order.
[CreateAssetMenu(menuName = "Study Sim/Furniture Catalog", fileName = "FurnitureCatalog")]
public class FurnitureCatalog : ScriptableObject
{
    public List<FurnitureItem> items = new List<FurnitureItem>();
}
