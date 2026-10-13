using UnityEngine;
using UnityEngine.EventSystems;

// Initial redraw: double-click (double-tap on mobile) a hand card to exchange it, up to 2 cards
public class CardExchange : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public SceneController controller;
    public bool isExchangeable;

    // A redraw rebuilds the hand under the pointer: the clicks right after it (the end of a triple or quadruple
    // click) must not start a double-click on the card that took its place and spend the second redraw
    private const float ClickCooldown = 0.35f;
    private static float lastExchangeTime = float.NegativeInfinity;

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

        if (Time.unscaledTime - lastExchangeTime < ClickCooldown)
            return;
        if (doubleClick.Register())
            Exchange();
    }

    private void Exchange()
    {
        lastExchangeTime = Time.unscaledTime;
        controller.ExchangeCard(GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._id, true);
        controller.PlayerInfo.CardsExchanged++;
        if (controller.PlayerInfo.CardsExchanged >= 2)
            controller.FinishCardExchange();
    }
}
