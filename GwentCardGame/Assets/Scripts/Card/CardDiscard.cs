using UnityEngine;
using UnityEngine.EventSystems;

// Eredin, Bringer of Death: double-click (double-tap on mobile) two hand cards to discard them, then pick one from the deck
public class CardDiscard : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public SceneController controller;

    float lastClick = 0f;
    float interval = 0.4f;

    void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if ((lastClick + interval) <= Time.time)
        {
            lastClick = Time.time;
            return;
        }

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
