using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;
    [SerializeField] private GameObject storePanel;
    [SerializeField] private GameObject roomCustomizationPanel;
    
    private CurrencyManager currencyManager;

    private void Start()
    {
        currencyManager = CurrencyManager.Instance;
        UpdateMoneyDisplay();
    }

    private void Update()
    {
        UpdateMoneyDisplay();
    }

    private void UpdateMoneyDisplay()
    {
        if (moneyText != null && currencyManager != null)
        {
            moneyText.text = $"${currencyManager.GetCurrentMoney():F2}";
        }
    }

    // Toggle store panel visibility
    public void ToggleStore()
    {
        if (storePanel != null)
        {
            storePanel.SetActive(!storePanel.activeSelf);
        }
    }

    // Toggle room customization panel visibility
    public void ToggleRoomCustomization()
    {
        if (roomCustomizationPanel != null)
        {
            roomCustomizationPanel.SetActive(!roomCustomizationPanel.activeSelf);
        }
    }
}