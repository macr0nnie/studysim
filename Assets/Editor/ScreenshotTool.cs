using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Study Sim > Capture Screenshot (Ctrl+Shift+K): saves the Game view, UI and post-processing included, to
// Screenshots/ next to Assets, so lighting changes can be compared against a reference. Works best in Play mode.
public static class ScreenshotTool
{
    [MenuItem("Study Sim/Capture Screenshot %#k")]
    private static void Capture()
    {
        string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"lofilife-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path); // written at the end of the Game view's next frame
        Debug.Log($"Screenshot saved to {path}");
    }
}
