using UnityEditor;
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
        PlayerSettings.productName = "Study Sim";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.runInBackground = true; // the study timer and the browser extension must keep running when the window loses focus
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(Scenes + "MainMenu.unity");
        Debug.Log("Study Sim: menu scenes created, Build Settings = MainMenu, SaveSelect, Protoype_2.");
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
