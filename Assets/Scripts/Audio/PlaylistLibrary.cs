using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlaylistLibrary", menuName = "iPod/Playlist Library")]
public class PlaylistLibrary : ScriptableObject
{
    public List<Playlist> playlists = new List<Playlist>();
}