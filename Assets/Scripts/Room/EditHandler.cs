using UnityEngine;
using System.Collections.Generic;


public class EditHandler : IEditHandler
{
    private GameObject selectedObject;
    private Camera mainCamera;
    private LayerMask placementLayer;
    private LayerMask wallLayer;
    private LayerMask shelfLayer;
    private List<GameObject> placedObjects;
    private bool isEditMode;

    public EditHandler(Camera mainCamera, LayerMask placementLayer, LayerMask wallLayer, LayerMask shelfLayer, List<GameObject> placedObjects)
    {
        this.mainCamera = mainCamera;
        this.placementLayer = placementLayer;
        this.wallLayer = wallLayer;
        this.shelfLayer = shelfLayer;
        this.placedObjects = placedObjects;
    }

    public void EnterEditMode()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
        {
            GameObject hitObject = hit.collider.gameObject;
            if (placedObjects.Contains(hitObject))
            {
                isEditMode = true;
                selectedObject = hitObject;
            }
        }
    }

    public void HandleEditMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
            {
                GameObject hitObject = hit.collider.gameObject;
                if (placedObjects.Contains(hitObject))
                {
                    selectedObject = hitObject;
                }
            }
        }

        if (selectedObject != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
            {
                Vector3 position = hit.point;
                selectedObject.transform.position = position;
            }
        }
    }

    public void HandleObjectSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
            {
                GameObject hitObject = hit.collider.gameObject;
                if (placedObjects.Contains(hitObject))
                {
                        isEditMode = true;
                        selectedObject = hitObject;
        
                }
            }
        }
        else if (Input.GetMouseButtonDown(1)) 
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
            {
                GameObject hitObject = hit.collider.gameObject;
                DeleteObject(hitObject);
            }
        }
    }

    public void HandleRotationAndFlipping()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.up, 90f);
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.forward, 180f);
            }
        }

        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.R))
        {
            if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.right, 90f);
            }
        }
    }

    private void DeleteObject(GameObject obj)
    {
        if (placedObjects.Contains(obj))
        {
            placedObjects.Remove(obj);
            obj.SetActive(false);
        }
    }
}