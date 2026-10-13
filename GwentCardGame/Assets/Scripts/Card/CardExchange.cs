using System.Collections;
using UnityEngine;

// Initial redraw: double-click (double-tap on mobile) a hand card to exchange it, up to 2 cards
public class CardExchange : MonoBehaviour
{
    [HideInInspector]
    public SceneController controller;
    public bool isExchangeable;

    private float firstClickTime = 0f;
    private float timeBetweenClick = 0.2f;
    private bool couroutineAllowed = true;
    private int clickCounter = 0;

    void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnCardClick()
    {
        // Check if the player can exchange cards (redraw two initially drawn cards)
        if (!controller.PlayerInfo.CanExchange || !gameObject.CompareTag("Player") || !isExchangeable)
            return;

        if (controller.PlayerInfo.CardsExchanged >= 2)
        {
            controller.FinishCardExchange();
            return;
        }

        if (Input.GetMouseButtonUp(0))
            clickCounter += 1;
        if (clickCounter == 1 && couroutineAllowed)
        {
            firstClickTime = Time.time;
            StartCoroutine(ExchangeDetection());
        }
    }

    private void Exchange()
    {
        controller.ExchangeCard(GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._id, true);
        controller.PlayerInfo.CardsExchanged++;
        if (controller.PlayerInfo.CardsExchanged >= 2)
            controller.FinishCardExchange();
    }

    // Couroutine Double Click Exchange Detection
    private IEnumerator ExchangeDetection()
    {
        couroutineAllowed = false;
        while (Time.time < firstClickTime + timeBetweenClick)
        {
            if (clickCounter == 2)
            {
                Exchange();
                break;
            }
            yield return new WaitForEndOfFrame();
        }
        clickCounter = 0;
        firstClickTime = 0f;
        couroutineAllowed = true;
    }
}
