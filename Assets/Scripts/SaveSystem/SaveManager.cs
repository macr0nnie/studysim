using UnityEngine;

public class SaveManager : MonoBehaviour
{

    private saveslots currentSaveSlot;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    public void SetCurrentSaveSlot(saveslots slot)
    {
        currentSaveSlot = slot;
    }

    public void initialize_save_data()
    {
        
    }

    public void Update_Game_Save_Data()
    {
        
    }

    public enum saveslots
    {
        slot1,
        slot2,
        slot3
    }

 
}
