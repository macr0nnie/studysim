using System.Collections.Generic;
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

    // Deletes the objects HideReplacedUI switches off, but only those nothing else in the scene points at.
    // The ones something still references are listed with the script and field, to be cleared by hand first.
    [MenuItem("Study Sim/Delete Replaced Scene UI")]
    public static void DeleteReplacedUI()
    {
        var doomed = new List<GameObject>();
        foreach (string name in Replaced)
        {
            GameObject go = FindIncludingInactive(name);
            if (go != null) doomed.Add(go);
        }
        int deleted = 0;
        foreach (GameObject go in doomed)
        {
            if (go == null) continue; // already gone with a parent
            var referrers = new List<string>();
            foreach (MonoBehaviour script in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (script == null || script.transform.IsChildOf(go.transform)) continue;
                var so = new SerializedObject(script);
                for (SerializedProperty p = so.GetIterator(); p.NextVisible(true);)
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                    var target = p.objectReferenceValue as Component != null ? ((Component)p.objectReferenceValue).transform : (p.objectReferenceValue as GameObject)?.transform;
                    if (target != null && target.IsChildOf(go.transform)) referrers.Add($"{script.GetType().Name}.{p.propertyPath} on {script.name}");
                }
            }
            if (referrers.Count > 0)
            {
                Debug.LogWarning($"Kept {go.name}: still referenced by {string.Join(", ", referrers)}. Clear those fields, then run this again.");
                continue;
            }
            Undo.DestroyObjectImmediate(go);
            deleted++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Deleted {deleted} replaced UI objects. Save the scene to keep it.");
    }

    private static GameObject FindIncludingInactive(string name)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }
}
