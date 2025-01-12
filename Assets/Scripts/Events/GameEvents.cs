using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central event system for game-wide events
/// </summary>


public static class GameEvents
{
    // Currency events
    public static readonly UnityEvent<float> OnMoneyChanged = new UnityEvent<float>();
    // Timer events
    public static readonly UnityEvent OnSessionStarted = new UnityEvent();
    public static readonly UnityEvent OnSessionCompleted = new UnityEvent();
    public static readonly UnityEvent OnSessionPaused = new UnityEvent();
    
    // Store events
    public static readonly UnityEvent<IStoreItem> OnItemPurchased = new UnityEvent<IStoreItem>();
    
    // Room events
    public static readonly UnityEvent OnRoomStateChanged = new UnityEvent();
}