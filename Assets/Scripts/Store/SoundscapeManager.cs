using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the soundscape store functionality, including purchasing and playing soundscapes
/// </summary>
public class SoundscapeManager : MonoBehaviour
{
    [SerializeField] private List<SoundscapeItem> availableSoundscapes = new List<SoundscapeItem>();
    private IAudioManager audioManager;
    private ISaveSystem saveSystem;
    
    private void Awake()
    {
        audioManager = GetComponent<SimpleAudioManager>();
        saveSystem = new PlayerPrefsSaveSystem();
        if (audioManager == null)
        {
            audioManager = gameObject.AddComponent<SimpleAudioManager>();
        }
        LoadPurchasedSoundscapes();
    }

    /// <summary>
    /// Attempts to purchase a soundscape at the given index
    /// </summary>
    /// <param name="index">Index of the soundscape to purchase</param>
    /// <returns>True if purchase was successful, false otherwise</returns>
    public bool PurchaseSoundscape(int index)
    {
        if (index >= 0 && index < availableSoundscapes.Count)
        {
            SoundscapeItem soundscape = availableSoundscapes[index];
            if (!soundscape.IsOwned)
            {
                if (CurrencyManager.Instance.SpendMoney(soundscape.Price))
                {
                    soundscape.IsOwned = true;
                    SavePurchasedSoundscapes();
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Plays a purchased soundscape
    /// </summary>
    /// <param name="index">Index of the soundscape to play</param>
    public void PlaySoundscape(int index)
    {
        if (index >= 0 && index < availableSoundscapes.Count)
        {
            availableSoundscapes[index].Apply();
        }
    }

    /// <summary>
    /// Stops the currently playing soundscape
    /// </summary>
    public void StopSoundscape()
    {
        audioManager.StopSound();
    }

    private void SavePurchasedSoundscapes()
    {
        var saveData = new List<bool>();
        foreach (var soundscape in availableSoundscapes)
        {
            saveData.Add(soundscape.IsOwned);
        }
        saveSystem.SaveData("Soundscapes", saveData);
    }

    private void LoadPurchasedSoundscapes()
    {
        var saveData = saveSystem.LoadData("Soundscapes", new List<bool>());
        for (int i = 0; i < Mathf.Min(saveData.Count, availableSoundscapes.Count); i++)
        {
            availableSoundscapes[i].IsOwned = saveData[i];
        }
    }
}