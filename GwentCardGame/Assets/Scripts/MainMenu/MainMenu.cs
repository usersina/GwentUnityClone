using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private static readonly float[] VolumeSteps = { 1f, 0.75f, 0.5f, 0.25f, 0f };

    private void Start()
    {
        GameAudio.PlayMenuMusic();
        CreateAudioSettings();
    }

    public void PlayGame()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(SceneNames.Game);
    }

    public void EditDeck()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(SceneNames.Deck);
    }

    public void QuitGame()
    {
        GameAudio.PlaySfx("ui_click");
        GameLog.Info("Quitting...");
        Application.Quit();
    }

    //----------------------------------------Audio settings----------------------------------------//
    // Two buttons in the bottom-right corner, each click lowers the volume one step (100% > 75% > ... > Off > 100%)
    private void CreateAudioSettings()
    {
        Button music = UIOverlay.CreateButton(UIOverlay.Root, "", new Vector2(1f, 0f), new Vector2(-190, 140), new Vector2(320, 70), null);
        Button sounds = UIOverlay.CreateButton(UIOverlay.Root, "", new Vector2(1f, 0f), new Vector2(-190, 60), new Vector2(320, 70), null);
        music.onClick.AddListener(() => { GameAudio.MusicVolume = NextStep(GameAudio.MusicVolume); UpdateLabels(music, sounds); });
        sounds.onClick.AddListener(() => { GameAudio.SfxVolume = NextStep(GameAudio.SfxVolume); GameAudio.PlaySfx("ui_click"); UpdateLabels(music, sounds); });
        UpdateLabels(music, sounds);
    }

    private static float NextStep(float volume)
    {
        int index = System.Array.FindIndex(VolumeSteps, step => Mathf.Approximately(step, volume));
        return VolumeSteps[(index + 1) % VolumeSteps.Length];
    }

    private static void UpdateLabels(Button music, Button sounds)
    {
        music.GetComponentInChildren<TextMeshProUGUI>().text = "Music: " + VolumeLabel(GameAudio.MusicVolume);
        sounds.GetComponentInChildren<TextMeshProUGUI>().text = "Sounds: " + VolumeLabel(GameAudio.SfxVolume);
    }

    private static string VolumeLabel(float volume)
    {
        return volume <= 0f ? "Off" : Mathf.RoundToInt(volume * 100) + "%";
    }
}
