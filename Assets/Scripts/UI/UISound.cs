using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Short UI sounds, made in code so they need no audio files (and no credits). Every button, toggle and
// slider in the game clicks when pressed; greyed-out ones give a soft buzz. Other scripts call Play for
// placing furniture, coins, lamps and warnings. Volume follows Settings > Sound > Effects.
public class UISound : MonoBehaviour
{
    public enum Cue { Click, Toggle, Denied, Place, Coin, Light, Warning, Recovered }

    private static UISound instance;
    private AudioSource source;
    private AudioClip[] clips;
    private readonly List<RaycastResult> hits = new List<RaycastResult>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("UISound");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UISound>();
    }

    public static void Play(Cue cue)
    {
        if (instance == null || !GameSettings.UISounds) return;
        instance.source.PlayOneShot(instance.clips[(int)cue], GameSettings.EffectsVolume);
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        clips = new AudioClip[Enum.GetValues(typeof(Cue)).Length];
        clips[(int)Cue.Click] = Synth("Click", 0.05f, t => Sweep(t, 1400, 900, 0.05f) * Env(t, 70) * 0.22f);
        clips[(int)Cue.Toggle] = Synth("Toggle", 0.08f, t => Sweep(t, 700, 1100, 0.08f) * Env(t, 45) * 0.2f);
        clips[(int)Cue.Denied] = Synth("Denied", 0.14f, t => Mathf.Sign(Sweep(t, 160, 140, 0.14f)) * Env(t, 25) * 0.06f);
        clips[(int)Cue.Place] = Synth("Place", 0.16f, t => Sweep(t, 240, 110, 0.16f) * Env(t, 28) * 0.45f);
        clips[(int)Cue.Coin] = Synth("Coin", 0.3f, t => Mathf.Sin(2 * Mathf.PI * (t < 0.07f ? 1318.5f : 1760f) * t) * Env(t < 0.07f ? t : t - 0.07f, 14) * 0.16f);
        clips[(int)Cue.Light] = Synth("Light", 0.04f, t => Sweep(t, 2400, 1800, 0.04f) * Env(t, 110) * 0.2f);
        clips[(int)Cue.Warning] = Synth("Warning", 0.55f, t => Sweep(t, 520, 260, 0.55f) * Env(t, 4) * 0.18f);
        clips[(int)Cue.Recovered] = Synth("Recovered", 0.45f, t => Sweep(t, 330, 660, 0.45f) * Env(t, 5) * 0.16f);
    }

    private void Update()
    {
        EventSystem events = EventSystem.current;
        if (!Controls.ClickDown || events == null) return;
        hits.Clear();
        events.RaycastAll(new PointerEventData(events) { position = Controls.PointerPosition }, hits);
        if (hits.Count == 0) return;
        Selectable control = hits[0].gameObject.GetComponentInParent<Selectable>();
        if (control == null) return;
        Play(!control.IsInteractable() ? Cue.Denied : control is Toggle || control is Slider ? Cue.Toggle : Cue.Click);
    }

    // Sine sweeping linearly from f0 to f1 Hz over length seconds.
    private static float Sweep(float t, float f0, float f1, float length) =>
        Mathf.Sin(2 * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2 * length)));

    // 3 ms fade-in (no click at the start), then exponential decay.
    private static float Env(float t, float decay) => Mathf.Min(1, t / 0.003f) * Mathf.Exp(-t * decay);

    private static AudioClip Synth(string name, float length, Func<float, float> sample)
    {
        const int rate = 44100;
        var data = new float[(int)(rate * length)];
        for (int i = 0; i < data.Length; i++) data[i] = sample((float)i / rate);
        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
