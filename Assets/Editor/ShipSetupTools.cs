using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click release setup: creates the MainMenu and SaveSelect scenes, puts the three scenes in Build
// Settings in play order, and sets the product name, version and background running for the timer.
public static class ShipSetupTools
{
    const string Scenes = "Assets/Scenes/";

    [MenuItem("Study Sim/Set Up Menu Scenes and Build")]
    public static void SetUp()
    {
        MakeScene("MainMenu", MenuUI.Screen.Main);
        MakeScene("SaveSelect", MenuUI.Screen.Select);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Scenes + "MainMenu.unity", true),
            new EditorBuildSettingsScene(Scenes + "SaveSelect.unity", true),
            new EditorBuildSettingsScene(Scenes + "Protoype_2.unity", true),
        };
        PlayerSettings.productName = "lofilife";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.runInBackground = true; // the study timer and the browser extension must keep running when the window loses focus
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(Scenes + "MainMenu.unity");
        Debug.Log("Study Sim: menu scenes created, Build Settings = MainMenu, SaveSelect, Protoype_2.");
    }

    // Web build for itch.io: zip the CONTENTS of Builds/WebGL (index.html at the zip root), upload as HTML,
    // tick "This file will be played in the browser". Gzip + decompression fallback loads on any host, itch.io included.
    [MenuItem("Study Sim/Build for itch.io (WebGL)")]
    public static void BuildWeb()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true; // repeat visits skip the download
        PlayerSettings.runInBackground = true; // the timer keeps going while the player is in another tab
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
        });
        Debug.Log($"Study Sim web build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB in Builds/WebGL.");
        FailCI(report);
    }

    [MenuItem("Study Sim/Build for Windows")]
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
            locationPathName = "Builds/Windows/lofilife.exe",
            target = BuildTarget.StandaloneWindows64,
        });
        Debug.Log($"Study Sim Windows build: {report.summary.result} in Builds/Windows.");
        FailCI(report);
    }

    // The CI workflow runs these with -batchmode; a non-zero exit code is how it learns the build failed.
    static void FailCI(UnityEditor.Build.Reporting.BuildReport report)
    {
        if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    // iPhone/iPad: writes an Xcode project to Builds/iOS. Needs Unity's iOS Build Support module (works on Windows);
    // the project is then opened in Xcode on a Mac to sign, run on a phone and upload to TestFlight.
    [MenuItem("Study Sim/Build for iOS (Xcode project)")]
    public static void BuildIOS()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.macronnie.studysim");
        // Landscape only: the UI is laid out for 1920x1080.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
            locationPathName = "Builds/iOS",
            target = BuildTarget.iOS,
        });
        Debug.Log($"Study Sim iOS build: {report.summary.result}. Open Builds/iOS/Unity-iPhone.xcodeproj in Xcode on a Mac.");
    }

    // Separate so it can wait until testing is done.
    [MenuItem("Study Sim/Remove Experimental Pipeline Package")]
    public static void RemovePipeline() => Client.Remove("com.unity.pipeline");

    private static void MakeScene(string name, MenuUI.Screen screen)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        new GameObject("Menu").AddComponent<MenuUI>().screen = screen;
        EditorSceneManager.SaveScene(scene, Scenes + name + ".unity");
    }
}
