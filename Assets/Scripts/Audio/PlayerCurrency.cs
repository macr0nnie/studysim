using UnityEngine;
using UnityEngine.Events;

public class PlayerCurrency : MonoBehaviour
{
    [SerializeField] private int startingCoins = 300;
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
        CoinBurst(amount);
    }

    private ParticleSystem coinPop;

    // Hearts burst up off the character whenever coins are earned.
    private void CoinBurst(int amount)
    {
        if (coinPop == null) coinPop = IconPop.Make("CoinPop", 1.2f, IconPop.Heart, IconPop.Sparkle);
        if (FindAnyObjectByType<StudyCharacter>() is StudyCharacter character && IconPop.BoundsOf(character.gameObject) is Bounds b)
            IconPop.Burst(coinPop, b, Mathf.Clamp(amount / 10, 5, 16), 1f, IconPop.Sweets);
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
