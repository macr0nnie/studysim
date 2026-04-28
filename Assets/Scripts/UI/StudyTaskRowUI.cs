using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StudyTaskRowUI : MonoBehaviour
{
    [SerializeField] private Toggle completeToggle;
    [SerializeField] private TMP_Text taskLabel;
    [SerializeField] private Button deleteButton;

    private string rawName;

    public void Setup(StudyTask task, Action<bool> onToggle, Action onDelete)
    {
        rawName = task.taskName;
        completeToggle.SetIsOnWithoutNotify(task.isComplete);
        ApplyStyle(task.isComplete);

        completeToggle.onValueChanged.AddListener(complete =>
        {
            ApplyStyle(complete);
            onToggle(complete);
        });

        deleteButton.onClick.AddListener(() => onDelete());
    }

    private void ApplyStyle(bool complete)
    {
        taskLabel.text = complete ? $"<s>{rawName}</s>" : rawName;
        taskLabel.color = complete ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
    }
}
