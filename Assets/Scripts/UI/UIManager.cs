using UnityEngine;
using TMPro;

/// <summary>
/// Manages the game's user interface and UI-related events
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private GameObject storePanel;
    [SerializeField] private GameObject roomCustomizationPanel;
    
    private CurrencyManager currencyManager;

    private void Start()
    {
        currencyManager = CurrencyManager.Instance;
        SetupEventListeners();
        UpdateMoneyDisplay();
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
    }

    /// <summary>
    /// Sets up listeners for game events
    /// </summary>
    private void SetupEventListeners()
    {
        GameEvents.OnMoneyChanged.AddListener(HandleMoneyChanged);
        GameEvents.OnSessionCompleted.AddListener(HandleSessionCompleted);
    }

    /// <summary>
    /// Removes event listeners when the object is destroyed
    /// </summary>
    private void CleanupEventListeners()
    {
        GameEvents.OnMoneyChanged.RemoveListener(HandleMoneyChanged);
        GameEvents.OnSessionCompleted.RemoveListener(HandleSessionCompleted);
    }

    /// <summary>
    /// Updates the money display text
    /// </summary>
    private void UpdateMoneyDisplay()
    {
        if (moneyText != null && currencyManager != null)
        {
            moneyText.text = $"${currencyManager.GetCurrentMoney():F2}";
        }
    }

    /// <summary>
    /// Handles changes in the player's money amount
    /// </summary>
    private void HandleMoneyChanged(float newAmount)
    {
        UpdateMoneyDisplay();
    }

    /// <summary>
    /// Handles completion of a study session
    /// </summary>
    private void HandleSessionCompleted()
    {
        // Could show a completion celebration or reward notification
    }

    /// <summary>
    /// Toggles the store panel visibility
    /// </summary>
    public void ToggleStore()
    {
        if (storePanel != null)
        {
            storePanel.SetActive(!storePanel.activeSelf);
        }
    }

    /// <summary>
    /// Toggles the room customization panel visibility
    /// </summary>
    public void ToggleRoomCustomization()
    {
        if (roomCustomizationPanel != null)
        {
            roomCustomizationPanel.SetActive(!roomCustomizationPanel.activeSelf);
        }
    }
}