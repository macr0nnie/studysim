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
            Debug.LogError("Camera component not found on " + gameObject.name);
            enabled = false;
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
#if UNITY_2022_1_OR_NEWER
        Application.onBeforeRender += UpdateCameraSettings;
#endif
        // Handle screen resolution changes
        //Screen.onResolutionChanged += OnResolutionChanged;
    }

    private void OnDisable()
    {
#if UNITY_2022_1_OR_NEWER
        Application.onBeforeRender -= UpdateCameraSettings;
#endif
        //Screen.onResolutionChanged -= OnResolutionChanged;
    }

    private void OnResolutionChanged(int width, int height)
    {
        if (this.enabled)
        {
            UpdateCameraSettings();
        }
    }

    private void UpdateCameraSettings()
    {
        if (cam == null) return;

        try
        {
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

            // Apply the new orthographic size smoothly
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, orthographicSize, Time.deltaTime * 5f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating camera settings: {e.Message}");
        }
    }

    public void SetZoom(float zoomLevel)
    {
        if (!enabled || !cam) return;

        targetOrthoSize = Mathf.Clamp(zoomLevel, minOrthoSize, maxOrthoSize);
        UpdateCameraSettings();
    }

    public void ResetZoom()
    {
        if (!enabled || !cam) return;

        targetOrthoSize = initialOrthoSize;
        UpdateCameraSettings();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Validate and update settings in editor
        minOrthoSize = Mathf.Max(0.1f, minOrthoSize);
        maxOrthoSize = Mathf.Max(minOrthoSize, maxOrthoSize);
        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);

        // Update camera if it exists
        if (cam != null)
        {
            UpdateCameraSettings();
        }
        if (Application.isPlaying)
        {
            UpdateCameraSettings();
        }
    }
#endif
}