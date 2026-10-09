using UnityEngine;

// Resources/MoodIcons: which of ronnie's icon sprites (Assets/ART/UI/Icons.png) shows over the head for each mood.
[CreateAssetMenu(menuName = "Study Sim/Mood Icons", fileName = "MoodIcons")]
public class MoodIcons : ScriptableObject
{
    public Sprite happy, sad, stressed;

    public Sprite For(Mood mood) => mood == Mood.Happy ? happy : mood == Mood.Sad ? sad : mood == Mood.Stressed ? stressed : null;
}
