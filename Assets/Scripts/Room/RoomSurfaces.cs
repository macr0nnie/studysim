using UnityEngine;

// Materials the Paint drawer offers for the floor and walls. Lives in Resources; filled by
// Study Sim > Build Room Surfaces from the interior pack's floor and wall pieces.
[CreateAssetMenu(menuName = "Study Sim/Room Surfaces", fileName = "RoomSurfaces")]
public class RoomSurfaces : ScriptableObject
{
    public Material[] floors = new Material[0];
    public Material[] walls = new Material[0];
    // 3D floors (raised planks, tiles): model files whose mesh and materials Paint swaps onto the room's Floor.
    public GameObject[] floorShapes = new GameObject[0];
}
