using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private PlaylistLibrary playlistLibrary;
    
    public UnityEvent<Song> OnSongChanged; 
    public UnityEvent<Playlist> OnPlaylistChanged;
    public UnityEvent<float> OnPlaybackProgressChanged;
    
    private Playlist currentPlaylist;
    private int currentSongIndex;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public Playlist CurrentPlaylist => currentPlaylist;
    public float SongTime => audioSource.clip != null ? audioSource.time : 0;
    public float SongLength => audioSource.clip != null ? audioSource.clip.length : 0;
    
    // Player state that persists between game sessions
    private PlayerData playerData;
    
    private void Awake()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.volume = GameSettings.MusicVolume;
            
        LoadPlayerData();
        ApplyUnlockStatus();
    }
    
    private float nextProgressUpdate;

    private void Update()
    {
        if (isPlaying && audioSource.clip != null)
        {
            // Progress bar only needs a few updates a second; every frame rebuilds the UI canvas for nothing.
            if (Time.unscaledTime >= nextProgressUpdate)
            {
                nextProgressUpdate = Time.unscaledTime + 0.25f;
                OnPlaybackProgressChanged.Invoke(audioSource.time / audioSource.clip.length);
            }
            
            // Auto-advance when the song ends. A finished AudioSource rewinds time to 0, so check that it
            // stopped on its own (we weren't paused) instead of comparing time to the clip length.
            if (!audioSource.isPlaying)
            {
                PlayNextSong();
            }
        }
    }
    
    public List<Playlist> GetAllPlaylists()
    {
        return playlistLibrary.playlists;
    }
    
    public List<Playlist> GetUnlockedPlaylists()
    {
        List<Playlist> unlockedPlaylists = new List<Playlist>();
        foreach (Playlist playlist in playlistLibrary.playlists)
        {
            if (playlist.isUnlocked)
                unlockedPlaylists.Add(playlist);
        }
        return unlockedPlaylists;
    }
    
    public void PlayPlaylist(Playlist playlist)
    {
        if (playlist == null || playlist.songs.Count == 0 || !playlist.isUnlocked)
            return;
            
        currentPlaylist = playlist;
        currentSongIndex = 0;
        PlayCurrentSong();
        
        OnPlaylistChanged.Invoke(currentPlaylist);
    }
    
    public void PlayCurrentSong()
    {
        if (currentPlaylist == null || currentSongIndex < 0 || 
            currentSongIndex >= currentPlaylist.songs.Count)
            return;
            
        Song song = currentPlaylist.songs[currentSongIndex];
        audioSource.clip = song.audioClip;
        audioSource.Play();
        isPlaying = true;
        
        OnSongChanged.Invoke(song);
    }
    
    public void PlayNextSong()
    {
        if (currentPlaylist == null || currentPlaylist.songs.Count == 0)
            return;
            
        currentSongIndex = (currentSongIndex + 1) % currentPlaylist.songs.Count;
        PlayCurrentSong();
    }
    
    public void PlayPreviousSong()
    {
        if (currentPlaylist == null || currentPlaylist.songs.Count == 0)
            return;
            
        // If we're more than 3 seconds into a song, just restart it
        if (audioSource.time > 3f)
        {
            audioSource.time = 0;
            return;
        }
        
        currentSongIndex--;
        if (currentSongIndex < 0)
            currentSongIndex = currentPlaylist.songs.Count - 1;
            
        PlayCurrentSong();
    }
    
    public void PausePlayback()
    {
        if (isPlaying)
        {
            audioSource.Pause();
            isPlaying = false;
        }
    }
    
    public void ResumePlayback()
    {
        if (!isPlaying && audioSource.clip != null)
        {
            audioSource.UnPause();
            isPlaying = true;
        }
    }
    
    public void TogglePlayPause()
    {
        if (isPlaying)
            PausePlayback();
        else
            ResumePlayback();
    }
    
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }
    
    public bool PurchasePlaylist(Playlist playlist, int playerCoins)
    {
        if (playlist == null || playlist.isUnlocked || playerCoins < playlist.price)
            return false;
            
        // Unlock the playlist
        playlist.isUnlocked = true;
        
        // Save unlock status
        SavePlayerData();
        
        return true;
    }
    
    private void LoadPlayerData()
    {
        string savedData = PlayerPrefs.GetString("iPodPlayerData", "");
        if (string.IsNullOrEmpty(savedData))
        {
            playerData = new PlayerData();
            // Set initial playlists as unlocked
            if (playlistLibrary.playlists.Count > 0)
            {
                playerData.unlockedPlaylistTitles.Add(playlistLibrary.playlists[0].title);
            }
        }
        else
        {
            playerData = JsonUtility.FromJson<PlayerData>(savedData);
        }
    }
    
    private void SavePlayerData()
    {
        playerData.unlockedPlaylistTitles.Clear();
        foreach (Playlist playlist in playlistLibrary.playlists)
        {
            if (playlist.isUnlocked)
            {
                playerData.unlockedPlaylistTitles.Add(playlist.title);
            }
        }
        
        string json = JsonUtility.ToJson(playerData);
        PlayerPrefs.SetString("iPodPlayerData", json);
        PlayerPrefs.Save();
    }
    
    private void ApplyUnlockStatus()
    {
        foreach (Playlist playlist in playlistLibrary.playlists)
        {
            playlist.isUnlocked = playerData.unlockedPlaylistTitles.Contains(playlist.title);
        }
    }
    
    [System.Serializable]
    private class PlayerData
    {
        public List<string> unlockedPlaylistTitles = new List<string>();
    }
}