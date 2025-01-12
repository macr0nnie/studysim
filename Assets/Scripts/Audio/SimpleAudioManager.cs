using UnityEngine;

public class SimpleAudioManager : MonoBehaviour, IAudioManager
{
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlaySound(AudioClip clip, bool loop = false)
    {
        if (clip != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.loop = loop;
            audioSource.Play();
        }
    }

    public void StopSound()
    {
        audioSource.Stop();
    }

    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }
}