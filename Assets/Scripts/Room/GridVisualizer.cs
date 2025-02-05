using UnityEngine;

public class GridVisualizer : MonoBehaviour
{
    [Header("Grid Settings")]
    public float gridSize = 1.0f;
    public int gridWidth = 10;
    public int gridHeight = 10;
    public Color gridColor = Color.green;
    public float gridHeightOffset = 0.0f; // Height offset for the grid

    [Header("Target Settings")]
    public Transform target; // The target position to follow

    private void OnDrawGizmos()
    {
        if (target == null)
        {
            Debug.LogWarning("GridVisualizer: No target set for the grid to follow.");
            return;
        }

        Gizmos.color = gridColor;

        Vector3 targetPosition = target.position;
        targetPosition.x = Mathf.Round(targetPosition.x / gridSize) * gridSize;
        targetPosition.z = Mathf.Round(targetPosition.z / gridSize) * gridSize;
        targetPosition.y = gridHeightOffset; // Set the height of the grid

        for (int x = -gridWidth; x <= gridWidth; x++)
        {
            Gizmos.DrawLine(new Vector3(x * gridSize, gridHeightOffset, -gridHeight * gridSize) + targetPosition, new Vector3(x * gridSize, gridHeightOffset, gridHeight * gridSize) + targetPosition);
        }

        for (int z = -gridHeight; z <= gridHeight; z++)
        {
            Gizmos.DrawLine(new Vector3(-gridWidth * gridSize, gridHeightOffset, z * gridSize) + targetPosition, new Vector3(gridWidth * gridSize, gridHeightOffset, z * gridSize) + targetPosition);
        }
    }
}