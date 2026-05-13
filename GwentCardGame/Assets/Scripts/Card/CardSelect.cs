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
                    GameAudio.PlaySfx("card_select");
                    CardHover hover = gameObject.GetComponent<CardHover>();
                    hover.isHoverable = true;
                    hover.DestroyEffect();
                    controller.selectedCard = null;
                    //Debug.Log("Player deselected a card !");
                }
                else
                {
                    GameAudio.PlaySfx("card_select");
                    CardHover hover = gameObject.GetComponent<CardHover>();
                    hover.isHoverable = false;
                    hover.TranslateUp();
                    hover.ShowPreview();
                    controller.selectedCard = gameObject;
                    //Debug.Log("Player selected a new card!");
                    // Hightlights the appropriate field based on: Card Faction AND Card Row
                    controller.HighlightField(gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>() ,true);
                }
            }
        }

        if (!isSelectable && controller.TryPlaySelectedDecoyOnCard(gameObject))
            return;

        if (!isSelectable && controller.TryPlaySelectedCardOnBoard())
            return;

        // Related to the legacy two-step decoy flow.
        if (controller.swapActivated && !isSelectable)
        {
            CardStats card_stats = gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
            if (card_stats.faction != "Special" && !card_stats.unique && !(card_stats.ability == "leader"))
            {
                gameObject.GetComponent<CardHover>().DestroyEffect();
                controller.Decoy(card_stats._id, gameObject.tag, transform.parent.parent.name);
            }
            else
            {
                GameAudio.PlaySfx("invalid");
                Debug.Log("Cannot Decoy !");
            }
        }
        else if (!isSelectable)
        {
            TryActivateLeaderCard();
        }
    }

    private bool TryActivateLeaderCard()
    {
        if (controller.battleState != BattleState.PLAYERTURN || !gameObject.CompareTag("Player"))
            return false;

        CardStats cardStats = gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        if (cardStats.ability != "leader")
            return false;

        LeaderManager leaderManager = GetComponentInParent<LeaderManager>();
        if (leaderManager == null)
            return false;

        leaderManager.ActivateLeader();
        return true;
    }

    // Check if the player selected a new card, if yes deselect it else select the appropriate card
    private bool CheckSameCard()
    {
        if (controller.selectedCard != null)
        {
            if (controller.selectedCard == gameObject)//Same card
            {
                //gameObject.GetComponent<CardHover>().isCardUp = true; // Deprecated
                CardHover hover = gameObject.GetComponent<CardHover>();
                hover.isHoverable = true;
                hover.TranslateDown();
                hover.DestroyEffect();
                controller.selectedCard = null;
                //sameCard = true;
                return true;
            }
            else
            {
                CardHover hover = controller.selectedCard.gameObject.GetComponent<CardHover>();
                hover.isHoverable = true;
                hover.TranslateDown();
                hover.DestroyEffect();
                controller.selectedCard = null;
                return false;
                //sameCard = false;
            }
        }
        return false;
    }
}
