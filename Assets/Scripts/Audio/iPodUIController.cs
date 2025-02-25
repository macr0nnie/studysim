
// File: iPodUIController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class iPodUIController : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private MusicPlayer musicPlayer;
    [SerializeField] private PlayerCurrency playerCurrency;
    
    [Header("UI Elements")]
    [SerializeField] private Transform playlistContainer;
    [SerializeField] private GameObject playlistItemPrefab;
    [SerializeField] private Text currentSongText;
    [SerializeField] private Text currentArtistText;
    [SerializeField] private Image currentCoverImage;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Button playPauseButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Text coinsText;
    
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
    
    private void UpdateCurrencyDisplay()
    {
        coinsText.text = playerCurrency.GetCoins().ToString();
    }
}