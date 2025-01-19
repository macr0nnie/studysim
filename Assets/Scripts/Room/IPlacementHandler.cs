using UnityEngine;
public interface IPlacementHandler
{
    void StartPlacingFurniture(GameObject furniturePrefab);
    void UpdatePreviewPosition();
    void HandlePlacement();
    void CancelPlacement();
    bool IsValidPlacement(Vector3 position);
}
