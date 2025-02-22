using UnityEngine;
using UnityEngine.UI;
using System;

public class ColorPicker : MonoBehaviour
{
    [SerializeField] private Image colorImage; // The image used for color picking
    [SerializeField] private Camera uiCamera; // The camera used for UI rendering

    public event Action<Color> OnColorChanged; // Event called when the color is changed

    private Texture2D colorTexture;

    private void Start()
    {
        if (colorImage != null && colorImage.sprite != null)
        {
            colorTexture = colorImage.sprite.texture;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            SelectColor();
        }
    }

    private void SelectColor()
    {
        if (colorTexture == null || uiCamera == null) return;

        Vector2 localCursor;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            colorImage.rectTransform,
            Input.mousePosition,
            uiCamera,
            out localCursor
        );

        Rect rect = colorImage.rectTransform.rect;
        float x = Mathf.Clamp01((localCursor.x - rect.x) / rect.width);
        float y = Mathf.Clamp01((localCursor.y - rect.y) / rect.height);

        Color selectedColor = colorTexture.GetPixelBilinear(x, y);
        Debug.Log($"Selected Color: {selectedColor}");

        OnColorChanged?.Invoke(selectedColor);
    }
}