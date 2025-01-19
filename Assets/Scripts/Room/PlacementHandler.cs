using UnityEngine;
using System.Collections.Generic;

public class PlacementHandler : IPlacementHandler
{
    private GameObject currentPreview;
    private Material validPlacementMaterial;
    private Material invalidPlacementMaterial;
    private LayerMask placementLayer;
    private LayerMask wallLayer;
    private LayerMask shelfLayer;
    private float snapThreshold;
    private Camera mainCamera;
    private bool isPlacementValid;
    private List<GameObject> placedObjects;

    public PlacementHandler(Material validPlacementMaterial, Material invalidPlacementMaterial, LayerMask placementLayer, LayerMask wallLayer, LayerMask shelfLayer, float snapThreshold, Camera mainCamera, List<GameObject> placedObjects)
    {
        this.validPlacementMaterial = validPlacementMaterial;
        this.invalidPlacementMaterial = invalidPlacementMaterial;
        this.placementLayer = placementLayer;
        this.wallLayer = wallLayer;
        this.shelfLayer = shelfLayer;
        this.snapThreshold = snapThreshold;
        this.mainCamera = mainCamera;
        this.placedObjects = placedObjects;
    }

    public void StartPlacingFurniture(GameObject furniturePrefab)
    {
        if (furniturePrefab == null)
        {
            Debug.LogError("Furniture prefab is null!");
            return;
        }

        if (currentPreview != null)
        {
            GameObject.Destroy(currentPreview);
        }

        currentPreview = GameObject.Instantiate(furniturePrefab);
        if (currentPreview == null)
        {
            Debug.LogError("Failed to instantiate furniture preview!");
            return;
        }

        SetPreviewMaterial(validPlacementMaterial);
    }

    public void UpdatePreviewPosition()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
        {
            Vector3 position = hit.point;
            Quaternion rotation = currentPreview.transform.rotation;
            Furniture furniture = currentPreview.GetComponent<Furniture>();

            if (furniture != null)
            {
                switch (furniture.Type)
                {
                    case Furniture.FurnitureType.Wall:
                        if (((1 << hit.collider.gameObject.layer) & wallLayer) != 0)
                        {
                            position = SnapToWall(position, hit.normal);
                        }
                        else
                        {
                            isPlacementValid = false;
                            SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                    case Furniture.FurnitureType.Shelf:
                        if (((1 << hit.collider.gameObject.layer) & shelfLayer) != 0)
                        {
                            position = SnapToShelf(position);
                        }
                        else
                        {
                            isPlacementValid = false;
                            SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                    case Furniture.FurnitureType.Floor:
                    default:
                        if (((1 << hit.collider.gameObject.layer) & placementLayer) != 0)
                        {
                            position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
                        }
                        else
                        {
                            isPlacementValid = false;
                            SetPreviewMaterial(invalidPlacementMaterial);
                            return;
                        }
                        break;
                }
            }

            currentPreview.transform.position = position;
            currentPreview.transform.rotation = rotation;
            isPlacementValid = IsValidPlacement(position);
            SetPreviewMaterial(isPlacementValid ? validPlacementMaterial : invalidPlacementMaterial);
        }
        else
        {
            Vector3 position = mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
            currentPreview.transform.position = position;
            isPlacementValid = false;
            SetPreviewMaterial(invalidPlacementMaterial);
        }
    }

    public void HandlePlacement()
    {
        if (Input.GetMouseButtonDown(0) && isPlacementValid)
        {
            PlaceObject();
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    public void CancelPlacement()
    {
        if (currentPreview != null)
        {
            GameObject.Destroy(currentPreview);
            currentPreview = null;
            Debug.Log("Placement canceled.");
        }
    }

    public bool IsValidPlacement(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapBox(position, currentPreview.GetComponent<Collider>().bounds.extents, currentPreview.transform.rotation);
        bool isValid = colliders.Length == 0;

        foreach (GameObject placedObject in placedObjects)
        {
            if (placedObject.GetComponent<Collider>().bounds.Intersects(currentPreview.GetComponent<Collider>().bounds))
            {
                isValid = false;
                break;
            }
        }

        Debug.Log("Placement valid: " + isValid);
        return isValid;
    }

    private void SetPreviewMaterial(Material material)
    {
        Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = material;
        }
    }

    private void PlaceObject()
    {
        Vector3 position = currentPreview.transform.position;

        GameObject placedObject = GameObject.Instantiate(currentPreview, position, currentPreview.transform.rotation);
        placedObjects.Add(placedObject);
        ResetPreviewMaterial(placedObject);
        currentPreview = null;
        Debug.Log("Object placed successfully.");
    }

    private void ResetPreviewMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = validPlacementMaterial;
        }
    }

    private Vector3 SnapToWall(Vector3 position, Vector3 normal)
    {
        position += normal * currentPreview.GetComponent<Collider>().bounds.extents.z;
        return position;
    }

    private Vector3 SnapToShelf(Vector3 position)
    {
        position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
        return position;
    }
}