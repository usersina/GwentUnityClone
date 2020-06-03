using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderManager : MonoBehaviour
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;

    public int leaderId;
    public string type;

    private void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
    }

    public void UISetPassive()
    {
        // Disable the button if passive ability
        Debug.Log(type);
        if (type == "passive")
            transform.Find("Activate").GetComponent<Button>().interactable = false;
    }

    // RC: Red Color
    public void DisableButtonRC()
    {
        ColorBlock newColorBlock = transform.Find("Activate").GetComponent<Button>().colors;
        newColorBlock.disabledColor = new Color32(255, 134, 134, 160);
        transform.Find("Activate").GetComponent<Button>().colors = newColorBlock;
        transform.Find("Activate").GetComponent<Button>().interactable = false;
    }

    public void ActivateLeader()
    {
        if (controller.battleState == BattleState.PLAYERTURN)
        {
            // Disable the button
            DisableButtonRC();

            // Activate the leader's ability
            if (!controller.disableLeader && controller.PlayerInfo.canLeader)
                controller.ActivateLeader(leaderId, true);
            else
                Debug.Log("Leader ability disabled !");
        }
    }
}
