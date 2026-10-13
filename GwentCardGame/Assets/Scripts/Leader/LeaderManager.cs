using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderManager : MonoBehaviour
{
    [HideInInspector]
    public SceneController controller;

    public int leaderId;
    public string type;

    private void Start()
    {
        controller = SceneController.Instance;
    }

    public void UISetPassive()
    {
        // Disable the button if passive ability
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

    // Reference: Activate button. The controller disables the button once the ability is really used.
    public void ActivateLeader()
    {
        if (controller.battleState == BattleState.PLAYERTURN)
            controller.ActivateLeader(leaderId, true);
    }
}
