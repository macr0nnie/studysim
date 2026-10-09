using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class StudyCharacter : MonoBehaviour
{
    [System.Serializable]
    public class DialogueEvent
    {
        public string triggerName;
        public string[] dialogueLines;
        public bool hasOccurred;
    }

    [Header("Character Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private DialogueEvent[] dialogueEvents;
    
    [Header("Study State")]
    [SerializeField] private float studyAnimationSpeed = 1f;
    
    private bool isStudying;
    private static readonly int StudyingParam = Animator.StringToHash("IsStudying");
    private static readonly int InteractingParam = Animator.StringToHash("IsInteracting");
    private static readonly int CelebrateParam = Animator.StringToHash("Celebrate");
    private TimerManager timer;

    // Rooms without a placed character get the Player prefab (Resources/Player, built by Study Sim > Create Player Prefab).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsurePlayer();
        EnsurePlayer();
    }

    private static void EnsurePlayer()
    {
        // Protoype_2 has a leftover StudyCharacter on GameManager with no model, so only a rigged one counts as placed.
        if (FindFirstObjectByType<RoomManager>() == null) return;
        foreach (StudyCharacter c in FindObjectsByType<StudyCharacter>(FindObjectsSortMode.None))
            if (c.GetComponent<Animator>() != null) return;
        GameObject prefab = Resources.Load<GameObject>("Player");
        GameObject desk = GameObject.Find("desk");
        if (prefab == null) { Debug.LogWarning("No Resources/Player prefab: run Study Sim > Create Player Prefab"); return; }
        // The desk's meshes can sit on children, so measure them all.
        if (desk == null || !TryBounds(desk, out Bounds b)) { Debug.LogWarning("No desk with a mesh in this room; not placing the character"); return; }

        // shortcut: seat guessed from the desk bounds (camera side, sized to the desk); drag Player into the scene to place it exactly
        Vector3 toCamera = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.position - b.center, Vector3.up).normalized : Vector3.back;
        Vector3 seat = new Vector3(b.center.x, b.min.y, b.center.z) + toCamera * Mathf.Max(b.extents.x, b.extents.z);
        GameObject player = Instantiate(prefab, seat, Quaternion.LookRotation(-toCamera));
        player.name = "Player";
        // Size by measurement: the prefab is measured standing (bind pose), and a person is ~2.4x desk height.
        if (TryBounds(player, out Bounds body) && body.size.y > 0.0001f)
            player.transform.localScale *= b.size.y * 2.4f / body.size.y;
    }

    private static bool TryBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator component not found!");
                enabled = false;
                return;
            }
        }

        if (dialogueEvents == null || dialogueEvents.Length == 0)
        {
            Debug.LogWarning("No dialogue events configured.");
        }
        
        // Start in studying state
        StartStudying();
        timer = FindFirstObjectByType<TimerManager>();
        if (timer != null) timer.OnTimerComplete += OnTimerComplete;
    }

    private void OnDestroy()
    {
        if (timer != null) timer.OnTimerComplete -= OnTimerComplete;
    }

    // A cheer when a study session finishes (the timer has already flipped to the break).
    private void OnTimerComplete()
    {
        if (!timer.IsStudySession && enabled) animator.SetTrigger(CelebrateParam);
    }

    public void StartStudying()
    {
        isStudying = true;
        animator.SetBool(StudyingParam, true);
        animator.speed = studyAnimationSpeed;
    }

    public void StopStudying()
    {
        isStudying = false;
        animator.SetBool(StudyingParam, false);
        animator.speed = 1f;
    }

    public void TriggerDialogue(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName))
        {
            Debug.LogError("Invalid trigger name!");
            return;
        }

        if (currentDialogueCoroutine != null)
        {
            StopCoroutine(currentDialogueCoroutine);
            animator.SetBool(InteractingParam, false);
        }

        foreach (var dialogueEvent in dialogueEvents)
        {
            if (dialogueEvent != null && dialogueEvent.triggerName == triggerName && !dialogueEvent.hasOccurred)
            {
                currentDialogueCoroutine = StartCoroutine(PlayDialogueSequence(dialogueEvent));
                break;
            }
        }
    }

    private Coroutine currentDialogueCoroutine;

    private IEnumerator PlayDialogueSequence(DialogueEvent dialogueEvent)
    {
        if (dialogueEvent == null || dialogueEvent.dialogueLines == null)
        {
            Debug.LogError("Invalid dialogue event!");
            yield break;
        }

        StopStudying();
        animator.SetBool(InteractingParam, true);
        
        foreach (string line in dialogueEvent.dialogueLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            // TODO: Implement dialogue UI system
            Debug.Log($"Character says: {line}");
            yield return new WaitForSeconds(2f); // Adjust timing as needed
        }
   
        animator.SetBool(InteractingParam, false);
        dialogueEvent.hasOccurred = true;
        
        if (isStudying)
        {
            StartStudying();
        }
    }
    public void OnInteractionStart()
    {
        StopStudying();
        animator.SetBool(InteractingParam, true);
    }

    public void OnInteractionEnd()
    {
        animator.SetBool(InteractingParam, false);
        StartStudying();
    }
}