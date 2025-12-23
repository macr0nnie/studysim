using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;

public class Sprite_Swap : MonoBehaviour
{

    //this is the script for changing the walls and floor of the room.
    [SerializeField] private SpriteRenderer wallRenderer;
    [SerializeField] private SpriteRenderer floorRenderer;

    public void SetWallColor(Color color)
    {
        if (wallRenderer != null)
            wallRenderer.color = color;
    }
    public void SetFloorColor(Color color)
    {
        if (floorRenderer != null)
            floorRenderer.color = color;
    }

    //changet the material of the wall and floor
    public void SetWallMaterial(Material material)
    {
        if (wallRenderer != null)
            wallRenderer.material = material;
    }
    public void SetFloorMaterial(Material material)
    {
        if (floorRenderer != null)
            floorRenderer.material = material;
    }



}
