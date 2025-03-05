using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class iPodUIController : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private MusicPlayer musicPlayer;
    [SerializeField] private PlayerCurrency playerCurrency;
    
    [Header("UI Elements")]
    [SerializeField] private Transform playlistContainer;
    [SerializeField] private GameObject playlistItemPrefab;
    [SerializeField] private TMP_Text currentSongText;
    [SerializeField] private TMP_Text currentArtistText;
    [SerializeField] private Image currentCoverImage;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private TMP_Text coinsText; //get this from the gm instead this is just to test.
    [SerializeField] private GameObject currentPlayingScreen; // Reference to the current playing screen

    //panel opening and closing/ipod controls
    [SerializeField] private GameObject storeScreen; // Reference to the store screen
    [SerializeField] private GameObject homeScreen; // Reference to the home screen
    [SerializeField] private GameObject homeButton; // Reference to the home button
    bool is_home = false;

    private void Start()
    {
        if (musicPlayer == null)
            musicPlayer = FindObjectOfType<MusicPlayer>();
            
        if (playerCurrency == null)
            playerCurrency = FindObjectOfType<PlayerCurrency>();
            
        // Setup event listeners
        musicPlayer.OnSongChanged.AddListener(UpdateSongDisplay);
        musicPlayer.OnPlaybackProgressChanged.AddListener(UpdateProgressBar);
        
        // Setup button listeners
        playPauseButton.onClick.AddListener(musicPlayer.TogglePlayPause);
        nextButton.onClick.AddListener(musicPlayer.PlayNextSong);
        previousButton.onClick.AddListener(musicPlayer.PlayPreviousSong);
        
        // Populate playlists
        LoadPlaylists();
        
        // Update currency display
        UpdateCurrencyDisplay();
    }

    private void LoadPlaylists()
    {
        // Clear existing items
        foreach (Transform child in playlistContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Add all playlists
        List<Playlist> allPlaylists = musicPlayer.GetAllPlaylists();
        foreach (Playlist playlist in allPlaylists)
        {
            GameObject itemObj = Instantiate(playlistItemPrefab, playlistContainer);
            PlaylistItemUI itemUI = itemObj.GetComponent<PlaylistItemUI>();
            
            if (itemUI != null)
            {
                itemUI.SetPlaylist(playlist);
                itemUI.OnPlayClicked += HandlePlayPlaylist;
                itemUI.OnBuyClicked += HandleBuyPlaylist;
            }
        }
    }
    
    private void HandlePlayPlaylist(Playlist playlist)
    {
        musicPlayer.PlayPlaylist(playlist);
    }
    
    private void HandleBuyPlaylist(Playlist playlist)
    {

        //refactor notes make sure to change it do the player currency comes from the game manager 
        //or this one for the furniture system 
        if (musicPlayer.PurchasePlaylist(playlist, playerCurrency.GetCoins()))
        {
            // Deduct coins
            playerCurrency.SpendCoins(playlist.price);
            // Update UI
            UpdateCurrencyDisplay();
            LoadPlaylists(); // Refresh to update locked status
        }
        else
        {
            Debug.Log("Not enough coins to purchase this playlist!");
            // You could show a UI message here
        }
    }
    
    private void UpdateSongDisplay(Song song)
    {
        if (song != null)
        {
            currentSongText.text = song.title;
            currentArtistText.text = song.artist;
            
            if (song.coverArt != null)
                currentCoverImage.sprite = song.coverArt;
        }
    }
    
    private void UpdateProgressBar(float progress)
    {
        progressSlider.value = progress;
    }

    //home button
     public void OnHomeButtonClicked()
    {
        // Logic to return to the main menu or previous screen
        Debug.Log("Home button clicked");
        
        // Check which screen is currently active and switch to the other screen
        if (is_home)
        {
            // Go to the store screen
            homeScreen.SetActive(false);
            storeScreen.SetActive(true);
        }
        else
        {
            // Go to the home screen
            storeScreen.SetActive(false);
            homeScreen.SetActive(true);
        }
        
        // Toggle the flag
        is_home = !is_home;
    }
    private void UpdateCurrencyDisplay()
    {
        coinsText.text = playerCurrency.GetCoins().ToString();
    }
}