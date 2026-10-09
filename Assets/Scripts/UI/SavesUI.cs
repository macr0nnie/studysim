using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Save / load drawer (F5) with an autosave every couple of minutes and a small "Saved" badge when it happens.
public class SavesUI : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F5;
    [SerializeField] private float autosaveSeconds = 120f;

    private GameObject panel;
    private Transform slots;
    private TMP_Text newGameLabel, badge;
    private bool confirmNewGame;
    private float nextAutosave;
    private Coroutine flash;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureSaves();
        EnsureSaves();
    }

    private static void EnsureSaves()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<SavesUI>() == null)
            new GameObject("Saves").AddComponent<SavesUI>();
    }

    private void Awake()
    {
        BuildUI();
        nextAutosave = Time.unscaledTime + autosaveSeconds;
        SaveSystem.Saved += OnSaved;
    }

    private void OnDestroy() => SaveSystem.Saved -= OnSaved;

    private void OnApplicationQuit() => SaveSystem.Save(0);

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !Typing()) Toggle();
        if (Time.unscaledTime >= nextAutosave)
        {
            nextAutosave = Time.unscaledTime + autosaveSeconds;
            SaveSystem.Save(0);
        }
    }

    public void Toggle()
    {
        confirmNewGame = false;
        if (ToggleDrawer(this, panel)) Refresh();
    }

    private void OnSaved(int slot)
    {
        if (panel.activeSelf) Refresh();
        if (flash != null) StopCoroutine(flash); // not StopAllCoroutines: that would freeze the drawer's slide-in
        flash = StartCoroutine(FlashBadge(slot == 0 ? "Autosaved" : $"Saved to slot {slot}"));
    }

    private IEnumerator FlashBadge(string message)
    {
        badge.text = message;
        badge.gameObject.SetActive(true);
        for (float t = 0; t < 2.5f; t += Time.unscaledDeltaTime)
        {
            badge.alpha = Mathf.Clamp01(Mathf.Min(t / 0.2f, (2.5f - t) / 0.6f));
            yield return null;
        }
        badge.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        for (int i = slots.childCount - 1; i >= 0; i--) Destroy(slots.GetChild(i).gameObject);
        for (int slot = 0; slot < SaveSystem.SlotCount; slot++) AddSlot(slot);
        newGameLabel.text = confirmNewGame ? "Click again to start over" : "New game";
    }

    private void AddSlot(int slot)
    {
        SaveSystem.Snapshot save = SaveSystem.Peek(slot);
        GameObject row = Row("Slot", slots, 64, 8, false);
        Style(row.AddComponent<Image>(), CardColor);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 8, 8, 8);
        layout.childAlignment = TextAnchor.MiddleLeft;

        string title = slot == 0 ? "Autosave" : $"Slot {slot}";
        string detail = save == null ? "Empty"
            : $"Level {save.level}  ·  {save.coins} coins  ·  {save.SavedAt.ToString("MMM d, h:mm tt", CultureInfo.CurrentCulture)}";
        TMP_Text label = MakeText("Label", row.transform, $"<b>{title}</b>\n<size=14><color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{detail}</color></size>",
            20, TextColor, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        if (slot != 0) SmallButton(row.transform, "Save", AccentButtonColor, 64).onClick.AddListener(() => SaveSystem.Save(slot));
        Button load = SmallButton(row.transform, "Load", TabColor, 64);
        load.interactable = save != null;
        load.onClick.AddListener(() => SaveSystem.Load(slot));
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("SavesCanvas", transform, 10).transform;

        // Room-button row: Edit, Shop, Paint, Planner, Saves, Settings.
        Button open = NavButton(canvas, 4, "Saves", "F5");
        open.onClick.AddListener(Toggle);

        panel = MakeDrawer("SavesPanel", canvas);
        Header(panel.transform, "Saves", Toggle);
        MakeText("Hint", panel.transform, "Your progress also saves itself as you play.", LabelSize, MutedText, TextAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

        slots = ScrollList(panel.transform, out _);
        var list = slots.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 8;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;

        // Two clicks to wipe; the old game still lands in the autosave.
        Button newGame = TextButton("NewGame", panel.transform, "New game", DangerColor, BodySize);
        newGame.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        newGameLabel = newGame.GetComponentInChildren<TMP_Text>();
        newGame.onClick.AddListener(() =>
        {
            if (confirmNewGame) SaveSystem.NewGame();
            confirmNewGame = true;
            newGameLabel.text = "Click again to start over";
        });

        // Bottom right, just left of the music card, so autosaves are visible without interrupting.
        badge = MakeText("SavedBadge", canvas, "", BodySize, MutedText, TextAlignmentOptions.Right);
        var badgeRect = (RectTransform)badge.transform;
        badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(1, 0);
        badgeRect.sizeDelta = new Vector2(260, 28);
        badgeRect.anchoredPosition = new Vector2(-480, 28);
        badge.gameObject.SetActive(false);
    }
}
