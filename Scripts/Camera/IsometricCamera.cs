using UnityEngine;

public class IsometricCamera : MonoBehaviour
{
    [SerializeField] private float targetAspectRatio = 16f / 9f;
    [SerializeField] private float cameraSize = 5f;
    [SerializeField] private Vector3 isometricRotation = new Vector3(35.264f, 45f, 0f);

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (!mainCamera) mainCamera = Camera.main;

        // Set isometric rotation
        transform.rotation = Quaternion.Euler(isometricRotation);
    }

    private void Start()
    {
        AdjustCameraForScreen();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) AdjustCameraForScreen();
    }

    private void AdjustCameraForScreen()
    {
        if (!mainCamera) return;

        float currentAspectRatio = (float)Screen.width / Screen.height;
        float sizeMultiplier = targetAspectRatio / currentAspectRatio;

        // Adjust for different aspect ratios while maintaining view
        if (sizeMultiplier < 1f)
        {
            mainCamera.orthographicSize = cameraSize;
        }
        else
        {
            mainCamera.orthographicSize = cameraSize * sizeMultiplier;
        }
    }
}