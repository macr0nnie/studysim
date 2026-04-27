using UnityEngine;

public class Streaks : MonoBehaviour
{
    //display the statistics information 
    //show the calendar
    public void NewYearSpecial()
    {
        //check system date
        //if january 1st, display a special message or animation
        if (System.DateTime.Now.Month == 1 && System.DateTime.Now.Day == 1)
        {
            Debug.Log("Happy New Year! Start your year with productive study habits!");
            //gamestate special event code event 
            //trigger animation or special effect here
        }   
    }
    public void OnDailyLogin()
    {
        //add to a streak of studying daily
        //great the user with a message 
        //check if game has been opened today
    
        if (PlayerPrefs.HasKey("DailyLoginStreak"))
        {
            int streak = PlayerPrefs.GetInt("DailyLoginStreak");
            streak++;
            PlayerPrefs.SetInt("DailyLoginStreak", streak);
        }
        else
        {
            PlayerPrefs.SetInt("DailyLoginStreak", 1);
        }
        
    }
    

}
