using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDiscard : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;

    float lastClick = 0f;
    float interval = 0.4f;

    void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if ((lastClick + interval) > Time.time)
        {
            if (controller.battleState == BattleState.PLAYERTURN && controller.PlayerInfo.CanDiscard && gameObject.CompareTag("Player"))
            {
                Debug.Log("Discarding");
                controller.DiscardCard(GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._id, true);
                controller.PlayerInfo.CardsDiscarded++;

                // Conditions met, a card can be chosen
                if (controller.PlayerInfo.CardsDiscarded == 2)
                    controller.DeckCardPicker(true);
            }
        }
        else
        {
            //Debug.Log("Single Click! ");
            lastClick = Time.time;
        }
    }
}
