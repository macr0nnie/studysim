using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpScreen : MonoBehaviour
{
    [Header("Level Up Panel")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private Button continueButton;

    [Header("Cutscene Panel")]
    [SerializeField] private GameObject cutscenePanel;
    [SerializeField] private TMP_Text cutsceneTitleText;
    [SerializeField] private TMP_Text cutsceneLineText;
    [SerializeField] private Button nextLineButton;

    [Header("References")]
    [SerializeField] private Experience experience;
    [SerializeField] private StoryManager storyManager;

    private readonly Queue<int> levelUpQueue = new();
    private bool isPlaying;

    private void Awake()
    {
        levelUpPanel.SetActive(false);
        cutscenePanel.SetActive(false);
    }

    private void OnEnable()
    {
        experience.OnLevelUp += EnqueueLevelUp;
    }

    private void OnDisable()
    {
        experience.OnLevelUp -= EnqueueLevelUp;
    }

    private void EnqueueLevelUp(int newLevel)
    {
        levelUpQueue.Enqueue(newLevel);
        if (!isPlaying)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isPlaying = true;
        while (levelUpQueue.Count > 0)
            yield return RunLevelUpSequence(levelUpQueue.Dequeue());
        isPlaying = false;
    }

    private IEnumerator RunLevelUpSequence(int newLevel)
    {
        // --- Level-up panel ---
        levelText.text = $"Level {newLevel}!";
        xpText.text = $"0 / {experience.XPToNextLevel} XP";
        levelUpPanel.SetActive(true);

        yield return WaitForButton(continueButton);

        levelUpPanel.SetActive(false);

        // --- Story cutscene (if unlocked at this level) ---
        StoryChapter chapter = storyManager.GetChapterForLevel(newLevel);
        if (chapter != null)
        {
            yield return PlayCutscene(chapter);
            storyManager.MarkChapterSeen(newLevel);
        }
    }

    private IEnumerator PlayCutscene(StoryChapter chapter)
    {
        cutsceneTitleText.text = chapter.title;
        cutscenePanel.SetActive(true);

        foreach (string line in chapter.lines)
        {
            cutsceneLineText.text = string.Empty;
            yield return TypeLine(line);
            yield return WaitForButton(nextLineButton);
        }

        cutscenePanel.SetActive(false);
    }

    private IEnumerator TypeLine(string line)
    {
        foreach (char c in line)
        {
            cutsceneLineText.text += c;
            yield return new WaitForSeconds(0.03f);
        }
    }

    private IEnumerator WaitForButton(Button button)
    {
        bool clicked = false;
        void Handler() => clicked = true;
        button.onClick.AddListener(Handler);
        yield return new WaitUntil(() => clicked);
        button.onClick.RemoveListener(Handler);
    }
}
