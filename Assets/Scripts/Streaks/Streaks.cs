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
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");
        const string lastLoginKey = "LastLoginDate";
        const string streakKey = "DailyLoginStreak";

        if (PlayerPrefs.HasKey(lastLoginKey))
        {
            string lastLogin = PlayerPrefs.GetString(lastLoginKey);
            if (lastLogin == today) return;

            System.DateTime lastDate = System.DateTime.Parse(lastLogin);
            int daysDiff = (System.DateTime.Now.Date - lastDate.Date).Days;

            int streak = PlayerPrefs.GetInt(streakKey, 0);
            streak = daysDiff == 1 ? streak + 1 : 1;
            PlayerPrefs.SetInt(streakKey, streak);
        }
        else
        {
            PlayerPrefs.SetInt(streakKey, 1);
        }

        PlayerPrefs.SetString(lastLoginKey, today);
        PlayerPrefs.Save();
    }
    

}
