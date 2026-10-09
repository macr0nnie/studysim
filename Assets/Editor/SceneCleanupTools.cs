using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The HUD, planner, store, saves, settings and music player are built at runtime, so the scene's old UI
// still shows in the Scene view. This switches those replaced objects off in the open scene (kept, not
// deleted, because iPodUIController and others still reference them). Play mode hides them anyway.
public static class SceneCleanupTools
{
    static readonly string[] Replaced =
    {
        "IPOD", "MusicStore", "HomeButton", "money_text", "player_experience", "coinsText",
        "Wall_ColorChanger", "Camera_ColorChanger", "EditMode", "Icon (3)", "Button_Panel",
    };

    [MenuItem("Study Sim/Hide Replaced Scene UI")]
    public static void HideReplacedUI()
    {
        int hidden = 0;
        foreach (string name in Replaced)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;
            Undo.RecordObject(go, "Hide replaced UI");
            go.SetActive(false);
            hidden++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Hid {hidden} replaced UI objects. Save the scene to keep it. The new UI only appears in Play mode.");
    }
}
