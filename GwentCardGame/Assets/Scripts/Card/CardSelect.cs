using UnityEngine;
using UnityEngine.EventSystems;
using Gwent.Rules;

// Click on a card: select a hand card; on the board, the target of a selected decoy, the leader (chosen first, used
// by a second click), a discard pile to browse, or the row / weather board under it for a selected card
public class CardSelect : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public SceneController controller;

    // Needs to be false if placed on the field
    public bool isSelectable;

    void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            OnCardSelect();
    }

    public void OnCardSelect()
    {
        if (controller == null)
            controller = SceneController.Instance;

        if (isSelectable)
        {
            // A hand card while the opponent plays: not now
            if (controller.battleState == BattleState.ENEMYTURN && gameObject.CompareTag("Player"))
            {
                GameAudio.PlaySfx("invalid");
                return;
            }

            // If the turn is the player's and the card is his, hence the Tag
            if (controller.IsPlayerActionAllowed && gameObject.CompareTag("Player") && !controller.PlayerInfo.CanDiscard)
            {
                // Lightoff any previous fields
                controller.LightoffField(true);
                controller.LightoffField(false);
                GameAudio.PlaySfx("card_select");
                if (!CheckSameCard())
                {
                    CardHover hover = gameObject.GetComponent<CardHover>();
                    CardStats stats = gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
                    hover.isHoverable = false;
                    hover.TranslateUp();
                    hover.DestroyEffect();
                    controller.selectedCard = gameObject;
                    // The card goes to the showcase while its place is chosen (The Witcher 3)
                    CardShowcase.Select(gameObject, controller.GetCardStats(stats._id));
                    // Hightlights the appropriate field based on: Card Faction AND Card Row
                    controller.HighlightField(stats, true);
                }
            }
            return;
        }

        // A card in a discard pile: browse the pile (or use the leader that takes a unit from it)
        DiscardPileBrowser pile = GetComponentInParent<DiscardPileBrowser>();
        if (pile != null)
        {
            pile.Clicked();
            return;
        }

        if (controller.TryPlaySelectedDecoyOnCard(gameObject))
            return;

        if (TryActivateLeaderCard())
            return;

        // A card already on a row or the weather board, with a card selected (or the leader chosen): the click counts
        // for the row (or the weather board) under it, so full rows still take the selected card
        if ((controller.selectedCard != null || controller.LeaderSelected) && controller.battleState == BattleState.PLAYERTURN)
        {
            RowClick row = GetComponentInParent<RowClick>();
            WeatherManager weather = GetComponentInParent<WeatherManager>();
            if (row != null)
                row.OnRowClick();
            else if (weather != null)
                weather.OnWeatherClick();
            else
                GameAudio.PlaySfx("invalid");
        }
    }

    private bool TryActivateLeaderCard()
    {
        if (controller.battleState != BattleState.PLAYERTURN || !gameObject.CompareTag("Player"))
            return false;

        CardStats cardStats = gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        if (cardStats.ability != Abilities.Leader)
            return false;

        LeaderManager leaderManager = GetComponentInParent<LeaderManager>();
        if (leaderManager == null)
            return false;

        leaderManager.ActivateLeader();
        return true;
    }

    // Deselects the previously selected card; true if it was this very card (click again = deselect)
    private bool CheckSameCard()
    {
        // The chosen leader goes back: this card takes its place
        if (controller.LeaderSelected)
        {
            controller.PutBackSelectedCard(false);
            return false;
        }
        if (controller.selectedCard == null)
            return false;

        bool same_card = controller.selectedCard == gameObject;
        CardHover hover = controller.selectedCard.GetComponent<CardHover>();
        hover.isHoverable = true;
        hover.TranslateDown();
        hover.DestroyEffect();
        CardShowcase.Deselect();
        controller.selectedCard = null;
        return same_card;
    }
}
