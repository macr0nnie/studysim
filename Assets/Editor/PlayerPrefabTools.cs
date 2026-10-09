using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Resources/Player.prefab: the study character with a seated pose and a cheer when a session ends.
public static class PlayerPrefabTools
{
    const string Folder = "Assets/ART/Character/";
    const string PrefabPath = "Assets/Resources/Player.prefab";
    const string ControllerPath = "Assets/Resources/PlayerAnimator.controller";

    [InitializeOnLoadMethod]
    static void CreateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // An empty controller means an earlier build never flushed its states to disk.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null || controller == null || controller.layers.Length == 0)
                CreatePlayerPrefab();
        };
    }

    [MenuItem("Study Sim/Create Player Prefab")]
    public static void CreatePlayerPrefab()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Ch03_nonPBR.fbx");
        if (model == null) { Debug.LogWarning("Player prefab: " + Folder + "Ch03_nonPBR.fbx not found"); return; }

        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsStudying", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsInteracting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Celebrate", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState study = machine.AddState("Study");
        study.motion = Clip("Sitting Disbelief");
        study.speed = 0; // hold the seated first frame rather than play the reaction
        machine.defaultState = study;

        AnimationClip cheer = Clip("Sitting Victory");
        if (cheer != null)
        {
            AnimatorState celebrate = machine.AddState("Celebrate");
            celebrate.motion = cheer;
            AnimatorStateTransition into = machine.AddAnyStateTransition(celebrate);
            into.AddCondition(AnimatorConditionMode.If, 0, "Celebrate");
            into.canTransitionToSelf = false;
            into.duration = 0.25f;
            AnimatorStateTransition back = celebrate.AddTransition(study);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.25f;
        }

        var player = (GameObject)PrefabUtility.InstantiatePrefab(model);
        player.name = "Player";
        if (!player.TryGetComponent(out Animator animator)) animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        player.AddComponent<StudyCharacter>();
        PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
        Object.DestroyImmediate(player);
        AssetDatabase.SaveAssets(); // the controller's states and parameters only reach disk on a save
        Debug.Log("Player prefab created at " + PrefabPath);
    }

    static AnimationClip Clip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(Folder + file + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
}
