using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    private void Start()
    {
        GameAudio.PlayMenuMusic();
    }

    public void PlayGame()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(1);
    }

    public void EditDeck()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(2);
    }

    public void QuitGame()
    {
        GameAudio.PlaySfx("ui_click");
        Debug.Log("Quitting...");
        Application.Quit();
    }
}
