using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
/// <summary>
/// Manages all UI elements and interactions
/// Setup: Attach to a UI Canvas in the scene
/// Dependencies: Requires TextMeshPro and Unity UI components
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Timer UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Button startTimerButton;
    [SerializeField] private Button pauseTimerButton;
    [SerializeField] private Button resetTimerButton;
    
    [Header("Decoration UI")]
    [SerializeField] private GameObject decorationPanel;
    [SerializeField] private Transform furnitureButtonContainer;
   
    [SerializeField] private Button editModeButton;
    
    [Header("Audio UI")]
    [SerializeField] private GameObject audioPanel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Button nextTrackButton;
    [SerializeField] private Button previousTrackButton;
    [SerializeField] private Button openAudioStore;
    [SerializeField] private Button ipodHomeButton;
    
    //player currency UI 
    [Header("Player Currency UI")]
 // Reference to the PlayerCurrency script
    [SerializeField] private PlayerCurrency playerCurrency; // Reference to the PlayerCurrency script
    [SerializeField] private TMP_Text playerCoinsText;
    [SerializeField] private TMP_Text playerExperienceText;

    
    [Header ("Color Picker UI")]
    [SerializeField] private GameObject colorPickerPanel;
    [SerializeField] private Button closeColorPickerButton;
    private TimerManager timerManager;
    private RoomManager roomManager;
    private AudioManager audioManager;

    private void Start()
    {
        InitializeManagers();
        SetupUIListeners();
    }

    private void InitializeManagers()
    {
        timerManager = FindObjectOfType<TimerManager>();
        roomManager = FindObjectOfType<RoomManager>();
        audioManager = FindObjectOfType<AudioManager>();

        if (timerManager == null || roomManager == null || audioManager == null)
        {
            Debug.LogError("Required managers not found in scene!");
        }
    }
    private void SetupUIListeners()
    {
        // Timer UI
        if (startTimerButton) startTimerButton.onClick.AddListener(timerManager.StartTimer);
        if (pauseTimerButton) pauseTimerButton.onClick.AddListener(timerManager.PauseTimer);
        if (resetTimerButton) resetTimerButton.onClick.AddListener(timerManager.ResetTimer);
        if (timerManager.plusButton) timerManager.plusButton.onClick.AddListener(timerManager.AddFiveMinutes);
        if (timerManager.minusButton) timerManager.minusButton.onClick.AddListener(timerManager.RemoveFiveMinutes);
        
        // Audio UI
        if (musicVolumeSlider) 
        {
            musicVolumeSlider.onValueChanged.AddListener(audioManager.SetMusicVolume);
        }
        if (nextTrackButton) nextTrackButton.onClick.AddListener(audioManager.NextTrack);
        if (previousTrackButton) previousTrackButton.onClick.AddListener(audioManager.PreviousTrack);
        
        
         if (closeColorPickerButton)
        {
            closeColorPickerButton.onClick.AddListener(() => TogglePanel(colorPickerPanel));
        }
        //subscribe to the player currency changed event
        if (playerCurrency != null)
        {
            playerCurrency.OnCoinsChanged.AddListener(UpdateCurrencyUI);
        }

        // Subscribe to timer events
        timerManager.OnTimerTick += UpdateTimerDisplay;
        timerManager.OnTimerComplete += OnTimerComplete;

             //subscribe to the player currency changed event
        if (playerCurrency != null)
        {
            playerCurrency.OnCoinsChanged.AddListener(UpdateCurrencyUI);
        }

    }

    private void UpdateTimerDisplay(float timeRemaining)
    {
        if (timerText == null) return;
        
        TimeSpan timeSpan = TimeSpan.FromSeconds(timeRemaining);
        int totalMinutes = (int)timeSpan.TotalMinutes;
        timerText.text = $"{totalMinutes:D2}:{timeSpan.Seconds:D2}";
    }

    private void OnTimerComplete()
    {
        
    }
    public void TogglePanel(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf);
    }
    //i need a method that can close the current panel and open the new one
    public void OpenPanel(GameObject panelToOpen, GameObject panelToClose)
    {
        if (panelToOpen == null || panelToClose == null) return;
        panelToClose.SetActive(false);
        panelToOpen.SetActive(true);
    }
    public void StartFurniturePlacement(GameObject furniturePrefab)
    {
        Debug.Log("Starting furniture placement...");
        roomManager.StartPlacingFurniture(furniturePrefab);
    }
    private void OnDestroy()
    {
        if (timerManager != null)
        {
            timerManager.OnTimerTick -= UpdateTimerDisplay;
            timerManager.OnTimerComplete -= OnTimerComplete;
        }
    }
    //on the onpplayer currency changed there is a player currency changed function 
    ///when that function is invoced the Currency UI should be updated.
    public void UpdateCurrencyUI(int coins)
    {
        //when the player earns coins or spends coins, update the UI
        if (playerCoinsText != null)
        {
            playerCoinsText.text = coins.ToString();
        }
    }
}