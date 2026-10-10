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
    [SerializeField] private int defaultFurniturePrice = 10; // used by store buttons that call StartFurniturePlacement directly
    
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
    [SerializeField] private Experience playerExperience; //reference the player experience script
    [SerializeField] private TMP_Text playerExperienceText; //reference the player experience script 
    [SerializeField] private Slider experienceSlider; //reference the player experience script


    
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
        if (playerCurrency != null) UpdateCurrencyUI(playerCurrency.GetCoins());
    }
    // No panel hotkeys any more: M and Esc toggled ALL_THE_MUSIC_STUFF (stopping the music) and I the
    // old store; their replacements (music card, Shop B, Tab for edit mode, Esc for settings) handle their own keys.

    private void InitializeManagers()
    {
        timerManager = FindAnyObjectByType<TimerManager>();
        roomManager = FindAnyObjectByType<RoomManager>();
        audioManager = FindAnyObjectByType<AudioManager>();

        if (timerManager == null || roomManager == null || audioManager == null)
        {
            Debug.LogError("Required managers not found in scene!");
        }
    }
    private void SetupUIListeners()
    {
        // Timer UI (TimerManager wires its own +/- buttons)
        if (timerManager != null)
        {
            if (startTimerButton) startTimerButton.onClick.AddListener(timerManager.StartTimer);
            if (pauseTimerButton) pauseTimerButton.onClick.AddListener(timerManager.PauseTimer);
            if (resetTimerButton) resetTimerButton.onClick.AddListener(timerManager.ResetTimer);
            timerManager.OnTimerTick += UpdateTimerDisplay;
            timerManager.OnTimerComplete += OnTimerComplete;
            UpdateTimerDisplay(timerManager.CurrentTime);
        }

        // Audio UI
        if (audioManager != null)
        {
            if (musicVolumeSlider) musicVolumeSlider.onValueChanged.AddListener(audioManager.SetMusicVolume);
            if (nextTrackButton) nextTrackButton.onClick.AddListener(audioManager.NextTrack);
            if (previousTrackButton) previousTrackButton.onClick.AddListener(audioManager.PreviousTrack);
        }

        if (closeColorPickerButton)
        {
            closeColorPickerButton.onClick.AddListener(() => TogglePanel(colorPickerPanel));
        }
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
    // Only one main panel is open at a time so menus never stack on top of each other.
    public void TogglePanel(GameObject panel)
    {
        if (panel == null) return;
        bool open = !panel.activeSelf;
        if (open) CloseAllPanels();
        panel.SetActive(open);
    }

    public void CloseAllPanels()
    {
        if (audioPanel) audioPanel.SetActive(false);
        if (decorationPanel) decorationPanel.SetActive(false);
        if (colorPickerPanel) colorPickerPanel.SetActive(false);
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
        if (roomManager == null || playerCurrency == null) return;
        int price = defaultFurniturePrice;
        if (playerCurrency.GetCoins() < price)
        {
            Debug.Log("Not enough currency to place furniture!");
            return;
        }
        // Charge on placement, not on pick, so cancelling with right-click costs nothing.
        roomManager.StartPlacingFurniture(furniturePrefab, () => playerCurrency.SpendCoins(price));
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
            playerCoinsText.text = "Gems " +  coins.ToString();
        }
    }
    public void UpdateExperienceUI()
    {
        //update the experience progress bar when the player earns experience points
        experienceSlider.value = playerExperience.GetPlayerLevel();
        //player level text is the player level
        playerExperienceText.text = "Level " + playerExperience.GetPlayerLevel().ToString();
    }
}