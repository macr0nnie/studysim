using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private PlaylistLibrary playlistLibrary;
    
    public UnityEvent<Song> OnSongChanged; 
    
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
        RefreshUnlocks();
    }

    // Free playlists, and ones whose level the player has reached, unlock themselves. Called when the
    // library opens so a level gained mid-session counts.
    public void RefreshUnlocks()
    {
        int level = PlayerPrefs.GetInt("PlayerLevel", 1);
        bool changed = false;
        foreach (Playlist playlist in playlistLibrary.playlists)
        {
            if (playlist.isUnlocked) continue;
            if (playlist.price <= 0 || (playlist.unlockLevel > 0 && level >= playlist.unlockLevel))
            {
                playlist.isUnlocked = true;
                changed = true;
            }
        }
        if (changed) SavePlayerData();
    }
    
    private ParticleSystem notes;
    private float nextNote;
    private RoomManager room;

    private void Update()
    {
        if (isPlaying && audioSource.isPlaying && Time.unscaledTime >= nextNote)
        {
            nextNote = Time.unscaledTime + 1.6f;
            FloatNote();
        }
        if (isPlaying && audioSource.clip != null)
        {
            // Auto-advance when the song ends. A finished AudioSource rewinds time to 0, so check that it
            // stopped on its own (we weren't paused) instead of comparing time to the clip length.
            if (!audioSource.isPlaying)
            {
                PlayNextSong();
            }
        }
    }
    
    // A music note drifts up off the record player (or anything that plays music), else off the character.
    private void FloatNote()
    {
        if (notes == null) notes = IconPop.Make("MusicNotes", 2.4f, UIIcons.Named("Icons_5"));
        if (notes == null) return;
        if (room == null) room = FindAnyObjectByType<RoomManager>();
        GameObject source = null;
        if (room != null)
            foreach (GameObject piece in room.PlacedPieces)
            {
                string n = piece != null ? piece.name.ToLowerInvariant() : "";
                if (n.Contains("vinyl") || n.Contains("record") || n.Contains("speaker") || n.Contains("radio")) { source = piece; break; }
            }
        if (source == null && FindAnyObjectByType<StudyCharacter>() is StudyCharacter character) source = character.gameObject;
        if (IconPop.BoundsOf(source) is Bounds b) IconPop.Burst(notes, b, 1, 0.35f, new Color(1f, 0.85f, 0.95f));
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
            try { playerData = JsonUtility.FromJson<PlayerData>(savedData); }
            catch (System.ArgumentException) { playerData = null; }
            if (playerData == null || playerData.unlockedPlaylistTitles == null) { PlayerPrefs.DeleteKey("iPodPlayerData"); LoadPlayerData(); } // unreadable: start over with the defaults
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