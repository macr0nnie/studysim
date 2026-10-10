using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UIKit;

// In-game saving: an autosave into the slot picked on the save-select screen every couple of minutes and on quit,
// the Save now key (F5), and a small "Saved" badge when it happens. Slots are only chosen in the main menu.
public class SavesUI : MonoBehaviour
{
    [SerializeField] private float autosaveSeconds = 120f;

    private TMP_Text badge;
    private float nextAutosave;
    private Coroutine flash;
    private bool manual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsureSaves();
        EnsureSaves();
    }

    private static void EnsureSaves()
    {
        if (FindAnyObjectByType<RoomManager>() != null && FindAnyObjectByType<SavesUI>() == null)
            new GameObject("Saves").AddComponent<SavesUI>();
    }

    private void Awake()
    {
        BuildUI();
        nextAutosave = Time.unscaledTime + autosaveSeconds;
        SaveSystem.Saved += OnSaved;
    }

    private void OnDestroy() => SaveSystem.Saved -= OnSaved;

    private void OnApplicationQuit() => SaveSystem.Save(SaveSystem.CurrentSlot);

    private void Update()
    {
        if (Controls.Pressed(Controls.Act.Saves) && !Typing()) SaveNow();
        if (Time.unscaledTime >= nextAutosave)
        {
            nextAutosave = Time.unscaledTime + autosaveSeconds;
            SaveSystem.Save(SaveSystem.CurrentSlot);
        }
    }

    // Also the Save now button in Settings.
    public void SaveNow()
    {
        manual = true;
        SaveSystem.Save(SaveSystem.CurrentSlot);
        nextAutosave = Time.unscaledTime + autosaveSeconds;
    }

    private void OnSaved(int slot)
    {
        bool shown = manual || GameSettings.AutosaveBadge;
        string message = manual ? "Saved" : "Autosaved";
        manual = false;
        if (!shown) return;
        if (flash != null) StopCoroutine(flash);
        flash = StartCoroutine(FlashBadge(message));
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

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("SavesCanvas", transform, 10).transform;

        // Top right under the timer card, so saves are visible without covering the dock or the music card.
        badge = MakeText("SavedBadge", canvas, "", BodySize, MutedText, TextAlignmentOptions.Right);
        var badgeRect = (RectTransform)badge.transform;
        badgeRect.anchorMin = badgeRect.anchorMax = badgeRect.pivot = new Vector2(1, 1);
        badgeRect.sizeDelta = new Vector2(260, 28);
        badgeRect.anchoredPosition = new Vector2(-24, -112);
        badge.gameObject.SetActive(false);
    }
}
