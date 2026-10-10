using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Every key and mouse control in the game, on the Input System. Keys can be rebound in Settings > Controls;
// the overrides are saved in PlayerPrefs.
public static class Controls
{
    public enum Act { Edit, Store, Planner, Saves, Settings, Undo, Redo, Rotate, Delete, Grid, Raise, Lower, Grow, Shrink, MiniMode }

    public static readonly string[] Labels =
    {
        "Edit mode", "Shop", "Planner", "Save now", "Settings", "Undo", "Redo", "Rotate / flip (hold to spin, mouse wheel 15°)", "Delete piece", "Grid snap", "Raise piece", "Lower piece", "Enlarge piece", "Shrink piece", "Mini mode",
    };

    private static readonly string[] Defaults =
    {
        "<Keyboard>/tab", "<Keyboard>/b", "<Keyboard>/p", "<Keyboard>/f5", "<Keyboard>/escape", "<Keyboard>/z", "<Keyboard>/y",
        "<Keyboard>/r", "<Keyboard>/delete", "<Keyboard>/g",
        "<Keyboard>/pageUp", "<Keyboard>/pageDown", "<Keyboard>/equals", "<Keyboard>/minus", "<Keyboard>/f9",
    };

    private const string SaveKey = "ControlBindings";
    private static readonly InputActionMap map = new InputActionMap("Game");
    private static readonly InputAction[] actions = new InputAction[Defaults.Length];

    public static event Action Rebound;

    static Controls()
    {
        for (int i = 0; i < Defaults.Length; i++)
            actions[i] = map.AddAction(((Act)i).ToString(), InputActionType.Button, Defaults[i]);
        actions[(int)Act.Delete].AddBinding("<Keyboard>/backspace");
        actions[(int)Act.Grow].AddBinding("<Keyboard>/numpadPlus");
        actions[(int)Act.Shrink].AddBinding("<Keyboard>/numpadMinus");
        string saved = PlayerPrefs.GetString(SaveKey, "");
        if (saved.Length > 0)
        {
            try { map.LoadBindingOverridesFromJson(saved); }
            catch (Exception e) { Debug.LogWarning($"Ignoring saved key bindings: {e.Message}"); }
        }
        map.Enable();
    }

    public static bool Pressed(Act act) => actions[(int)act].WasPressedThisFrame();
    public static bool Released(Act act) => actions[(int)act].WasReleasedThisFrame();
    public static bool Held(Act act) => actions[(int)act].IsPressed();

    // Mini mode is display only: every key but its own is switched off while it's on.
    public static void MiniOnly(bool on)
    {
        for (int i = 0; i < actions.Length; i++)
            if (i != (int)Act.MiniMode) { if (on) actions[i].Disable(); else actions[i].Enable(); }
    }

    // The key shown on buttons and in Settings, e.g. "B" or "F10".
    public static string KeyName(Act act) => actions[(int)act].GetBindingDisplayString(0);

    // Pointer: the mouse, or a touch on touch screens.
    public static Vector2 PointerPosition => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
    public static bool ClickDown => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
    public static bool ClickUp => Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;
    public static bool ClickHeld => Pointer.current != null && Pointer.current.press.isPressed;
    public static bool RightClickDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    public static bool Alt => Keyboard.current != null && Keyboard.current.altKey.isPressed;

    // Waits for the next key press and binds it to act. Esc cancels; mouse buttons are ignored.
    public static void Rebind(Act act, Action done)
    {
        InputAction action = actions[(int)act];
        action.Disable();
        action.PerformInteractiveRebinding(0)
            .WithControlsExcluding("<Mouse>")
            .WithControlsExcluding("<Pointer>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => Finish(op, action, done))
            .OnCancel(op => Finish(op, action, done))
            .Start();
    }

    private static void Finish(InputActionRebindingExtensions.RebindingOperation op, InputAction action, Action done)
    {
        op.Dispose();
        action.Enable();
        Save();
        done?.Invoke();
    }

    public static void ResetAll()
    {
        map.RemoveAllBindingOverrides();
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(SaveKey, map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        Rebound?.Invoke();
    }
}
