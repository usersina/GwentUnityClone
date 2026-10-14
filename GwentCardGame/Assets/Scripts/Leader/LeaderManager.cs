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
    private bool used;
    private bool cancelled;

    private Button ActivateButton
    {
        get
        {
            if (activateButton == null)
                activateButton = transform.Find("Activate").GetComponent<Button>();
            return activateButton;
        }
    }

    // The opponent's crown only shows the state of their ability: it is not a button for the player
    public Button Button { get { return ActivateButton; } }

    private bool IsPlayers
    {
        get
        {
            if (controller == null)
                controller = SceneController.Instance;
            return controller != null && transform.IsChildOf(controller.PlayerField.transform);
        }
    }

    private void Awake()
    {
        UITooltip tooltip = ActivateButton.gameObject.AddComponent<UITooltip>();
        tooltip.text = TooltipText;
    }

    public void UISetPassive()
    {
        if (!IsPlayers)
            ActivateButton.transition = Selectable.Transition.None; // no hover / press look
        // Disable the button if passive ability
        if (type == Rows.LeaderPassive)
            SetUnavailable(Color.white);
    }

    // The ability was used: the button greys out
    public void MarkUsed()
    {
        used = true;
        SetUnavailable(new Color(0.45f, 0.45f, 0.45f, 0.9f));
    }

    // Cancelled by the opponent's leader (Emhyr, The White Flame): the button turns red
    public void MarkCancelled()
    {
        cancelled = true;
        SetUnavailable(new Color32(255, 134, 134, 200));
    }

    private void SetUnavailable(Color tint)
    {
        Button button = ActivateButton;
        ColorBlock colors = button.colors;
        colors.disabledColor = type == Rows.LeaderPassive && !used && !cancelled ? new Color(0.78f, 0.78f, 0.78f, 1f) : tint;
        button.colors = colors;
        button.interactable = false;
        if (button.transition == Selectable.Transition.None && button.targetGraphic != null)
            button.targetGraphic.color = colors.disabledColor;
    }

    private string TooltipText()
    {
        string whose = IsPlayers ? "Leader ability" : "Opponent's leader ability";
        if (cancelled)
            return whose + ": cancelled by the other leader";
        if (type == Rows.LeaderPassive)
            return whose + ": passive, always in effect";
        if (used)
            return whose + ": used";
        return IsPlayers ? (GameInput.IsTouch ? "Leader ability: tap to use" : "Leader ability: click to use") : whose + ": not used yet";
    }

    // Reference: Activate button. The controller marks the button used once the ability really happened.
    public void ActivateLeader()
    {
        if (!IsPlayers)
            return;
        if (controller.battleState == BattleState.PLAYERTURN)
            controller.ActivateLeader(leaderId, true);
    }
}
