using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Settings drawer: master volume, study and break lengths, and quitting the game.
public class SettingsUI : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";
    private GameObject panel;
    private TimerManager timer;
    private Slider study, rest;
    private TMP_Text timerNote, studyText, restText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureSettings();
        EnsureSettings();
    }

    private static void EnsureSettings()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<SettingsUI>() == null)
            new GameObject("Settings").AddComponent<SettingsUI>();
    }

    private void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        timer = FindFirstObjectByType<TimerManager>();
        BuildUI();
    }

    public void Toggle()
    {
        if (!ToggleDrawer(this, panel) || timer == null) return;
        // Lengths can only change while the timer is stopped.
        study.SetValueWithoutNotify(Mathf.Round(timer.StudyMinutes / 5f));
        rest.SetValueWithoutNotify(Mathf.Round(timer.BreakMinutes));
        study.interactable = rest.interactable = !timer.IsTimerRunning;
        timerNote.gameObject.SetActive(timer.IsTimerRunning);
        studyText.text = $"{study.value * 5:0} min"; // labels only: invoking onValueChanged would reset a paused timer
        restText.text = $"{rest.value:0} min";
    }

    private static void Quit()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("SettingsCanvas", transform, 10).transform;

        Button open = NavButton(canvas, 5, "Settings", null);
        open.onClick.AddListener(Toggle);

        panel = MakeDrawer("SettingsPanel", canvas);
        Header(panel.transform, "Settings", Toggle);

        Setting("Volume", out _, 0, 100, Mathf.Round(AudioListener.volume * 100), v => $"{v:0}%", v =>
        {
            AudioListener.volume = v / 100f;
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        });
        if (timer != null)
        {
            study = Setting("Study session", out studyText, 1, 24, Mathf.Round(timer.StudyMinutes / 5f), v => $"{v * 5:0} min",
                v => { if (!timer.IsTimerRunning) timer.SetCustomDuration(v * 5); });
            rest = Setting("Break", out restText, 1, 30, Mathf.Round(timer.BreakMinutes), v => $"{v:0} min",
                v => { if (!timer.IsTimerRunning) timer.SetBreakDuration(v); });
            timerNote = MakeText("TimerNote", panel.transform, "Stop the timer to change session lengths.", CaptionSize, MutedText, TextAlignmentOptions.Left);
            timerNote.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
        }

        GameObject spacer = Make("Spacer", panel.transform, typeof(LayoutElement));
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1;

        Button quit = TextButton("Quit", panel.transform, "Save and quit", DangerColor, BodySize);
        quit.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        quit.onClick.AddListener(Quit);
    }

    // Label + value on one line, a slider under it.
    private Slider Setting(string title, out TMP_Text valueText, float min, float max, float value, Func<float, string> format, Action<float> changed)
    {
        GameObject labels = Row(title, panel.transform, 26, 8, false);
        MakeText("Title", labels.transform, title, BodySize, TextColor, TextAlignmentOptions.Left).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        valueText = MakeText("Value", labels.transform, format(value), BodySize, AccentColor, TextAlignmentOptions.Right);
        valueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;

        GameObject root = Make(title + "Slider", panel.transform, typeof(Slider), typeof(LayoutElement));
        root.GetComponent<LayoutElement>().preferredHeight = 28;
        GameObject track = Make("Track", root.transform, typeof(Image));
        Anchor(track, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
        Style(track.GetComponent<Image>(), CardColor);
        GameObject fillArea = Make("FillArea", root.transform);
        Anchor(fillArea, new Vector2(0, 0.35f), new Vector2(1, 0.65f));
        GameObject fill = Make("Fill", fillArea.transform, typeof(Image));
        Anchor(fill, Vector2.zero, Vector2.one); // the Slider only drives x, so y must already span the track
        Style(fill.GetComponent<Image>(), AccentButtonColor);
        GameObject handleArea = Make("HandleArea", root.transform);
        Anchor(handleArea, Vector2.zero, Vector2.one);
        GameObject handle = Make("Handle", handleArea.transform, typeof(Image));
        Anchor(handle, Vector2.zero, Vector2.one);
        ((RectTransform)handle.transform).sizeDelta = new Vector2(24, 0);
        handle.GetComponent<Image>().sprite = Circle;
        handle.GetComponent<Image>().color = TextColor;

        var slider = root.GetComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.wholeNumbers = true;
        slider.minValue = min;
        slider.maxValue = max;
        slider.SetValueWithoutNotify(value);
        TMP_Text label = valueText; // out params can't be captured
        slider.onValueChanged.AddListener(v => { label.text = format(v); changed(v); });
        return slider;
    }
}
