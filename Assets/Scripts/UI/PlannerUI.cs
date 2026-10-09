using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UIKit;

// To-do list and daily habits in one left drawer (shares the store's slot, so only one is open).
// Ticking things off earns XP. Press P to toggle it.
// Habits follow "don't break the chain": each habit shows this week as dots joined into a chain while the
// streak holds, a month calendar shows how full each day was, and a missed day warns "never miss twice"
// (one slip is normal; two in a row is how habits die).
public class PlannerUI : MonoBehaviour
{
    const int TaskExperience = 10, HabitExperience = 15, MilestoneExperience = 25;
    static readonly int[] Milestones = { 3, 7, 14, 21, 30, 50, 100, 365 };
    static readonly string[] DayLetters = { "M", "T", "W", "T", "F", "S", "S" };

    private TaskList taskList;
    private HabitTracker habits;
    private Experience experience;
    private GameObject panel, todoPage, habitPage;
    private Transform todoRows, habitRows;
    private TMP_Text todoEmpty, habitEmpty;
    private Image todoTab, habitTab;
    // Something just ticked off: after the list is rebuilt its new dot or check pops with "+XP".
    private object celebrate;
    private string celebrateText;
    private RectTransform celebrateTarget;

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
        if (Controls.Pressed(Controls.Act.Planner) && !Typing()) Toggle();
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
        PlayCelebration();
    }

    private void RefreshHabits()
    {
        Clear(habitRows);
        if (habits.habits.Count > 0) AddMonth();
        foreach (StudyHabit habit in habits.habits) AddHabitCard(habit);
        habitEmpty.gameObject.SetActive(habits.habits.Count == 0);
        PlayCelebration();
    }

    private static void Clear(Transform rows)
    {
        for (int i = rows.childCount - 1; i >= 0; i--) Destroy(rows.GetChild(i).gameObject);
    }

    private void AddTodoRow(Task task)
    {
        GameObject row = ListRow(todoRows);
        RectTransform check = Check(row.transform, task.isComplete, () =>
        {
            if (taskList.ToggleTask(task))
            {
                if (experience != null) experience.GainExperience(TaskExperience);
                Celebrate(task, $"+{TaskExperience} XP");
            }
            RefreshTodos();
        });
        if (celebrate == task) celebrateTarget = check;
        TMP_Text label = MakeText("Task", row.transform, task.taskName, BodySize, task.isComplete ? MutedText : TextColor, TextAlignmentOptions.Left);
        if (task.isComplete) label.fontStyle = FontStyles.Strikethrough;
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        SmallButton(row.transform, "x", CardColor, 32).onClick.AddListener(() => { taskList.RemoveTask(task); RefreshTodos(); });
    }

    // ---------- habits ----------

    private void ToggleHabit(StudyHabit habit)
    {
        if (habits.ToggleToday(habit))
        {
            int xp = HabitExperience;
            string text = $"+{xp} XP";
            if (Array.IndexOf(Milestones, habit.currentStreak) >= 0)
            {
                xp += MilestoneExperience;
                text = $"{habit.currentStreak}-day streak!  +{xp} XP";
                GameHUD hud = FindFirstObjectByType<GameHUD>();
                if (hud != null) hud.ShowToast($"{habit.currentStreak}-day streak on \"{habit.habitName}\"! Keep the chain going.");
            }
            if (experience != null) experience.GainExperience(xp);
            Celebrate(habit, text);
        }
        RefreshHabits();
    }

    // This month as a calendar: each day's dot grows and turns green with the share of habits done.
    private void AddMonth()
    {
        DateTime today = DateTime.Now.Date;
        var first = new DateTime(today.Year, today.Month, 1);
        int perfect = 0;
        for (DateTime d = first; d <= today; d = d.AddDays(1))
            if (Share(d) >= 1f) perfect++;

        GameObject card = Card("Month");
        GameObject title = Row("Title", card.transform, 26, 8, false);
        MakeText("Month", title.transform, today.ToString("MMMM yyyy", CultureInfo.CurrentCulture), BodySize, TextColor, TextAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        MakeText("Perfect", title.transform, perfect == 1 ? "1 perfect day" : $"{perfect} perfect days", LabelSize, AccentColor, TextAlignmentOptions.Right)
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 150;

        GameObject grid = Make("Days", card.transform, typeof(GridLayoutGroup));
        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(50, 28);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 7;
        layout.childAlignment = TextAnchor.UpperCenter;
        foreach (string letter in DayLetters)
            MakeText("Day", grid.transform, letter, CaptionSize, MutedText, TextAlignmentOptions.Center);
        for (int i = 0; i < Monday(first); i++) Make("Blank", grid.transform);
        for (DateTime d = first; d.Month == first.Month; d = d.AddDays(1))
        {
            Transform cell = Make(d.Day.ToString(), grid.transform).transform;
            if (d > today) { Dot(cell, 6, new Color(MutedText.r, MutedText.g, MutedText.b, 0.18f)); continue; }
            float share = Share(d);
            if (d == today) Ring(cell, 26);
            if (share <= 0) Dot(cell, 8, new Color(MutedText.r, MutedText.g, MutedText.b, 0.35f));
            else Dot(cell, Mathf.Lerp(11, 22, share), Color.Lerp(AccentColor, XpColor, share));
        }
    }

    // Share of habits done on a day (0-1).
    private float Share(DateTime day)
    {
        if (habits.habits.Count == 0) return 0;
        int done = 0;
        foreach (StudyHabit h in habits.habits) if (h.DoneOn(day)) done++;
        return (float)done / habits.habits.Count;
    }

    private static int Monday(DateTime day) => ((int)day.DayOfWeek + 6) % 7; // 0 = Monday

    // One habit: name and streak, then this week's dots, chained while the streak holds. Today's dot ticks it.
    private void AddHabitCard(StudyHabit habit)
    {
        DateTime today = DateTime.Now.Date;
        bool doneToday = habit.DoneOn(today);
        bool missedYesterday = !habit.DoneOn(today.AddDays(-1));
        bool slipping = !doneToday && missedYesterday && habit.DoneOn(today.AddDays(-2));

        GameObject card = Card("Habit");
        GameObject top = Row("Top", card.transform, 28, 8, false);
        top.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        MakeText("Name", top.transform, habit.habitName, BodySize, TextColor, TextAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        int streak = habit.currentStreak;
        MakeText("Streak", top.transform, streak > 0 ? $"<b>{streak}</b> day{(streak == 1 ? "" : "s")}" : "", LabelSize,
            doneToday ? AccentColor : MutedText, TextAlignmentOptions.Right).gameObject.AddComponent<LayoutElement>().preferredWidth = 80;
        SmallButton(top.transform, "x", PanelColor, 28).onClick.AddListener(() => { habits.RemoveHabit(habit.habitName); RefreshHabits(); });

        GameObject week = Row("Week", card.transform, 30, 0, true);
        GameObject letters = Row("Letters", card.transform, 16, 0, true);
        DateTime monday = today.AddDays(-Monday(today));
        for (int i = 0; i < 7; i++)
        {
            DateTime day = monday.AddDays(i);
            bool done = habit.DoneOn(day);
            Transform cell = Make(day.ToString("ddd"), week.transform, typeof(LayoutElement)).transform;
            cell.GetComponent<LayoutElement>().flexibleWidth = 1;
            // Chain links to the neighbouring days that were done too.
            if (done && i > 0 && habit.DoneOn(day.AddDays(-1))) Link(cell, 0f, 0.5f);
            if (done && i < 6 && day < today && habit.DoneOn(day.AddDays(1))) Link(cell, 0.5f, 1f);

            if (day == today)
            {
                Image ring = Ring(cell, 28, slipping ? AccentColor : TextColor);
                if (slipping) StartCoroutine(Pulse(ring));
                RectTransform dot = done ? Dot(cell, 22, XpColor) : null;
                if (celebrate == habit && dot != null) celebrateTarget = dot;
                // The whole cell is the button: a big target is quicker to hit.
                GameObject hit = Make("Tick", cell, typeof(Image), typeof(Button));
                Stretch((RectTransform)hit.transform);
                hit.GetComponent<Image>().color = Color.clear;
                hit.GetComponent<Button>().onClick.AddListener(() => ToggleHabit(habit));
            }
            else if (day > today) Dot(cell, 6, new Color(MutedText.r, MutedText.g, MutedText.b, 0.18f));
            else if (done) Dot(cell, 22, XpColor);
            else Dot(cell, 9, new Color(MutedText.r, MutedText.g, MutedText.b, 0.4f));

            MakeText("Letter", letters.transform, DayLetters[i], CaptionSize, day == today ? TextColor : MutedText, TextAlignmentOptions.Center)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        string status = doneToday ? $"Done today. Best streak: {Math.Max(habit.bestStreak, streak)} days."
            : slipping ? "Missed yesterday. Never miss twice: tick it today!"
            : streak > 0 ? $"Tick today to keep your {streak}-day chain going."
            : "Tap today's dot to start a chain.";
        MakeText("Status", card.transform, status, CaptionSize, slipping ? AccentColor : MutedText, TextAlignmentOptions.Left)
            .gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
    }

    private GameObject Card(string name)
    {
        GameObject card = Make(name, habitRows, typeof(Image), typeof(VerticalLayoutGroup));
        Style(card.GetComponent<Image>(), CardColor);
        var v = card.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 8, 8, 8);
        v.spacing = 4;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return card;
    }

    private static RectTransform Dot(Transform cell, float size, Color color)
    {
        GameObject dot = Make("Dot", cell, typeof(Image));
        var rect = (RectTransform)dot.transform;
        rect.sizeDelta = new Vector2(size, size);
        var image = dot.GetComponent<Image>();
        image.sprite = Circle;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    // Outline circle for today (a dot with the card colour punched out of it).
    private static Image Ring(Transform cell, float size) => Ring(cell, size, AccentColor);

    private static Image Ring(Transform cell, float size, Color color)
    {
        Image ring = Dot(cell, size, color).GetComponent<Image>();
        Dot(cell, size - 4, CardColor);
        return ring;
    }

    private static void Link(Transform cell, float from, float to)
    {
        GameObject bar = Make("Link", cell, typeof(Image));
        var rect = (RectTransform)bar.transform;
        rect.anchorMin = new Vector2(from, 0.5f);
        rect.anchorMax = new Vector2(to, 0.5f);
        rect.sizeDelta = new Vector2(0, 8);
        var image = bar.GetComponent<Image>();
        image.color = new Color(XpColor.r, XpColor.g, XpColor.b, 0.55f);
        image.raycastTarget = false;
    }

    private static IEnumerator Pulse(Image ring)
    {
        Color color = ring.color;
        while (ring != null)
        {
            ring.color = new Color(color.r, color.g, color.b, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f));
            yield return null;
        }
    }

    // ---------- the tick ----------

    private void Celebrate(object what, string text)
    {
        celebrate = what;
        celebrateText = text;
        celebrateTarget = null;
    }

    private void PlayCelebration()
    {
        if (celebrateTarget != null)
        {
            UISound.Play(UISound.Cue.Coin);
            StartCoroutine(Pop(celebrateTarget));
            StartCoroutine(Floater(celebrateTarget, celebrateText));
        }
        celebrate = null;
        celebrateTarget = null;
    }

    // Squash and overshoot, like a stamp landing.
    private static IEnumerator Pop(RectTransform target)
    {
        for (float t = 0; t < 1 && target != null; t += Time.unscaledDeltaTime / 0.35f)
        {
            float s = t < 0.35f ? Mathf.Lerp(0.4f, 1.3f, t / 0.35f) : Mathf.Lerp(1.3f, 1f, (t - 0.35f) / 0.65f);
            target.localScale = Vector3.one * s;
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one;
    }

    // "+15 XP" drifting up from what was ticked, above the list so the scroll mask doesn't clip it.
    private IEnumerator Floater(RectTransform from, string text)
    {
        yield return null; // let the rebuilt list lay itself out first
        if (from == null) yield break;
        TMP_Text label = MakeText("XP", panel.transform, text, LabelSize + 2, AccentColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var rect = (RectTransform)label.transform;
        rect.sizeDelta = new Vector2(240, 30);
        Vector3 start = from.position;
        for (float t = 0; t < 1 && label != null; t += Time.unscaledDeltaTime / 0.9f)
        {
            rect.position = start + Vector3.up * (12 + 46 * t) * rect.lossyScale.y;
            label.alpha = t < 0.6f ? 1 : 1 - (t - 0.6f) / 0.4f;
            yield return null;
        }
        if (label != null) Destroy(label.gameObject);
    }

    // ---------- UI construction ----------

    private void BuildUI()
    {
        Transform canvas = MakeCanvas("PlannerCanvas", transform, 10).transform;

        // Sits next to the Shop button in the room-button row.
        Button open = NavButton(canvas, 3, "Planner", Controls.Act.Planner);
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
        habitPage = Page("HabitPage", "A small daily habit, e.g. Read 10 pages", name =>
        {
            if (habits.habits.Exists(h => h.habitName == name)) return;
            habits.AddHabit(name, "", 0);
            RefreshHabits();
        }, out habitRows, out habitEmpty, "Add a small habit and tap today's dot each day. Don't break the chain!");

        MakeText("Hint", panel.transform, $"+{TaskExperience} XP per task, +{HabitExperience} XP per habit a day, bonus at 3, 7, 14, 30 days", CaptionSize, MutedText, TextAlignmentOptions.Center)
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
    private static RectTransform Check(Transform row, bool done, Action onClick)
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
        return (RectTransform)box.transform;
    }
}
