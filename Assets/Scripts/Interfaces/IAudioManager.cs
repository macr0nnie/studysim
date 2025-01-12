using UnityEngine;

public interface IAudioManager
{
    void PlaySound(AudioClip clip, bool loop = false);
    void StopSound();
    void SetVolume(float volume);
}