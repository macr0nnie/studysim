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

    public static void EnsurePlayer()
    {
        // Protoype_2 has a leftover StudyCharacter on GameManager with no model, so only a rigged one counts as placed.
        if (FindAnyObjectByType<RoomManager>() == null) return;
        foreach (StudyCharacter c in FindObjectsByType<StudyCharacter>())
            if (c.GetComponent<Animator>() != null) return;
        GameObject prefab = Resources.Load<GameObject>("Player");
        if (prefab == null) { Debug.LogWarning("No Resources/Player prefab: run Study Sim > Create Player Prefab"); return; }
        // Spawn standing mid-floor, sized to the room; CharacterBehaviour then moves it onto a chair or bed (anchored to their bounds).
        Bounds floor = default; bool any = false;
        foreach (Renderer r in FindAnyObjectByType<RoomManager>().FloorRenderers) if (r != null) { if (!any) { floor = r.bounds; any = true; } else floor.Encapsulate(r.bounds); }
        if (!any) { Debug.LogWarning("No floor in this room; not placing the character"); return; }
        Vector3 spot = new Vector3(floor.center.x, floor.max.y, floor.center.z);
        Vector3 toCam = Camera.main ? Vector3.ProjectOnPlane(Camera.main.transform.position - spot, Vector3.up) : Vector3.back;
        GameObject player = Instantiate(prefab, spot, Quaternion.LookRotation(toCam.sqrMagnitude > 0.0001f ? toCam : Vector3.back));
        player.name = "Player";
        // shortcut: a person is ~0.9 of the bed's long side (else 2.3 desk heights, else 0.4 of the floor); tune if the models are rescaled
        GameObject bed = GameObject.Find("Bed"), desk = GameObject.Find("desk");
        float height = Mathf.Min(floor.size.x, floor.size.z) * 0.4f;
        if (bed != null && TryBounds(bed, out Bounds bb)) height = Mathf.Max(bb.size.x, bb.size.z) * 0.9f;
        else if (desk != null && TryBounds(desk, out Bounds db)) height = db.size.y * 2.3f;
        if (TryBounds(player, out Bounds body) && body.size.y > 0.0001f)
            player.transform.localScale *= height / body.size.y;
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
        
        if (GetComponent<CharacterBehaviour>() == null) gameObject.AddComponent<CharacterBehaviour>();

        // Start in studying state
        StartStudying();
        timer = FindAnyObjectByType<TimerManager>();
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

        bool wasStudying = isStudying; // StopStudying clears it
        StopStudying();
        animator.SetBool(InteractingParam, true);
        
        foreach (string line in dialogueEvent.dialogueLines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            if (TryGetComponent(out CharacterBehaviour behaviour)) behaviour.Say(line);
            yield return new WaitForSeconds(2f); // Adjust timing as needed
        }
   
        animator.SetBool(InteractingParam, false);
        dialogueEvent.hasOccurred = true;
        
        if (wasStudying)
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