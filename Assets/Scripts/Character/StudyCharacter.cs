using UnityEngine;
using System.Collections;

/// <summary>
/// Controls the studying character's behavior and interactions
/// Setup: Attach to the character model in the scene
/// Dependencies: Requires character model with animations
/// </summary>
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
        foreach (var dialogueEvent in dialogueEvents)
        {
            if (dialogueEvent.triggerName == triggerName && !dialogueEvent.hasOccurred)
            {
                StartCoroutine(PlayDialogueSequence(dialogueEvent));
                break;
            }
        }
    }

    private IEnumerator PlayDialogueSequence(DialogueEvent dialogueEvent)
    {
        animator.SetBool(InteractingParam, true);
        
        foreach (string line in dialogueEvent.dialogueLines)
        {
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