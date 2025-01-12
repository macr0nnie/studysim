using UnityEngine;

[System.Serializable]
public class SoundscapeItem : IStoreItem
{
    [SerializeField] private string itemName;
    [SerializeField] private float price;
    [SerializeField] private AudioClip audio;
    [SerializeField] private bool isOwned;

    public string Name => itemName;
    public float Price => price;
    public bool IsOwned
    {
        get => isOwned;
        set => isOwned = value;
    }

    private IAudioManager audioManager;

    public SoundscapeItem(IAudioManager audioManager)
    {
        this.audioManager = audioManager;
    }

    public void Apply()
    {
        if (IsOwned)
        {
            audioManager.PlaySound(audio, true);
        }
    }
}