using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Gwent.Rules;

public class RowClick : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public BattleController controller;

    //public bool isHighlighted;

    void Start()
    {
        controller = BattleController.Instance;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) // Detect a left click
        {
            OnRowClick();
        }
    }

    public void OnRowClick()
    {
        // Checking for the turn is un-needed, already checked when highlighting the field
        // This is the parent of the parent of the gameobject of this script
        // Basically the field gameObject (either PlayerField or EnemyField)
        if (controller == null)
            controller = BattleController.Instance;
        GameObject parent_go = transform.parent.parent.gameObject;
        List<GameObject> highLighted_fields = parent_go.GetComponent<RowPicker>().highLightedRows;

        if (controller.battleState == BattleState.PlayerTurn) // Just to be extra sure
        {
            // The chosen leader: a row it lights uses it
            if (controller.LeaderSelected)
            {
                if (!controller.TryUseLeaderOn(gameObject))
                    GameAudio.PlaySfx("invalid");
                return;
            }
            if (controller.IsSelectedCardPlayable())
            {
                GameObject targetField = GetPlayableTargetField(highLighted_fields);
                if (targetField != null)
                {
                    // Just be sure to update the lists
                    controller.DirectlyPlaceCard(controller.selectedCard, targetField, true);
                }
                else
                {
                    // Not a place for this card: say so instead of doing nothing
                    GameAudio.PlaySfx("invalid");
                    CardStats selected = controller.selectedCard.GetComponent<CardFace>().cardStats.GetComponent<CardStats>();
                    if (selected.ability == Abilities.Decoy)
                        UIOverlay.Toast("Decoy: click one of your units to take it back to your hand");
                    else if (selected.row == Rows.Weather || selected._id == CardIds.Scorch)
                        UIOverlay.Toast("Weather and Scorch go on the weather board");
                }
            }
            else
                GameLog.Info("Player has no card selected to play !");
        }
    }

    private GameObject GetPlayableTargetField(List<GameObject> highLightedFields)
    {
        CardStats selectedStats = controller.selectedCard.GetComponent<CardFace>().cardStats.GetComponent<CardStats>();
        if (selectedStats.faction == Factions.Special)
        {
            // Weather and scorch only go on the weather board
            if (selectedStats.row == Rows.Special)
            {
                if (highLightedFields.Contains(gameObject))
                    return gameObject;

                Transform specialRow = transform.parent.Find("Special");
                if (specialRow != null && highLightedFields.Contains(specialRow.gameObject))
                    return specialRow.gameObject;
            }

            return null;
        }

        if (highLightedFields.Contains(gameObject))
            return gameObject;

        return null;
    }
}
