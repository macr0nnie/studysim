using System.Security.Cryptography;
using UnityEditor.SearchService;
using UnityEngine;

public class Settings : MonoBehaviour
{
    //this script will handel the settings menu of the game. 

    //get gamemanger reference
    public GameObject settingsMenu;
    public GameObject mainMenu;
    void Start()
    {

    }
    // Update is called once per frame
    void Update()
    {

    }
    public void ExitGame()
    {
        //save the game 
        //display warning message that game will close
        Application.Quit();
    }
    public void ResumeGame()
    {
        // Resume the game
        Time.timeScale = 1f;
        // Hide the pause menu
        GameObject pauseMenu = GameObject.Find("PauseMenu");
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
    }
    public void SetVolume(float volume)
    {
        // Set the volume of the game
        AudioListener.volume = volume;
    }   
    public void OpenSettings(){
        // Open the settings menu
        settingsMenu.SetActive(true);
        mainMenu.SetActive(false);
    }
    public void BackToMainMenu()
    {
        settingsMenu.SetActive(false);
        mainMenu.SetActive(true);  
    }

    public void Start_Story_Mode(){

    }
    //select the save 
    public void SelectSave(int saveSlot)
    {
        //load the player prefs depending on the save selected
        switch (saveSlot)
        {
            case 1:
                //GameManager.Instance.LoadGameSave(1);
                break;
            case 2:
                //load the second save
                break;
            case 3:
                //load the third save
                break;
            default:
                Debug.LogError("Invalid save slot selected");
                break;
        }   
    
    }


}
