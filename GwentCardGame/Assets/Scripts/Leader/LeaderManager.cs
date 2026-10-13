using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Gwent.Rules;

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

    private Button activateButton;

    private Button ActivateButton
    {
        get
        {
            if (activateButton == null)
                activateButton = transform.Find("Activate").GetComponent<Button>();
            return activateButton;
        }
    }

    public void UISetPassive()
    {
        // Disable the button if passive ability
        if (type == Rows.LeaderPassive)
            ActivateButton.interactable = false;
    }

    // Disabled in red: the opponent's leader cancels this one
    public void DisableButtonRC()
    {
        ColorBlock colors = ActivateButton.colors;
        colors.disabledColor = new Color32(255, 134, 134, 160);
        ActivateButton.colors = colors;
        ActivateButton.interactable = false;
    }

    // Reference: Activate button. The controller disables the button once the ability is really used.
    public void ActivateLeader()
    {
        if (controller.battleState == BattleState.PLAYERTURN)
            controller.ActivateLeader(leaderId, true);
    }
}
