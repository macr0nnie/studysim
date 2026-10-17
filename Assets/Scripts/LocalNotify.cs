using System.Runtime.InteropServices;

// End-of-session notification on iPhone/iPad, from Assets/Plugins/iOS/LocalNotify.mm. Does nothing elsewhere.
// shortcut: iOS only, no package; switch to com.unity.mobile.notifications if Android is added.
public static class LocalNotify
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void LofiNotifyAsk();
    [DllImport("__Internal")] static extern void LofiNotifySchedule(string title, string body, double seconds);
    [DllImport("__Internal")] static extern void LofiNotifyCancel();

    public static void Ask() => LofiNotifyAsk();
    public static void Schedule(string title, string body, double seconds) => LofiNotifySchedule(title, body, seconds);
    public static void Cancel() => LofiNotifyCancel();
#else
    public static void Ask() { }
    public static void Schedule(string title, string body, double seconds) { }
    public static void Cancel() { }
#endif
}
