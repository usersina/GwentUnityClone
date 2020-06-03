using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class RowClick : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;

    //public bool isHighlighted;

    void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
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
        GameObject parent_go = transform.parent.parent.gameObject;
        List<GameObject> highLighted_fields = parent_go.GetComponent<RowPicker>().highLightedRows;

        if (controller.battleState == BattleState.PLAYERTURN) // Just to be extra sure
        {
            if (controller.selectedCard != null)
            {
                if (highLighted_fields.Contains(gameObject))
                {
                    // Debug.Log("Yeap, it's me, place the card directly without checking any single other thing !!!!");
                    // Just be sure to update the lists
                    controller.DirectlyPlaceCard(controller.selectedCard, gameObject, true);
                }
            }
            else
                Debug.Log("Player has no card selected to play !");
        }
    }
}
