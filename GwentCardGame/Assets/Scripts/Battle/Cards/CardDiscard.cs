using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gwent.Battle
{
    // Eredin, Destroyer of Worlds: double-click (double-tap on mobile, Enter with the keys) two hand cards to discard them,
    // then pick one from the deck
    public class CardDiscard : MonoBehaviour, IPointerClickHandler
    {
        [HideInInspector]
        public BattleController controller;

        private readonly DoubleClick doubleClick = new DoubleClick();

        void Start()
        {
            controller = BattleController.Instance;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && doubleClick.Register())
                Discard();
        }

        // Also Enter on the card the keys are on (BattleKeys)
        public void Discard()
        {
            if (controller == null)
                controller = BattleController.Instance;
            // After the second, the match has the player choose a card of the deck
            if (controller.battleState == BattleState.PlayerTurn && controller.PlayerInfo.CanDiscard && gameObject.CompareTag("Player"))
                controller.DiscardCard(GetComponent<CardFace>().Card._id, true, controller.HandIndexOf(gameObject));
        }
    }
}
