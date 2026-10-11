using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Loading screen between the save-select screen and the room. The room scene and the furniture catalog (it references
// every store prefab, which was the long freeze after pressing Play) load in the background behind a progress bar,
// and the screen lifts once the room has drawn its first frames.
public class LoadingScreen : MonoBehaviour
{
    private static readonly string[] Preload = { "FurnitureCatalog", "Player" };
    private static readonly Object[] held = new Object[Preload.Length]; // the scene change unloads unreferenced assets
    private static bool busy;

    private RectTransform fill;
    private Canvas canvas;

    public static void Load(string scene)
    {
        if (busy) return; // a second click on Play
        busy = true;
        var go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        go.AddComponent<LoadingScreen>().StartCoroutine(go.GetComponent<LoadingScreen>().Run(scene));
    }

    private void BuildUI()
    {
        canvas = MakeCanvas("LoadingCanvas", transform, 1000);
        GameObject back = Make("Background", canvas.transform, typeof(Image));
        Stretch((RectTransform)back.transform);
        back.GetComponent<Image>().color = LightTheme ? CardColor : Color.Lerp(PanelColor, Color.black, 0.55f); // the menu's backdrop

        TMP_Text title = MakeText("Title", canvas.transform, "lofilife", TitleSize * 2, AccentColor, TextAlignmentOptions.Center, true);
        title.rectTransform.sizeDelta = new Vector2(600, 90);
        title.rectTransform.anchoredPosition = new Vector2(0, 60);
        TMP_Text label = MakeText("Label", canvas.transform, "Loading your room...", BodySize, MutedText, TextAlignmentOptions.Center);
        label.rectTransform.sizeDelta = new Vector2(500, 40);

        GameObject track = Make("Track", canvas.transform, typeof(Image));
        Style(track.GetComponent<Image>(), TabColor);
        var trackRect = (RectTransform)track.transform;
        trackRect.sizeDelta = new Vector2(420, 14);
        trackRect.anchoredPosition = new Vector2(0, -50);
        GameObject bar = Make("Fill", track.transform, typeof(Image));
        Style(bar.GetComponent<Image>(), AccentButtonColor);
        fill = (RectTransform)bar.transform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0, 1);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
    }

    private IEnumerator Run(string scene)
    {
        BuildUI();
        yield return null; // show the screen before the heavy loads start

        // The catalog first: Unity holds other async loads back while a scene waits to activate, so they can't overlap.
        for (int i = 0; i < Preload.Length; i++)
        {
            ResourceRequest request = Resources.LoadAsync(Preload[i]);
            while (!request.isDone)
            {
                SetProgress(0.6f * (i + request.progress) / Preload.Length);
                yield return null;
            }
            held[i] = request.asset;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(scene);
        while (!load.isDone)
        {
            SetProgress(0.6f + 0.4f * load.progress);
            yield return null;
        }
        SetProgress(1f);
        // Awake and Start run on activation and the first frames build the HUD and room; keep covering them.
        for (int i = 0; i < 3; i++) yield return null;

        var group = canvas.gameObject.AddComponent<CanvasGroup>();
        for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.3f)
        {
            group.alpha = 1 - t;
            yield return null;
        }
        busy = false;
        Destroy(gameObject);
    }

    private void SetProgress(float value) => fill.anchorMax = new Vector2(value, 1);
}
