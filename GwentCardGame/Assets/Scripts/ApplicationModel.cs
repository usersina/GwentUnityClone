using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ApplicationModel : MonoBehaviour
{
    static public string playerDeckPath;
    static public string playerImagePath;

    private void Start()
    {
        Debug.Log("Starting, selected deck: " + PlayerPrefs.GetString("playerDeckPath"));
        GetDeckPref();
    }

    public void GetDeckPref()
    {
        if (PlayerPrefs.HasKey("playerDeckPath"))
        {
            if (PlayerPrefs.GetString("playerDeckPath").EndsWith(".json"))
                playerDeckPath = PlayerPrefs.GetString("playerDeckPath");
            else
                playerDeckPath = null;
        }
        else
            playerDeckPath = null;
    }

}
