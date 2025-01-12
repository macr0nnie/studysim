using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;

/// <summary>
/// Manages the audio system including soundscapes and music player functionality
/// Setup: Attach to an empty GameObject in the scene
/// Dependencies: Requires AudioMixer and audio clips
/// </summary>
public class AudioManager : MonoBehaviour
{
    [System.Serializable]
    public class Soundscape
    {
        public string name;
        public AudioClip clip;
        public bool isLocked = true;
        public int price;
    }

    [Header("Audio Settings")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private List<Soundscape> availableSoundscapes;
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambientSource;
    
    private int currentTrackIndex = -1;
    private List<Soundscape> unlockedSoundscapes = new List<Soundscape>();

    private void Start()
    {
        InitializeAudio();
    }

    private void InitializeAudio()
    {
        foreach (var soundscape in availableSoundscapes)
        {
            if (!soundscape.isLocked)
            {
                unlockedSoundscapes.Add(soundscape);
            }
        }
        
        if (unlockedSoundscapes.Count > 0)
        {
            PlayTrack(0);
        }
    }

    public void PlayTrack(int index)
    {
        if (index < 0 || index >= unlockedSoundscapes.Count) return;
        
        currentTrackIndex = index;
        musicSource.clip = unlockedSoundscapes[index].clip;
        musicSource.Play();
    }

    public void NextTrack()
    {
        int nextIndex = (currentTrackIndex + 1) % unlockedSoundscapes.Count;
        PlayTrack(nextIndex);
    }

    public void PreviousTrack()
    {
        int prevIndex = currentTrackIndex - 1;
        if (prevIndex < 0) prevIndex = unlockedSoundscapes.Count - 1;
        PlayTrack(prevIndex);
    }

    public void SetMusicVolume(float volume)
    {
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
    }

    public void SetAmbientVolume(float volume)
    {
        audioMixer.SetFloat("AmbientVolume", Mathf.Log10(volume) * 20);
    }

    public bool UnlockSoundscape(string soundscapeName)
    {
        Soundscape soundscape = availableSoundscapes.Find(s => s.name == soundscapeName);
        if (soundscape != null && soundscape.isLocked)
        {
            soundscape.isLocked = false;
            unlockedSoundscapes.Add(soundscape);
            return true;
        }
        return false;
    }

    public List<Soundscape> GetAvailableSoundscapes()
    {
        return availableSoundscapes;
    }

    public List<Soundscape> GetUnlockedSoundscapes()
    {
        return unlockedSoundscapes;
    }

    public void PauseMusic()
    {
        musicSource.Pause();
    }

    public void ResumeMusic()
    {
        musicSource.UnPause();
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }
}