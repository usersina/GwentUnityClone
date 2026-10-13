using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [HideInInspector]
    public SceneController controller;

    private void Start()
    {
        controller = SceneController.Instance;
    }

    public void GoFirst()
    {
        if (controller == null)
            controller = SceneController.Instance;
        GameAudio.PlaySfx("ui_click");
        GameLog.Info("Going First");
        controller.TurnCallBack(false);
        gameObject.SetActive(false);
    }

    public void GoSecond()
    {
        if (controller == null)
            controller = SceneController.Instance;
        // Start Enemy Turn
        GameAudio.PlaySfx("ui_click");
        GameLog.Info("Going Second");
        controller.TurnCallBack(true);
        gameObject.SetActive(false);
    }
}
