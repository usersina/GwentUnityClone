using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;

    private void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
    }

    public void GoFirst()
    {
        Debug.Log("Going First");
        controller.TurnCallBack(false);
        gameObject.SetActive(false);
    }

    public void GoSecond()
    {
        // Start Enemy Turn
        Debug.Log("Going Second");
        controller.TurnCallBack(true);
        gameObject.SetActive(false);
    }
}
