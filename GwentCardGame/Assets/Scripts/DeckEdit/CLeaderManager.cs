using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CLeaderManager : MonoBehaviour, IPointerClickHandler
{
    public DeckController deckController;
    public GameObject LeaderPicker;

    float lastClick = 0f;
    float interval = 0.4f;

    public void OnPointerClick(PointerEventData eventData)
    {
        // Detect Card Right Click
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            //ShowBigCard();
            Debug.Log("Leader: Right Click !");
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if ((lastClick + interval) > Time.time)
            {
                // Double Click
                if (deckController.my_deck.Name == "__emptyname__")
                    return;

                LeaderPicker.SetActive(true);
                LeaderPicker.GetComponent<LeaderPicker>().ClearLeaderContent();
                LeaderPicker.GetComponent<LeaderPicker>().SetLeaderContent(GetComponent<CardStats>().faction);
            }
            else
            {
                // Single Click
            }
            lastClick = Time.time;
        }

    }
}
