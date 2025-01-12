using UnityEngine;
using System;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public event Action<int> OnCurrencyChanged;
    
    [SerializeField] private int currencyPerMinuteFocused = 10;
    private int currentCurrency;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        LoadCurrency();
    }

    public void AddCurrencyForFocusTime(float focusTimeInSeconds)
    {
        int earnedCurrency = Mathf.RoundToInt((focusTimeInSeconds / 60f) * currencyPerMinuteFocused);
        AddCurrency(earnedCurrency);
    }

    public void AddCurrency(int amount)
    {
        currentCurrency += amount;
        OnCurrencyChanged?.Invoke(currentCurrency);
        SaveCurrency();
    }

    public bool TrySpendCurrency(int amount)
    {
        if (currentCurrency >= amount)
        {
            currentCurrency -= amount;
            OnCurrencyChanged?.Invoke(currentCurrency);
            SaveCurrency();
            return true;
        }
        return false;
    }

    public int GetCurrentCurrency()
    {
        return currentCurrency;
    }

    private void SaveCurrency()
    {
        PlayerPrefs.SetInt("Currency", currentCurrency);
        PlayerPrefs.Save();
    }

    private void LoadCurrency()
    {
        currentCurrency = PlayerPrefs.GetInt("Currency", 0);
        OnCurrencyChanged?.Invoke(currentCurrency);
    }
}