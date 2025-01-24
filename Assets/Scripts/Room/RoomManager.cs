using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
 

    //settings
    [SerializeField] private LayerMask placementLayer;
    [SerializeField] private LayerMask furnitureLayer;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask shelfLayer;
    [SerializeField] private float snapThreshold = 0.5f;
    [SerializeField] private float gridSize = 1.0f;

    //materials and effects
    [SerializeField] private Material validPlacementMaterial;
    [SerializeField] private Material invalidPlacementMaterial;

    [SerializeField] private GameObject placementParticlePrefab; //need to change

    private List<GameObject> placedObjects = new List<GameObject>(); //keeps track of objects in the scene
    private GameObject currentPreview; 
    private GameObject selectedObject; 
    private bool isPlacementValid;
    private Camera mainCamera;
    private bool isEditMode = false; //fix this


    //keep track of the object's positions so we can undo and redo 
    //edit mode things not working
    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();

    private float lastClickTime; //check for double click (part of edit mode)
    private const float doubleClickThreshold = 0.3f; //how soon in between clicks

    private void Start()
    {

        //set upt the script check if things to intizalized
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main camera not found!");
            enabled = false;
            return;
        }

        if (validPlacementMaterial == null || invalidPlacementMaterial == null)
        {
            Debug.LogError("Placement materials not assigned!");
            enabled = false;
            return;
        }

        if (placementParticlePrefab == null)
        {
            Debug.LogError("Placement particle prefab not assigned!");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (currentPreview != null)
        {
            UpdatePreviewPosition();
            HandlePlacement();
            HandleRotationAndFlipping();
        }
        else if (isEditMode)
        {
            HandleEditMode(); //needs to be fixed 
        }
        else
        {
            HandleObjectSelection();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            Undo(); //this sort of works
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            Redo(); //not really working the way i want it to
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            EnterEditMode(); //not working
            //(need to debug edit mode here)
        }
    }

    //gets a reference of the furniture to show near the mouse. 
    public void StartPlacingFurniture(GameObject furniturePrefab)
    {
        if (furniturePrefab == null)
        {
            Debug.LogError("Furniture prefab is null!");
            return;
        }

        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }

        currentPreview = Instantiate(furniturePrefab);
        if (currentPreview == null)
        {
            Debug.LogError("Failed to instantiate furniture preview!");
            return;
        }

        SetPreviewMaterial(validPlacementMaterial); //remember to set the orginal material back
    }

    //move the preview position around
    private void UpdatePreviewPosition()
    {
        //future me make sure to change the game objects back to the orginal materials
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        //check if the ray hits the one of the three layers
        //seperate to check each layer individualy
        if (Physics.Raycast(ray, out hit, 100f, placementLayer | wallLayer | shelfLayer))
        {
            Vector3 position = hit.point;
            Quaternion rotation = currentPreview.transform.rotation; //furntiure rotation
            Furniture furniture = currentPreview.GetComponent<Furniture>(); //the furniture objs have a class

            if (furniture != null)
            {
                //check how to place the object based on the furniture type
                switch (furniture.Type)
                {
                    case Furniture.FurnitureType.Wall:
                        //check if the object it currently hovering ovr the layer
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

    private Vector3 SnapToWall(Vector3 position, Vector3 normal)
    {
        //change make the object change rotatation when it is hovering over a wall
        position += normal * currentPreview.GetComponent<Collider>().bounds.extents.z;
        return position;
    }

    private Vector3 SnapToShelf(Vector3 position)
    {
        //make the object stand straight
        position.y += currentPreview.GetComponent<Collider>().bounds.extents.y;
        return position;
    }


    private void HandlePlacement()
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


    //rotatation
    //make smoother rotation use a corotine later
    private void HandleRotationAndFlipping()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (currentPreview != null)
            {
                currentPreview.transform.Rotate(Vector3.up, 90f);
            }
            else if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.up, -90f);
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (currentPreview != null)
            {
                currentPreview.transform.Rotate(Vector3.forward, 180f);
            }
            else if (selectedObject != null)
            {
                selectedObject.transform.Rotate(Vector3.back, 90f);
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

    private void PlaceObject()
    {
        Vector3 position = currentPreview.transform.position;

        GameObject placedObject = Instantiate(currentPreview, position, currentPreview.transform.rotation);
        
        //edit mode functionality
        //make it so the objects are stored in a list
        placedObjects.Add(placedObject);
        undoStack.Push(placedObject);
        
        ResetPreviewMaterial(placedObject);//this is not working
        
        PlayPlacementEffect(position); //effect

        Destroy(currentPreview);
        currentPreview = null;
        //Debug.Log("Object placed successfully.");
    }

    private void CancelPlacement()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
            Debug.Log("Placement canceled.");
        }
    }

    private bool IsValidPlacement(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapBox(position, currentPreview.GetComponent<Collider>().bounds.extents, currentPreview.transform.rotation, furnitureLayer);
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

    //check this later
    private void ResetPreviewMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material = validPlacementMaterial;
        }
    }

    private void HandleEditMode()
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

    private void HandleObjectSelection()
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
                    float timeSinceLastClick = Time.time - lastClickTime;
                    if (timeSinceLastClick <= doubleClickThreshold)
                    {
                        isEditMode = true;
                        selectedObject = hitObject;
                    }
                    lastClickTime = Time.time;
                }
            }
        }
        else if (Input.GetMouseButtonDown(1)) // Right-click to delete
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

    private void EnterEditMode()
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

    private void DeleteObject(GameObject obj)
    {
        if (placedObjects.Contains(obj))
        {
            placedObjects.Remove(obj);
            undoStack.Push(obj);
            obj.SetActive(false);
        }
    }

    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            GameObject lastObject = undoStack.Pop();
            redoStack.Push(lastObject);
            lastObject.SetActive(!lastObject.activeSelf);
        }
    }

    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            GameObject lastObject = redoStack.Pop();
            undoStack.Push(lastObject);
            lastObject.SetActive(!lastObject.activeSelf);
        }
    }

    private void PlayPlacementEffect(Vector3 position)
    {
        if (placementParticlePrefab != null)
        {
            GameObject particleEffect = Instantiate(placementParticlePrefab, position, Quaternion.identity);
            ParticleSystem particleSystem = particleEffect.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
            Destroy(particleEffect, 2f); // Destroy the particle effect after 2 seconds
        }
    }
}