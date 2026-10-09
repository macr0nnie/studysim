using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// To-do list and daily habits in one left drawer (shares the store's slot, so only one is open).
// Ticking things off earns XP. Press P to toggle it.
public class PlannerUI : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.P;
    const int TaskExperience = 10, HabitExperience = 15;

    private TaskList taskList;
    private HabitTracker habits;
    private Experience experience;
    private GameObject panel, todoPage, habitPage;
    private Transform todoRows, habitRows;
    private TMP_Text todoEmpty, habitEmpty;
    private Image todoTab, habitTab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToRoomScenes()
    {
        SceneManager.sceneLoaded += (scene, mode) => EnsurePlanner();
        EnsurePlanner();
    }

    private static void EnsurePlanner()
    {
        if (FindFirstObjectByType<RoomManager>() != null && FindFirstObjectByType<PlannerUI>() == null)
            new GameObject("Planner").AddComponent<PlannerUI>();
    }

    private void Awake()
    {
        taskList = FindFirstObjectByType<TaskList>() ?? gameObject.AddComponent<TaskList>();
        habits = FindFirstObjectByType<HabitTracker>() ?? gameObject.AddComponent<HabitTracker>();
        experience = FindFirstObjectByType<Experience>();
        BuildUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && !Typing()) Toggle();
    }

    public void Toggle()
    {
        if (ToggleDrawer(this, panel)) RefreshAll();
    }

    private void ShowPage(bool todo)
    {
        todoPage.SetActive(todo);
        habitPage.SetActive(!todo);
        todoTab.color = todo ? SelectedColor : TabColor;
        habitTab.color = todo ? TabColor : SelectedColor;
    }

    private void RefreshAll()
    {
        RefreshTodos();
        RefreshHabits();
    }

    private void RefreshTodos()
    {
        Clear(todoRows);
        // Open tasks first, finished ones sink to the bottom.
        foreach (bool done in new[] { false, true })
            foreach (Task task in taskList.tasks)
                if (task.isComplete == done) AddTodoRow(task);
        todoEmpty.gameObject.SetActive(taskList.tasks.Count == 0);
    }

    private void RefreshHabits()
    {
        Clear(habitRows);
        foreach (StudyHabit habit in habits.habits) AddHabitRow(habit);
        habitEmpty.gameObject.SetActive(habits.habits.Count == 0);
    }

    private static void Clear(Transform rows)
    {
        for (int i = rows.childCount - 1; i >= 0; i--) Destroy(rows.GetChild(i).gameObject);
    }

    private void AddTodoRow(Task task)
    {
        GameObject row = ListRow(todoRows);
        Check(row.transform, task.isComplete, () =>
        {
            if (taskList.ToggleTask(task) && experience != null) experience.GainExperience(TaskExperience);
            RefreshTodos();
        });
        TMP_Text label = MakeText("Task", row.transform, task.taskName, BodySize, task.isComplete ? MutedText : TextColor, TextAlignmentOptions.Left);
        if (task.isComplete) label.fontStyle = FontStyles.Strikethrough;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        SmallButton(row.transform, "x", CardColor, 32).onClick.AddListener(() => { taskList.RemoveTask(task); RefreshTodos(); });
    }

    private void AddHabitRow(StudyHabit habit)
    {
        GameObject row = ListRow(habitRows);
        Check(row.transform, habit.isCompletedToday, () =>
        {
            if (habit.isCompletedToday) return; // once a day
            habits.CompleteHabit(habit.habitName);
            if (experience != null) experience.GainExperience(HabitExperience);
            RefreshHabits();
        });
        TMP_Text label = MakeText("Habit", row.transform, habit.habitName, BodySize, TextColor, TextAlignmentOptions.Left);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        int streak = LiveStreak(habit);
        TMP_Text streakText = MakeText("Streak", row.transform, streak > 0 ? $"{streak} day streak" : "start today", 15,
            streak > 0 ? AccentColor : MutedText, TextAlignmentOptions.Right);
        streakText.gameObject.AddComponent<LayoutElement>().preferredWidth = 100;
        SmallButton(row.transform, "x", CardColor, 32).onClick.AddListener(() => { habits.RemoveHabit(habit.habitName); RefreshHabits(); });
    }

    // A streak only counts while it is unbroken: last done today or yesterday.
    private static int LiveStreak(StudyHabit habit)
    {
        if (habit.completionDates.Count == 0) return 0;
        DateTime last = habit.completionDates[habit.completionDates.Count - 1].Date;
        return (DateTime.Now.Date - last).Days <= 1 ? habit.currentStreak : 0;
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("PlannerCanvas", transform, 10).transform;

        // Sits next to the Shop button in the room-button row.
        Button open = NavButton(canvas, 2, "Planner", "P");
        open.onClick.AddListener(Toggle);

        panel = MakeDrawer("PlannerPanel", canvas);
        Header(panel.transform, "Planner", Toggle);

        GameObject tabs = Row("Tabs", panel.transform, 34, 6, true);
        Button todoButton = TextButton("To-do", tabs.transform, "To-do", TabColor, LabelSize);
        Button habitButton = TextButton("Habits", tabs.transform, "Daily habits", TabColor, LabelSize);
        todoTab = todoButton.GetComponent<Image>();
        habitTab = habitButton.GetComponent<Image>();
        todoButton.onClick.AddListener(() => ShowPage(true));
        habitButton.onClick.AddListener(() => ShowPage(false));

        todoPage = Page("TodoPage", "What do you need to get done?", name => { taskList.AddTask(name); RefreshTodos(); },
            out todoRows, out todoEmpty, "Nothing on your list yet. Add a task above.");
        habitPage = Page("HabitPage", "A habit to keep, e.g. Read 20 min", name =>
        {
            if (habits.habits.Exists(h => h.habitName == name)) return;
            habits.AddHabit(name, "", 0);
            RefreshHabits();
        }, out habitRows, out habitEmpty, "Add a habit and tick it off each day to build a streak.");

        MakeText("Hint", panel.transform, $"+{TaskExperience} XP per task, +{HabitExperience} XP per habit each day", CaptionSize, MutedText, TextAlignmentOptions.Center)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
        ShowPage(true);
    }

    // Input row + scrolling list + empty-state message.
    private GameObject Page(string name, string placeholder, Action<string> add, out Transform rows, out TMP_Text empty, string emptyText)
    {
        GameObject page = Make(name, panel.transform, typeof(VerticalLayoutGroup), typeof(LayoutElement));
        page.GetComponent<LayoutElement>().flexibleHeight = 1;
        var layout = page.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        GameObject inputRow = Row("Add", page.transform, 40, 8, false);
        TMP_InputField field = MakeInput(inputRow.transform, placeholder);
        void Submit()
        {
            string text = field.text.Trim();
            if (text.Length == 0) return;
            add(text);
            field.text = "";
            field.ActivateInputField(); // keep typing the next one
        }
        field.onSubmit.AddListener(_ => Submit());
        SmallButton(inputRow.transform, "Add", AccentButtonColor, 64).onClick.AddListener(Submit);

        empty = MakeText("Empty", page.transform, emptyText, LabelSize, MutedText, TextAlignmentOptions.Center);
        empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
        empty.textWrappingMode = TextWrappingModes.Normal;

        rows = ScrollList(page.transform, out _);
        var list = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 6;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        return page;
    }

    private static GameObject ListRow(Transform parent)
    {
        GameObject row = Row("Row", parent, 44, 8, false);
        row.AddComponent<Image>();
        Style(row.GetComponent<Image>(), CardColor);
        row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 6, 6, 6);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        return row;
    }

    // Round checkbox: filled when done.
    private static void Check(Transform row, bool done, Action onClick)
    {
        GameObject box = Make("Check", row, typeof(Image), typeof(Button), typeof(LayoutElement));
        var element = box.GetComponent<LayoutElement>();
        element.preferredWidth = element.preferredHeight = 28;
        element.flexibleWidth = 0;
        var image = box.GetComponent<Image>();
        image.sprite = Circle;
        image.color = done ? XpColor : TabColor;
        box.GetComponent<Button>().onClick.AddListener(() => onClick());
        if (done)
        {
            GameObject dot = Make("Dot", box.transform, typeof(Image));
            Anchor(dot, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
            dot.GetComponent<Image>().sprite = Circle;
            dot.GetComponent<Image>().color = PanelColor;
            dot.GetComponent<Image>().raycastTarget = false;
        }
    }
}
