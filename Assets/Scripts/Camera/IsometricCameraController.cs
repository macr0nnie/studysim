using UnityEngine;

/// <summary>
/// Controls the isometric camera view with proper scaling and positioning for different screen sizes
/// Setup: Attach to the main camera in the scene
/// Dependencies: None
/// </summary>
public class IsometricCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float targetOrthoSize = 5f;
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 10f;
    
    [Header("Position Settings")]
    [SerializeField] private Vector3 targetPosition = new Vector3(10f, 10f, -10f);
    [SerializeField] private Vector3 rotationAngles = new Vector3(35f, 45f, 0f);
    
    private Camera cam;
    private float aspectRatio;
    private float initialOrthoSize;
    private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("Camera component not found!");
            return;
        }

        cam.orthographic = true;
        initialOrthoSize = targetOrthoSize;
        
        // Set initial position and rotation
        transform.position = targetPosition;
        transform.rotation = Quaternion.Euler(rotationAngles);
        
        // Initial setup
        UpdateCameraSettings();
    }

    private void OnEnable()
    {
        // Subscribe to screen resolution changes
        Application.onScreenSizeChanged += OnScreenSizeChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe from screen resolution changes
        Application.onScreenSizeChanged -= OnScreenSizeChanged;
    }

    private void OnScreenSizeChanged(int width, int height)
    {
        UpdateCameraSettings();
    }

    private void UpdateCameraSettings()
    {
        if (cam == null) return;

        // Calculate current aspect ratio
        aspectRatio = (float)Screen.width / Screen.height;
        float targetAspect = referenceResolution.x / referenceResolution.y;

        // Adjust orthographic size based on aspect ratio
        float orthographicSize = initialOrthoSize;
        
        if (aspectRatio < targetAspect)
        {
            // Screen is taller than reference, adjust ortho size to maintain width
            orthographicSize = initialOrthoSize * (targetAspect / aspectRatio);
        }

        // Clamp the orthographic size
        orthographicSize = Mathf.Clamp(orthographicSize, minOrthoSize, maxOrthoSize);
        
        // Apply the new orthographic size
        cam.orthographicSize = orthographicSize;
    }

    public void SetZoom(float zoomLevel)
    {
        targetOrthoSize = Mathf.Clamp(zoomLevel, minOrthoSize, maxOrthoSize);
        UpdateCameraSettings();
    }

    public void ResetZoom()
    {
        targetOrthoSize = initialOrthoSize;
        UpdateCameraSettings();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Update camera settings when values are changed in the inspector
        if (Application.isPlaying)
        {
            UpdateCameraSettings();
        }
    }
#endif
}