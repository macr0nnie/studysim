using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// The two screens before the game: the main menu, then save select (continue, new game, delete).
// One script, built in code like the rest of the UI; the MainMenu and SaveSelect scenes each hold one
// object with it (made by Study Sim > Set Up Menu Scenes and Build).
public class MenuUI : MonoBehaviour
{
    public enum Screen { Main, Select }
    public Screen screen;

    private Transform canvas;
    private GameObject panel;
    private string pending; // a destructive button waiting for its second click
    private float pendingUntil;

    private void Awake()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = LightTheme ? CardColor : Color.Lerp(PanelColor, Color.black, 0.55f);
        }
        canvas = MakeCanvas("MenuCanvas", transform, 0).transform;
        Build();
    }

    private void Update()
    {
        if (pending != null && Time.unscaledTime > pendingUntil) { pending = null; Build(); }
    }

    private void Build()
    {
        if (panel != null) Destroy(panel);
        panel = Make("Panel", canvas, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(640, 0);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (screen == Screen.Main) BuildMain(); else BuildSelect();
    }

    private void BuildMain()
    {
        Text("Title", "Study Sim", TitleSize * 2, AccentColor, 90, true);
        Text("Tagline", "Study. Build your room. Stay focused.", BodySize, MutedText, 36, false);
        Gap(24);
        Button play = Big("Play", "Play", AccentButtonColor, 64);
        play.onClick.AddListener(() => Go(SaveSystem.SelectScene));
        Big("Quit", "Quit", TabColor, 52).onClick.AddListener(Quit);
        Gap(8);
        Text("Version", "v" + Application.version, CaptionSize, MutedText, 22, false);
    }

    private void BuildSelect()
    {
        Text("Title", "Choose a save", HeadingSize + 7, TextColor, 52, true);
        for (int slot = 1; slot < SaveSystem.SlotCount; slot++) SlotRow(slot);
        SlotRow(0);
        Gap(6);
        Big("Back", "Back", TabColor, 48).onClick.AddListener(() => Go(SaveSystem.MenuScene));
    }

    private void SlotRow(int slot)
    {
        SaveSystem.Snapshot snap = SaveSystem.Peek(slot);
        GameObject row = Row("Slot" + slot, panel.transform, 78, 10, false);
        Style(row.AddComponent<Image>(), CardColor);
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(16, 12, 10, 10);
        h.childAlignment = TextAnchor.MiddleLeft;
        string title = slot == 0 ? "Autosave" : "Slot " + slot;
        string detail = snap == null ? "Empty" : $"Level {snap.level}  ·  {snap.coins:N0} coins  ·  {snap.SavedAt:MMM d, HH:mm}";
        MakeText("Info", row.transform, $"<b>{title}</b>\n<size={CaptionSize}><color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{detail}</color></size>",
            BodySize, TextColor, TextAlignmentOptions.Left).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        if (snap == null)
        {
            SmallButton(row.transform, "Start", AccentButtonColor, 120).onClick.AddListener(() => SaveSystem.Play(slot, true));
            return;
        }
        SmallButton(row.transform, "Continue", AccentButtonColor, 120).onClick.AddListener(() => SaveSystem.Play(slot, false));
        Confirm(row.transform, "new" + slot, "New game", "Overwrite?", 110, () => SaveSystem.Play(slot, true));
        Confirm(row.transform, "del" + slot, "Delete", "Sure?", 90, () => { SaveSystem.Delete(slot); Build(); });
    }

    // Two clicks within three seconds: the first arms the button, the second does it.
    private void Confirm(Transform row, string key, string label, string armed, float width, System.Action action)
    {
        bool isArmed = pending == key;
        Button button = SmallButton(row, isArmed ? armed : label, isArmed ? DangerColor : TabColor, width);
        button.onClick.AddListener(() =>
        {
            if (pending == key) { pending = null; action(); return; }
            pending = key;
            pendingUntil = Time.unscaledTime + 3f;
            Build();
        });
    }

    private void Text(string name, string text, float size, Color color, float height, bool display)
    {
        TMP_Text t = MakeText(name, panel.transform, text, size, color, TextAlignmentOptions.Center, display);
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private Button Big(string name, string label, Color color, float height)
    {
        Button button = TextButton(name, panel.transform, label, color, HeadingSize);
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return button;
    }

    private void Gap(float height) => Make("Gap", panel.transform, typeof(LayoutElement)).GetComponent<LayoutElement>().preferredHeight = height;

    private static void Go(string scene)
    {
        if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
        else Debug.LogError($"Scene '{scene}' isn't in the build. Run Study Sim > Set Up Menu Scenes and Build.");
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
