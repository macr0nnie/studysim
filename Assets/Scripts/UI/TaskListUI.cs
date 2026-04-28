using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TaskListUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TaskList taskList;
    [SerializeField] private Transform taskContainer;
    [SerializeField] private GameObject taskRowPrefab;

    [Header("Input")]
    [SerializeField] private TMP_InputField taskInputField;
    [SerializeField] private Button addButton;
    [SerializeField] private Button clearCompletedButton;

    private void Start()
    {
        addButton.onClick.AddListener(OnAddClicked);
        clearCompletedButton.onClick.AddListener(taskList.ClearCompleted);
        taskInputField.onSubmit.AddListener(_ => OnAddClicked());
        taskList.OnTasksChanged += RefreshList;
        RefreshList();
    }

    private void OnDestroy()
    {
        taskList.OnTasksChanged -= RefreshList;
    }

    private void OnAddClicked()
    {
        taskList.AddTask(taskInputField.text);
        taskInputField.text = string.Empty;
        taskInputField.ActivateInputField();
    }

    private void RefreshList()
    {
        foreach (Transform child in taskContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < taskList.Tasks.Count; i++)
        {
            int index = i;
            GameObject row = Instantiate(taskRowPrefab, taskContainer);
            row.GetComponent<StudyTaskRowUI>().Setup(
                taskList.Tasks[i],
                complete => taskList.SetComplete(index, complete),
                () => taskList.RemoveTask(index)
            );
        }
    }
}
