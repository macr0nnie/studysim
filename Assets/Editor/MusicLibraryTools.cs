using System.Linq;
using UnityEditor;
using UnityEngine;

// Study Sim > Build Music Library: groups the tracks in Assets/Audio into playlists with cover photos, prices
// and unlock levels, replacing whatever was there (the old placeholder playlists pointed at these same files
// under names like "WhiteNoise"). Safe to run again: it rebuilds the same playlists.
public static class MusicLibraryTools
{
    const string LibraryPath = "Assets/Scripts/Audio/PlaylistLibrary.asset";

    // The square cover photos in Assets/ART/UI ("Rectangle N.png"), by number.
    static Sprite Cover(int n) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/ART/UI/Rectangle {n}.png");

    // file name contains -> title, artist, cover photo number
    static readonly (string key, string title, string artist, int cover)[] Tracks =
    {
        ("emotional-piano", "Emotional Piano", "", 50), ("cozy-piano", "Cozy Piano", "", 45), ("evening-glow", "Evening Glow", "", 46),
        ("soft-background-piano", "Soft Background Piano", "", 51), ("pure-love", "Pure Love", "", 38),
        ("good-night-lofi", "Good Night", "", 42), ("whispering-vinyl", "Whispering Vinyl", "", 47), ("iced coffee", "Iced Coffee", "moonboy", 41),
        ("dramatic-orchestral", "Mystery of Tension", "", 42), ("hope-overture", "Hope Overture", "", 43), ("yagi-wrath", "Yagi Wrath", "", 39), ("spellcraft", "Spellcraft", "", 37),
        ("once-in-paris", "Once in Paris", "", 37), ("summer-walk", "Summer Walk", "", 44),
    };

    // title, price, unlock level, cover photo number, track titles
    static readonly (string title, int price, int level, int cover, string[] songs)[] Lists =
    {
        ("Calm Piano", 0, 0, 46, new[] { "Emotional Piano", "Cozy Piano", "Evening Glow", "Soft Background Piano", "Pure Love" }),
        ("Lofi Cafe", 200, 2, 40, new[] { "Good Night", "Whispering Vinyl", "Iced Coffee" }),
        ("Daydream", 300, 4, 44, new[] { "Once in Paris", "Summer Walk" }),
        ("Cinematic", 500, 6, 39, new[] { "Mystery of Tension", "Hope Overture", "Yagi Wrath", "Spellcraft" }),
    };

    [MenuItem("Study Sim/Build Music Library")]
    public static void Build()
    {
        var library = AssetDatabase.LoadAssetAtPath<PlaylistLibrary>(LibraryPath);
        if (library == null) { Debug.LogError("No playlist library at " + LibraryPath); return; }
        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" })
            .Select(g => AssetDatabase.GUIDToAssetPath(g)).Select(p => (path: p, clip: AssetDatabase.LoadAssetAtPath<AudioClip>(p))).ToList();

        library.playlists.Clear();
        foreach (var list in Lists)
        {
            var playlist = new Playlist { title = list.title, coverArt = Cover(list.cover), price = list.price, unlockLevel = list.level };
            foreach (string songTitle in list.songs)
            {
                var track = Tracks.First(t => t.title == songTitle);
                var file = clips.FirstOrDefault(c => c.path.ToLowerInvariant().Contains(track.key));
                if (file.clip == null) { Debug.LogWarning("No audio file for " + songTitle); continue; }
                playlist.songs.Add(new Song { title = track.title, artist = track.artist, audioClip = file.clip, coverArt = Cover(track.cover) });
            }
            library.playlists.Add(playlist);
        }
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Music library now has {library.playlists.Count} playlists.");
    }
}
