using UnityEngine;

public class ColorChanger : MonoBehaviour
{
    [SerializeField] private ColorPicker colorPicker;
    [SerializeField] private Renderer targetRenderer; // The renderer of the object to change color

    private void OnEnable()
    {
        if (colorPicker != null)
        {
            colorPicker.OnColorChanged += HandleColorChanged;
        }
    }

    private void OnDisable()
    {
        if (colorPicker != null)
        {
            colorPicker.OnColorChanged -= HandleColorChanged;
        }
    }

    private void HandleColorChanged(Color newColor)
    {
        if (targetRenderer != null)
        {
            targetRenderer.material.color = newColor;
        }
    }
}