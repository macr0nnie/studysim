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

    // Coins burst up off the character whenever some are earned.
    private void CoinBurst(int amount)
    {
        if (coinPop == null) coinPop = IconPop.Make("CoinPop", 1.2f, UIIcons.Coin);
        if (FindAnyObjectByType<StudyCharacter>() is StudyCharacter character && IconPop.BoundsOf(character.gameObject) is Bounds b)
            IconPop.Burst(coinPop, b, Mathf.Clamp(amount / 10, 5, 16), 1f, UIIcons.Coin == UIIcons.Named("Icons_21") ? new Color(1f, 0.82f, 0.3f) : Color.white); // the sheet coin is white; tint it gold
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
