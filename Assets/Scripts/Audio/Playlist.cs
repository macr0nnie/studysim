
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Playlist
{
    public string title;
   // public string description;
    public Sprite coverArt;
    public int price;
    public int unlockLevel; // reaching this player level unlocks it for free; 0 = coins only
    public bool isUnlocked;
    public List<Song> songs = new List<Song>();
}
