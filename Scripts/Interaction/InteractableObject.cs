using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour
{
    [SerializeField] private bool requiresStoryMode = true;
    [SerializeField] private string interactionId;
    [SerializeField] private UnityEvent onInteract;

    private bool isInteractable = true;

    private void Start()
    {
        if (requiresStoryMode && !StoryManager.Instance.IsStoryModeEnabled())
        {
            enabled = false;
        }
    }

    private void OnMouseDown()
    {
        if (!isInteractable) return;
        
        if (Timer.Instance.isRunning) return; // Prevent interaction during focus time
        
        Interact();
    }

    public void Interact()
    {
        onInteract?.Invoke();
    }

    public void SetInteractable(bool canInteract)
    {
        isInteractable = canInteract;
    }

    public string GetInteractionId()
    {
        return interactionId;
    }
}