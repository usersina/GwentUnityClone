using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardExchange : MonoBehaviour
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;
    public bool isExchangeable;

    private float firstClickTime = 0f;
    private float timeBetweenClick = 0.2f;
    private bool couroutineAllowed = true;
    private int clickCounter = 0;


    void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
    }

    public void OnCardClick()
    {
        // Essential for the first draw
        // Check if the player can exchange cards (redraw two initially drawn cards)
        if (controller.PlayerInfo.CanExchange && gameObject.CompareTag("Player") && isExchangeable)
        {
            if (controller.PlayerInfo.CardsExchanged < 2)
            {
                if (Input.GetMouseButtonUp(0))
                    clickCounter += 1;
                if (clickCounter == 1 && couroutineAllowed)
                {
                    firstClickTime = Time.time;
                    StartCoroutine(ExchangeDetection());
                }
            }
            else
            {
                controller.PlayerInfo.CanExchange = false;
            }
        }
    }
    
    // Couroutine Double Click Exchange Detection
    private IEnumerator ExchangeDetection()
    {
        couroutineAllowed = false;
        while (Time.time < firstClickTime + timeBetweenClick)
        {
            if (clickCounter == 2)
            {
                //Debug.Log("Double Clicked: " + GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._idstr);
                if (gameObject.CompareTag("Player"))
                {
                    controller.ExchangeCard(GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>()._id, true);
                    controller.PlayerInfo.CardsExchanged++;
                    if (controller.PlayerInfo.CardsExchanged == 2)
                        controller.PlayerInfo.CanExchange = false;
                }
            }
            yield return new WaitForEndOfFrame();
        }
        clickCounter = 0;
        firstClickTime = 0f;
        couroutineAllowed = true;
    }
}
