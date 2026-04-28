using UnityEngine;

public class SaveManager : MonoBehaviour
{

    private SaveSlot currentSaveSlot;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public void SetCurrentSaveSlot(SaveSlot slot)
    {
        currentSaveSlot = slot;
    }

    public void InitializeSaveData()
    {

    }

    public void UpdateGameSaveData()
    {

    }

    public enum SaveSlot
    {
        slot1,
        slot2,
        slot3
    }

 
}
