using UnityEngine;
using UnityEngine.EventSystems;

// Initial redraw: double-click (double-tap on mobile) a hand card to exchange it, up to 2 cards
public class CardExchange : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public SceneController controller;
    public bool isExchangeable;

    private readonly DoubleClick doubleClick = new DoubleClick();

    void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (controller == null)
            controller = SceneController.Instance;

        // Check if the player can exchange cards (redraw two initially drawn cards)
        if (!controller.PlayerInfo.CanExchange || !gameObject.CompareTag("Player") || !isExchangeable)
            return;

        if (controller.PlayerInfo.CardsExchanged >= 2)
        {
            controller.FinishCardExchange();
            return;
        }

        if (doubleClick.Register())
            Exchange();
    }

    private void Exchange()
    {
        controller.ExchangeCard(GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._id, true);
        controller.PlayerInfo.CardsExchanged++;
        if (controller.PlayerInfo.CardsExchanged >= 2)
            controller.FinishCardExchange();
    }
}
