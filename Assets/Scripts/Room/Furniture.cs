using UnityEngine;

public class Furniture : MonoBehaviour
{
    public enum FurnitureType
    {
        Floor,
        Wall,
        Shelf,
        Ceiling // hangs from the ceiling: lights, plants
    }
    public FurnitureType Type;
}


