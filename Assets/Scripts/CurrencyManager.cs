using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    // The current amount of money the player has
    private float currentMoney = 0f;

    // How much money is awarded per completed study session
    [SerializeField] private float rewardAmount = 10f;

    // Singleton instance
    public static CurrencyManager Instance { get; private set; }

    private void Awake()
    {
        // Ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Add money to the player's balance
    public void AddMoney(float amount)
    {
        currentMoney += amount;
        SaveMoney();
        GameEvents.OnMoneyChanged.Invoke(currentMoney);
    }

    // Remove money from the player's balance
    public bool SpendMoney(float amount)
    {
        if (currentMoney >= amount)
        {
            currentMoney -= amount;
            SaveMoney();
            GameEvents.OnMoneyChanged.Invoke(currentMoney);
            return true;
        }
        return false;
    }

    // Award money for completing a study session
    public void AwardSessionCompletion()
    {
        AddMoney(rewardAmount);
    }

    // Get the current balance
    public float GetCurrentMoney()
    {
        return currentMoney;
    }

    // Save money to PlayerPrefs
    private void SaveMoney()
    {
        PlayerPrefs.SetFloat("PlayerMoney", currentMoney);
        PlayerPrefs.Save();
    }

    // Load money from PlayerPrefs
    private void Start()
    {
        currentMoney = PlayerPrefs.GetFloat("PlayerMoney", 0f);
    }
}