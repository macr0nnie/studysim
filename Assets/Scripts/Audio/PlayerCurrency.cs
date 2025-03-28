using UnityEngine;
using UnityEngine.Events;

public class PlayerCurrency : MonoBehaviour
{
    [SerializeField] private int startingCoins = 500;
    public UnityEvent<int> OnCoinsChanged;

    private int coins;
    
    private void Awake()
    {
        LoadCoins();
    }
    
    public int GetCoins()
    {
        return coins;
    }
    
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;        
        coins += amount;
        SaveCoins();
        OnCoinsChanged.Invoke(coins);
    }
    
    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return false;
        if (coins < amount) return false;

        coins -= amount;
        SaveCoins();
        OnCoinsChanged.Invoke(coins);
        return true;
    }
    
    private void LoadCoins()
    {
        coins = PlayerPrefs.GetInt("PlayerCoins", startingCoins);
    }
    
    private void SaveCoins()
    {
        PlayerPrefs.SetInt("PlayerCoins", coins);
        PlayerPrefs.Save();
    }
    
}
