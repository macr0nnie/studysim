using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// Now-playing card (bottom right) with a playlist library that opens above it. Replaces the scene's
// iPod sprite, which was a stock device mock-up that didn't match the rest of the UI.
public class MusicPlayerUI : MonoBehaviour
{
    const float Width = 440, CardHeight = 168, LibraryHeight = 360;

    private MusicPlayer music;
    private PlayerCurrency currency;
    private Image art, artNote, playIcon, pauseLeft, pauseRight;
    private TMP_Text title, artist, elapsed, total, nowLabel;
    private RectTransform progressFill;
    private RectTransform[] eqBars;
    private GameObject library;
    private Transform libraryRows;
    private static Sprite triangle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsurePlayer();
        EnsurePlayer();
    }

    private static void EnsurePlayer()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<MusicPlayer>() != null
            && FindFirstObjectByType<MusicPlayerUI>() == null)
            new GameObject("MusicPlayerUI").AddComponent<MusicPlayerUI>();
    }

    private void Awake()
    {
        music = FindFirstObjectByType<MusicPlayer>();
        currency = FindFirstObjectByType<PlayerCurrency>();
        BuildUI();
        // The old iPod screens stay in the scene (iPodUIController still references them) but out of sight.
        foreach (string old in new[] { "IPOD", "MusicStore", "HomeButton" })
        {
            GameObject go = GameObject.Find(old);
            if (go != null) go.SetActive(false);
        }
    }

    private void OnEnable() => music.OnSongChanged.AddListener(ShowSong);
    private void OnDisable() => music.OnSongChanged.RemoveListener(ShowSong);

    private void Update()
    {
        bool playing = music.IsPlaying;
        playIcon.enabled = !playing;
        pauseLeft.enabled = pauseRight.enabled = playing;

        float length = music.SongLength, time = music.SongTime;
        progressFill.anchorMax = new Vector2(length > 0 ? Mathf.Clamp01(time / length) : 0, 1);
        elapsed.text = Clock(time);
        total.text = length > 0 ? Clock(length) : "--:--";

        // Little equalizer next to "Now playing": bounces while music plays, rests when paused.
        for (int i = 0; i < eqBars.Length; i++)
        {
            float h = playing ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * (5.1f + i * 1.7f) + i)) : 0.25f;
            eqBars[i].anchorMax = new Vector2(eqBars[i].anchorMax.x, h);
        }
    }

    private static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    private void ShowSong(Song song)
    {
        if (song == null) return;
        title.text = song.title;
        artist.text = string.IsNullOrEmpty(song.artist) ? (music.CurrentPlaylist?.title ?? "") : song.artist;
        nowLabel.text = music.CurrentPlaylist != null ? music.CurrentPlaylist.title.ToUpperInvariant() : "NOW PLAYING";
        Sprite cover = song.coverArt != null ? song.coverArt : music.CurrentPlaylist?.coverArt;
        art.sprite = cover;
        art.color = cover != null ? Color.white : CardColor;
        artNote.enabled = cover == null;
        if (library.activeSelf) RefreshLibrary();
    }

    // ---------- library ----------

    private void ToggleLibrary()
    {
        library.SetActive(!library.activeSelf);
        if (library.activeSelf) RefreshLibrary();
    }

    private void RefreshLibrary()
    {
        for (int i = libraryRows.childCount - 1; i >= 0; i--) Destroy(libraryRows.GetChild(i).gameObject);
        foreach (Playlist playlist in music.GetAllPlaylists()) AddPlaylistRow(playlist);
    }

    private void AddPlaylistRow(Playlist playlist)
    {
        bool current = playlist == music.CurrentPlaylist;
        GameObject row = Row("Playlist", libraryRows, 64, 10, false);
        Style(row.AddComponent<Image>(), current ? SelectedColor : CardColor);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.MiddleLeft;

        Image thumb = Cover(row.transform, 48, out Image thumbNote);
        thumb.sprite = playlist.coverArt;
        thumb.color = playlist.coverArt != null ? Color.white : TabColor;
        thumbNote.enabled = playlist.coverArt == null;
        if (!playlist.isUnlocked) thumb.color *= new Color(0.55f, 0.55f, 0.6f, 1f); // locked ones read dimmer

        string songs = playlist.songs.Count == 1 ? "1 track" : $"{playlist.songs.Count} tracks";
        TMP_Text label = MakeText("Label", row.transform,
            $"{playlist.title}\n<size=15><color=#{ColorUtility.ToHtmlStringRGB(MutedText)}>{(current ? "Playing now" : songs)}</color></size>",
            19, TextColor, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        if (playlist.isUnlocked)
        {
            SmallButton(row.transform, current ? "Restart" : "Play", current ? TabColor : AccentButtonColor, 76)
                .onClick.AddListener(() => { music.PlayPlaylist(playlist); RefreshLibrary(); });
            return;
        }
        bool affordable = currency != null && currency.GetCoins() >= playlist.price;
        Button buy = SmallButton(row.transform, $"{playlist.price:N0}", affordable ? AccentButtonColor : TabColor, 76);
        Sprite coin = UIIcons.Named("Icons_21");
        if (coin != null)
        {
            var labelRect = (RectTransform)buy.GetComponentInChildren<TMP_Text>().transform;
            labelRect.offsetMin = new Vector2(22, 0);
            GameObject icon = Make("Coin", buy.transform, typeof(Image));
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.sizeDelta = new Vector2(18, 18);
            iconRect.anchoredPosition = new Vector2(10, 0);
            icon.GetComponent<Image>().sprite = coin;
            icon.GetComponent<Image>().color = AccentColor;
            icon.GetComponent<Image>().raycastTarget = false;
        }
        buy.interactable = affordable;
        buy.onClick.AddListener(() =>
        {
            // Pay first so a failed payment never unlocks anything.
            if (playlist.price > 0 && (currency == null || !currency.SpendCoins(playlist.price))) return;
            music.PurchasePlaylist(playlist, int.MaxValue);
            FindFirstObjectByType<GameHUD>()?.ShowToast($"Unlocked {playlist.title}");
            RefreshLibrary();
        });
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("MusicCanvas", transform, 9).transform;

        // Now-playing card.
        GameObject card = Make("NowPlaying", canvas, typeof(Image), typeof(Shadow));
        Corner(card, new Vector2(Width, CardHeight), new Vector2(-24, 24));
        Style(card.GetComponent<Image>(), PanelColor);
        var shadow = card.GetComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.35f);
        shadow.effectDistance = new Vector2(0, -6);

        float artSize = CardHeight - 32;
        art = Cover(card.transform, artSize, out artNote);
        var artRect = (RectTransform)art.transform.parent;
        artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(0, 0.5f);
        artRect.anchoredPosition = new Vector2(16, 0);
        art.color = CardColor;

        GameObject info = Make("Info", card.transform, typeof(VerticalLayoutGroup));
        var infoRect = (RectTransform)info.transform;
        infoRect.anchorMin = Vector2.zero;
        infoRect.anchorMax = Vector2.one;
        infoRect.offsetMin = new Vector2(16 + artSize + 16, 14);
        infoRect.offsetMax = new Vector2(-16, -12);
        var v = info.GetComponent<VerticalLayoutGroup>();
        v.spacing = 2;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        // Equalizer + playlist name + library button.
        GameObject top = Row("Top", info.transform, 24, 6, false);
        top.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        GameObject eq = Make("Equalizer", top.transform, typeof(LayoutElement));
        eq.GetComponent<LayoutElement>().preferredWidth = 16;
        eqBars = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject bar = Make("Bar", eq.transform, typeof(Image));
            bar.GetComponent<Image>().color = AccentColor;
            bar.GetComponent<Image>().raycastTarget = false;
            eqBars[i] = (RectTransform)bar.transform;
            eqBars[i].anchorMin = new Vector2(i / 3f + 0.04f, 0.15f);
            eqBars[i].anchorMax = new Vector2((i + 1) / 3f - 0.04f, 0.25f);
            eqBars[i].offsetMin = eqBars[i].offsetMax = Vector2.zero;
        }
        nowLabel = MakeText("Now", top.transform, "NOW PLAYING", 14, MutedText, TextAlignmentOptions.Left);
        nowLabel.characterSpacing = 8;
        nowLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        SmallButton(top.transform, "Library", TabColor, 72).onClick.AddListener(ToggleLibrary);

        title = MakeText("Title", info.transform, "Nothing playing", 28, TextColor, TextAlignmentOptions.Left, display: true);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
        artist = MakeText("Artist", info.transform, "Open the library to pick a playlist", 16, MutedText, TextAlignmentOptions.Left);
        artist.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

        // Progress: thin bar, times under it.
        GameObject track = Make("Progress", info.transform, typeof(Image), typeof(LayoutElement));
        track.GetComponent<LayoutElement>().preferredHeight = 6;
        Style(track.GetComponent<Image>(), CardColor);
        GameObject fill = Make("Fill", track.transform, typeof(Image));
        Style(fill.GetComponent<Image>(), AccentColor);
        progressFill = (RectTransform)fill.transform;
        Anchor(fill, Vector2.zero, new Vector2(0, 1));
        GameObject times = Row("Times", info.transform, 16, 0, true);
        elapsed = MakeText("Elapsed", times.transform, "0:00", 13, MutedText, TextAlignmentOptions.Left);
        total = MakeText("Total", times.transform, "--:--", 13, MutedText, TextAlignmentOptions.Right);

        // Transport, centred.
        GameObject controls = Row("Controls", info.transform, 38, 18, false);
        controls.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        Transport(controls.transform, "Previous", 30, TabColor, music.PlayPreviousSong, flip: true);
        Button play = Transport(controls.transform, "PlayPause", 38, AccentButtonColor, music.TogglePlayPause, flip: false);
        playIcon = play.transform.Find("Glyph").GetComponent<Image>();
        pauseLeft = Bar(play.transform, -5);
        pauseRight = Bar(play.transform, 5);
        Transport(controls.transform, "Next", 30, TabColor, music.PlayNextSong, flip: false);

        // Library, floating just above the card.
        library = Make("Library", canvas, typeof(Image), typeof(VerticalLayoutGroup), typeof(Shadow));
        Corner(library, new Vector2(Width, LibraryHeight), new Vector2(-24, 24 + CardHeight + 12));
        Style(library.GetComponent<Image>(), PanelColor);
        library.GetComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.35f);
        var lv = library.GetComponent<VerticalLayoutGroup>();
        lv.padding = new RectOffset(16, 16, 12, 14);
        lv.spacing = 8;
        lv.childControlWidth = lv.childControlHeight = true;
        lv.childForceExpandWidth = true;
        lv.childForceExpandHeight = false;
        Header(library.transform, "Library", ToggleLibrary);
        libraryRows = ScrollList(library.transform, out _);
        var list = libraryRows.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 6;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        library.SetActive(false);
    }

    private static void Corner(GameObject go, Vector2 size, Vector2 offset)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    // Rounded, masked album art with a music-note placeholder. Returns the art image (child of the mask).
    private static Image Cover(Transform parent, float size, out Image note)
    {
        GameObject frame = Make("Cover", parent, typeof(Image), typeof(Mask), typeof(LayoutElement));
        ((RectTransform)frame.transform).sizeDelta = new Vector2(size, size);
        var element = frame.GetComponent<LayoutElement>();
        element.preferredWidth = element.preferredHeight = size;
        element.flexibleWidth = 0;
        Style(frame.GetComponent<Image>(), Color.white);
        frame.GetComponent<Mask>().showMaskGraphic = false;

        GameObject artGO = Make("Art", frame.transform, typeof(Image));
        Anchor(artGO, Vector2.zero, Vector2.one);
        var image = artGO.GetComponent<Image>();
        image.preserveAspect = false;
        image.raycastTarget = false;

        GameObject noteGO = Make("Note", frame.transform, typeof(Image));
        Anchor(noteGO, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
        note = noteGO.GetComponent<Image>();
        note.sprite = UIIcons.Named("Icons_5");
        note.preserveAspect = true;
        note.color = MutedText;
        note.raycastTarget = false;
        note.enabled = note.sprite != null;
        return image;
    }

    // Round button with a drawn triangle glyph (plus a bar for previous/next).
    private Button Transport(Transform parent, string name, float size, Color color, UnityEngine.Events.UnityAction onClick, bool flip)
    {
        GameObject go = Make(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = element.preferredHeight = size;
        var image = go.GetComponent<Image>();
        image.sprite = Circle;
        image.color = color;
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        GameObject glyph = Make("Glyph", go.transform, typeof(Image));
        var g = (RectTransform)glyph.transform;
        g.sizeDelta = new Vector2(size * 0.4f, size * 0.4f);
        bool skip = name != "PlayPause";
        g.anchoredPosition = new Vector2(skip ? (flip ? 1.5f : -1.5f) : size * 0.04f, 0);
        if (flip) g.localScale = new Vector3(-1, 1, 1);
        glyph.GetComponent<Image>().sprite = Triangle;
        glyph.GetComponent<Image>().color = TextColor;
        glyph.GetComponent<Image>().raycastTarget = false;
        if (skip) Bar(go.transform, flip ? -size * 0.22f : size * 0.22f, size * 0.36f);
        return button;
    }

    private static Image Bar(Transform parent, float x, float height = 16)
    {
        GameObject bar = Make("Bar", parent, typeof(Image));
        var rect = (RectTransform)bar.transform;
        rect.sizeDelta = new Vector2(4, height);
        rect.anchoredPosition = new Vector2(x, 0);
        var image = bar.GetComponent<Image>();
        image.color = TextColor;
        image.raycastTarget = false;
        return image;
    }

    // Right-pointing anti-aliased triangle, built once.
    private static Sprite Triangle
    {
        get
        {
            if (triangle != null) return triangle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Signed distance to the left edge and to the two slanted edges, 1px soft.
                    float px = x + 0.5f, py = y + 0.5f, half = n / 2f;
                    float slant = (n - px) - Mathf.Abs(py - half) * 2f;
                    float d = Mathf.Min(px - 2, slant * 0.447f); // 0.447 = 1/(2*sqrt(1.25)) normalises the slant
                    byte a = (byte)(Mathf.Clamp01(d) * 255);
                    pixels[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return triangle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }
    }
}
