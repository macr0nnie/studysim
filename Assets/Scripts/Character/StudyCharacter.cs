using UnityEngine;
using System.Collections;

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