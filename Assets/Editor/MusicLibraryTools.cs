using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Study Sim > Build Music Library: groups the tracks in Assets/Audio into playlists with cover art, prices
// and unlock levels. Keeps the first two playlists (the free ambient ones) and replaces the placeholder
// copies after them. Safe to run again: it rebuilds the same playlists.
public static class MusicLibraryTools
{
    const string LibraryPath = "Assets/Scripts/Audio/PlaylistLibrary.asset", CoverFolder = "Assets/ART/UI/MusicCovers";

    // file name contains -> title, artist
    static readonly (string key, string title, string artist)[] Tracks =
    {
        ("emotional-piano", "Emotional Piano", ""), ("cozy-piano", "Cozy Piano", ""), ("evening-glow", "Evening Glow", ""),
        ("soft-background-piano", "Soft Background Piano", ""), ("pure-love", "Pure Love", ""),
        ("good-night-lofi", "Good Night", ""), ("whispering-vinyl", "Whispering Vinyl", ""), ("iced coffee", "Iced Coffee", "moonboy"),
        ("dramatic-orchestral", "Mystery of Tension", ""), ("hope-overture", "Hope Overture", ""), ("yagi-wrath", "Yagi Wrath", ""), ("spellcraft", "Spellcraft", ""),
        ("once-in-paris", "Once in Paris", ""), ("summer-walk", "Summer Walk", ""),
    };

    // title, price, unlock level, hue, track titles
    static readonly (string title, int price, int level, float hue, string[] songs)[] Lists =
    {
        ("Calm Piano", 200, 2, 0.60f, new[] { "Emotional Piano", "Cozy Piano", "Evening Glow", "Soft Background Piano", "Pure Love" }),
        ("Lofi Cafe", 300, 3, 0.08f, new[] { "Good Night", "Whispering Vinyl", "Iced Coffee" }),
        ("Daydream", 400, 5, 0.42f, new[] { "Once in Paris", "Summer Walk" }),
        ("Cinematic", 600, 8, 0.78f, new[] { "Mystery of Tension", "Hope Overture", "Yagi Wrath", "Spellcraft" }),
    };

    [MenuItem("Study Sim/Build Music Library")]
    public static void Build()
    {
        var library = AssetDatabase.LoadAssetAtPath<PlaylistLibrary>(LibraryPath);
        if (library == null) { Debug.LogError("No playlist library at " + LibraryPath); return; }
        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" })
            .Select(g => AssetDatabase.GUIDToAssetPath(g)).Select(p => (path: p, clip: AssetDatabase.LoadAssetAtPath<AudioClip>(p))).ToList();
        Directory.CreateDirectory(CoverFolder);

        if (library.playlists.Count > 2) library.playlists.RemoveRange(2, library.playlists.Count - 2);
        foreach (var list in Lists)
        {
            Sprite cover = MakeCover(list.title, list.hue);
            var playlist = new Playlist { title = list.title, coverArt = cover, price = list.price, unlockLevel = list.level };
            foreach (string songTitle in list.songs)
            {
                var track = Tracks.First(t => t.title == songTitle);
                var file = clips.FirstOrDefault(c => c.path.ToLowerInvariant().Contains(track.key));
                if (file.clip == null) { Debug.LogWarning("No audio file for " + songTitle); continue; }
                playlist.songs.Add(new Song { title = track.title, artist = track.artist, audioClip = file.clip, coverArt = cover });
            }
            library.playlists.Add(playlist);
        }
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Music library now has {library.playlists.Count} playlists.");
    }

    // A soft two-colour gradient with a glow, tinted per playlist.
    private static Sprite MakeCover(string name, float hue)
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color top = Color.HSVToRGB(hue, 0.45f, 0.95f), bottom = Color.HSVToRGB(Mathf.Repeat(hue + 0.08f, 1), 0.7f, 0.45f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float glow = Mathf.Exp(-((x - n * 0.65f) * (x - n * 0.65f) + (y - n * 0.7f) * (y - n * 0.7f)) / (n * n * 0.07f));
                tex.SetPixel(x, y, Color.Lerp(Color.Lerp(bottom, top, y / (float)n), Color.white, glow * 0.45f));
            }
        string path = $"{CoverFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
