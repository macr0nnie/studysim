using UnityEngine;

/// <summary>
/// Holds all story content for the burnout horror arc and tracks chapter progress.
/// StoryManager calls GetAllChapters() at runtime to populate itself if no
/// chapters are assigned in the Inspector.
/// </summary>
public class MainStory : MonoBehaviour
{
    public string currentQuest = "The Good Student";
    public int currentChapter = 0;

    // -------------------------------------------------------------------------
    // Story Content — all 10 beats across 20 levels
    // -------------------------------------------------------------------------

    public static StoryChapter[] GetAllChapters() => new StoryChapter[]
    {
        // ACT ONE: Normal ─────────────────────────────────────────────────────

        new()
        {
            title = "The Good Student",
            unlockLevel = 2,
            lines = new[]
            {
                "Another session complete. You're doing great.",
                "The exam is in three weeks. Plenty of time.",
                "You have a plan. You always have a plan.",
                "You've always been good at this.",
                "...Haven't you?"
            }
        },

        new()
        {
            title = "The Schedule",
            unlockLevel = 4,
            lines = new[]
            {
                "You haven't eaten since this morning.",
                "That's okay. Food takes time.",
                "Time is the one thing you don't have.",
                "Your friends keep texting.",
                "They wouldn't understand.",
                "One more chapter. Then you can rest.",
                "...You said that last time."
            }
        },

        // ACT TWO: The Pressure ───────────────────────────────────────────────

        new()
        {
            title = "The Cracks",
            unlockLevel = 6,
            lines = new[]
            {
                "You've read this page four times.",
                "The words keep sliding off your eyes.",
                "When did it get dark outside?",
                "Your handwriting looks different.",
                "That's fine. It's just stress.",
                "Everyone feels like this before an exam.",
                "...Don't they?"
            }
        },

        new()
        {
            title = "2:47 AM",
            unlockLevel = 8,
            lines = new[]
            {
                "You don't remember deciding to stay up.",
                "You just... didn't stop.",
                "The lamp has been on for nineteen hours.",
                "There's a shadow in the corner of the room.",
                "It wasn't there before.",
                "It's probably nothing.",
                "Study. Focus. You're almost there."
            }
        },

        // ACT THREE: Unraveling ───────────────────────────────────────────────

        new()
        {
            title = "You Can't Remember",
            unlockLevel = 10,
            lines = new[]
            {
                "What chapter are you on?",
                "You've been staring at this page for... how long?",
                "There's a coffee cup on your desk.",
                "You don't remember making coffee.",
                "...Maybe someone else made it.",
                "There is no one else.",
                "The words don't mean anything anymore.",
                "But you keep reading.",
                "You must be learning.",
                "You're still here."
            }
        },

        new()
        {
            title = "Day Unknown",
            unlockLevel = 12,
            lines = new[]
            {
                "What day is it?",
                "It doesn't matter. The exam is on—",
                "...",
                "When is the exam?",
                "It doesn't matter. Finish this chapter first.",
                "The bookshelf looks taller than it used to.",
                "The books are in the wrong order.",
                "You didn't move them."
            }
        },

        // ACT FOUR: Dissociation ──────────────────────────────────────────────

        new()
        {
            title = "She Studies",
            unlockLevel = 14,
            lines = new[]
            {
                "You are studying.",
                "That is what you do.",
                "You are a student. Students study.",
                "Something is watching you from behind the textbook.",
                "Don't look.",
                "If you look, you'll lose your place.",
                "Keep reading.",
                "Keep reading."
            }
        },

        new()
        {
            title = "The Exam",
            unlockLevel = 16,
            lines = new[]
            {
                "The exam was two weeks ago.",
                "You didn't go.",
                "You were in the middle of a session.",
                "It's okay. You'll take it next semester.",
                "...",
                "There is no next semester.",
                "Study anyway.",
                "You need to be ready.",
                "Ready for what?"
            }
        },

        // ACT FIVE: The End ───────────────────────────────────────────────────

        new()
        {
            title = "The Room",
            unlockLevel = 18,
            lines = new[]
            {
                "The room has been changing.",
                "You've noticed.",
                "It's better not to think about it.",
                "The walls are closer than they were in September.",
                "The window shows the wrong view now.",
                "The timer keeps running even when you pause it.",
                "That's fine.",
                "Time spent studying is never wasted.",
                "...",
                "Is anyone going to come for you?"
            }
        },

        new()
        {
            title = "One More Session",
            unlockLevel = 20,
            lines = new[]
            {
                "One more session.",
                "Then you'll be done.",
                "One more session.",
                "Then you'll be done.",
                "One more—",
                "The lamp flickers.",
                "You don't notice.",
                "You open your textbook to page one.",
                "You have always been here.",
                "You will always be here.",
                "Start the timer."
            }
        },
    };

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    public StoryProgress GetCurrentProgress() => currentChapter switch
    {
        0 => StoryProgress.Prologue,
        1 or 2 => StoryProgress.Act1,
        3 or 4 => StoryProgress.Act2,
        5 or 6 => StoryProgress.Act3,
        7 or 8 => StoryProgress.Act4,
        _ => StoryProgress.Epilogue,
    };

    public void AdvanceChapter()
    {
        currentChapter++;
        StoryChapter[] all = GetAllChapters();
        currentQuest = currentChapter < all.Length ? all[currentChapter].title : "The End";
    }

    public enum StoryProgress
    {
        Prologue,
        Act1,
        Act2,
        Act3,
        Act4,
        Epilogue
    }
}
