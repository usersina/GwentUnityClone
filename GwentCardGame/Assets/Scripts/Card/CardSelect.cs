using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardSelect : MonoBehaviour
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;

    // Needs to be false if placed on the field
    public bool isSelectable;

    void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
    }

    public void OnCardSelect()
    {
        if (isSelectable)
        {
            // If the turn is the player's and the card is his, hence the Tag
            if (controller.battleState == BattleState.PLAYERTURN && gameObject.CompareTag("Player") && !controller.PlayerInfo.CanDiscard)
            {
                // Lightoff any previous fields
                controller.LightoffField(true);
                controller.LightoffField(false);
                if (CheckSameCard())
                {
                    gameObject.GetComponent<CardHover>().isHoverable = true;
                    controller.selectedCard = null;
                    //Debug.Log("Player deselected a card !");
                }
                else
                {
                    gameObject.GetComponent<CardHover>().isHoverable = false;
                    controller.selectedCard = gameObject;
                    //Debug.Log("Player selected a new card!");
                    // Hightlights the appropriate field based on: Card Faction AND Card Row
                    controller.HighlightField(gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>() ,true);
                }
            }
        }

        // Related to card swapping (Decoy)
        if (controller.swapActivated && !isSelectable)
        {
            CardStats card_stats = gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
            if (card_stats.faction != "Special" && !card_stats.unique && !(card_stats.ability == "leader"))
            {
                gameObject.GetComponent<CardHover>().DestroyEffect();
                controller.Decoy(card_stats._id, gameObject.tag, transform.parent.parent.name);
            }
            else
                Debug.Log("Cannot Decoy !");
        }
    }

    // Check if the player selected a new card, if yes deselect it else select the appropriate card
    private bool CheckSameCard()
    {
        if (controller.selectedCard != null)
        {
            if (controller.selectedCard == gameObject)//Same card
            {
                //gameObject.GetComponent<CardHover>().isCardUp = true; // Deprecated
                controller.selectedCard = null;
                //sameCard = true;
                return true;
            }
            else
            {
                controller.selectedCard.gameObject.GetComponent<CardHover>().isHoverable = true;
                controller.selectedCard.gameObject.GetComponent<CardHover>().TranslateDown();
                controller.selectedCard = null;
                return false;
                //sameCard = false;
            }
        }
        return false;
    }
}
