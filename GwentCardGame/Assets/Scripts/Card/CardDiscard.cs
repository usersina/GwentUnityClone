using UnityEngine;
using UnityEngine.EventSystems;

// Eredin, Bringer of Death: double-click (double-tap on mobile, Enter with the keys) two hand cards to discard them,
// then pick one from the deck
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
        if (eventData.button == PointerEventData.InputButton.Left && doubleClick.Register())
            Discard();
    }

    // Also Enter on the card the keys are on (BattleKeys)
    public void Discard()
    {
        if (controller == null)
            controller = SceneController.Instance;
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
