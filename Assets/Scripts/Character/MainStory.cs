
using UnityEngine;

public class MainStory : MonoBehaviour
{
    public string current_quest = "Prologue";
    public int current_chapter = 0;
    bool special_event_triggered = false;
    bool choice_made = false;

    public void AdvanceChapter()
    {
        current_chapter++;
        //trigger events based on chapter
    }
    public void Update()
    {
        //check the story progression and triggers
    }

    //prologue dialoge
    //chapter1 dialoge
    //chapter2 dialoge  
    //chapter3 dialoge
    //epilogue dialoge

    //special triggers + choice detection

    public StoryProgress GetCurrentStoryProgress()
    {
        switch (current_chapter)
        {
            case 0:
                return StoryProgress.Prologue;
            case 1:
                return StoryProgress.Chapter1;
            case 2:
                return StoryProgress.Chapter2;
            case 3:
                return StoryProgress.Chapter3;
            default:
                return StoryProgress.Epilogue;
        }
    }

    public void UpdateProgress()
    {
        switch (current_chapter)
        {
            case 0:
                if (choice_made)
                {
                    current_quest = "Where Am I";
                    current_chapter = 1;
                }
                else
                {
                    current_quest = "Good Student";
                }
                current_quest = "Prologue";
                break;
            case 1:
                current_quest = "Chapter1";
                break;
              
            case 2:
                current_quest = "Chapter2";
                break;
            case 3:
                current_quest = "Chapter3";
                break;
            default:
               current_quest = "Epilogue";
                break;
        }
        
    }

    public enum StoryProgress
    {
        Prologue,
        Chapter1,
        Chapter2,
        Chapter3,
        Epilogue
    }

 
}
