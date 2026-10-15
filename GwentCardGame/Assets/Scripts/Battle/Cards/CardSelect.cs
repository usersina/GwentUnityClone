using Gwent.Core;
using Gwent.Rules;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gwent.Battle
{
    // Click on a card: select a hand card; on the board, the target of a selected decoy, the leader (chosen first, used
    // by a second click), a discard pile to browse, or the row / weather board under it for a selected card
    public class CardSelect : MonoBehaviour, IPointerClickHandler
    {
        [HideInInspector]
        public BattleController controller;

        // Needs to be false if placed on the field
        public bool isSelectable;

        void Start()
        {
            controller = BattleController.Instance;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                OnCardSelect();
        }

        public void OnCardSelect()
        {
            if (controller == null)
                controller = BattleController.Instance;

            if (isSelectable)
            {
                // A hand card while the opponent plays: not now
                if (controller.battleState == BattleState.EnemyTurn && gameObject.CompareTag("Player"))
                {
                    GameAudio.PlaySfx("invalid");
                    return;
                }

                // If the turn is the player's and the card is his, hence the Tag
                if (controller.IsPlayerActionAllowed && gameObject.CompareTag("Player") && !controller.PlayerInfo.CanDiscard)
                {
                    // Lightoff any previous fields
                    controller.LightOffField(true);
                    controller.LightOffField(false);
                    GameAudio.PlaySfx("card_select");
                    if (!CheckSameCard())
                    {
                        CardHover hover = gameObject.GetComponent<CardHover>();
                        Card stats = gameObject.GetComponent<CardFace>().Card;
                        hover.isHoverable = false;
                        hover.TranslateUp();
                        hover.DestroyEffect();
                        controller.selectedCard = gameObject;
                        // The card goes to the showcase while its place is chosen (The Witcher 3)
                        CardShowcase.Select(gameObject, controller.GetCard(stats._id));
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
            if ((controller.selectedCard != null || controller.LeaderSelected) && controller.battleState == BattleState.PlayerTurn)
            {
                RowClick row = GetComponentInParent<RowClick>();
                WeatherSlot weather = GetComponentInParent<WeatherSlot>();
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
            if (controller.battleState != BattleState.PlayerTurn || !gameObject.CompareTag("Player"))
                return false;

            Card cardStats = gameObject.GetComponent<CardFace>().Card;
            if (cardStats.ability != Abilities.Leader)
                return false;

            LeaderSlot leaderManager = GetComponentInParent<LeaderSlot>();
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
}
