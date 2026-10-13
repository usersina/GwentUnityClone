using UnityEngine;
using UnityEngine.EventSystems;

// Eredin, Bringer of Death: double-click (double-tap on mobile) two hand cards to discard them, then pick one from the deck
public class CardDiscard : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public SceneController controller;

    private readonly DoubleClick doubleClick = new DoubleClick();

    void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !doubleClick.Register())
            return;

        if (controller.battleState == BattleState.PLAYERTURN && controller.PlayerInfo.CanDiscard && gameObject.CompareTag("Player"))
        {
            GameLog.Info("Discarding");
            controller.DiscardCard(GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._id, true);
            controller.PlayerInfo.CardsDiscarded++;

            // Conditions met, a card can be chosen
            if (controller.PlayerInfo.CardsDiscarded >= 2)
                controller.DeckCardPicker(true);
        }
    }
}
