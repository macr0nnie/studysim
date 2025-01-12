using UnityEngine;
using System.Collections.Generic;

public class DecorationSystem : MonoBehaviour
{
    public static DecorationSystem Instance { get; private set; }

    [SerializeField] private LayerMask placementSurfaces;
    [SerializeField] private float gridSize = 0.5f;
    [SerializeField] private float rotationStep = 90f;

    private GameObject currentPlacementObject;
    private List<GameObject> placedObjects = new List<GameObject>();
    private bool isPlacementMode;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void StartPlacement(GameObject decorationPrefab)
    {
        if (Timer.Instance.isRunning) return; // Prevent decoration during focus time

        isPlacementMode = true;
        currentPlacementObject = Instantiate(decorationPrefab);
        currentPlacementObject.GetComponent<Collider>().enabled = false;
    }

    private void Update()
    {
        if (!isPlacementMode || !currentPlacementObject) return;

        UpdatePlacementPosition();
        HandlePlacementInput();
    }

    private void UpdatePlacementPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, placementSurfaces))
        {
            Vector3 position = hit.point;
            position = SnapToGrid(position);
            currentPlacementObject.transform.position = position;
        }
    }

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            Mathf.Round(position.y / gridSize) * gridSize,
            Mathf.Round(position.z / gridSize) * gridSize
        );
    }

    private void HandlePlacementInput()
    {
        if (Input.GetMouseButtonDown(0)) // Left click to place
        {
            ConfirmPlacement();
        }
        else if (Input.GetMouseButtonDown(1)) // Right click to cancel
        {
            CancelPlacement();
        }
        else if (Input.GetKeyDown(KeyCode.R)) // R to rotate
        {
            RotateObject();
        }
    }

    private void RotateObject()
    {
        if (currentPlacementObject)
        {
            currentPlacementObject.transform.Rotate(Vector3.up, rotationStep);
        }
    }

    private void ConfirmPlacement()
    {
        if (currentPlacementObject)
        {
            currentPlacementObject.GetComponent<Collider>().enabled = true;
            placedObjects.Add(currentPlacementObject);
            currentPlacementObject = null;
            isPlacementMode = false;
        }
    }

    private void CancelPlacement()
    {
        if (currentPlacementObject)
        {
            Destroy(currentPlacementObject);
            currentPlacementObject = null;
            isPlacementMode = false;
        }
    }

    public void RemoveDecoration(GameObject decoration)
    {
        if (placedObjects.Contains(decoration))
        {
            placedObjects.Remove(decoration);
            Destroy(decoration);
        }
    }
}