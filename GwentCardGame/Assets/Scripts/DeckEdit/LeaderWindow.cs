using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class LeaderWindow : MonoBehaviour, IPointerClickHandler
{
    public int leaderId;

    float lastClick = 0f;
    float interval = 0.4f;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if ((lastClick + interval) > Time.time)
            {
                // Double Click
                GameObject leaderPickerGo = transform.parent.parent.parent.gameObject;
                DeckController deckController = leaderPickerGo.GetComponent<LeaderPicker>().deckController;
                CollectionManager deckCollection = leaderPickerGo.GetComponent<LeaderPicker>().deckCollection;
                // Change the leader in deck
                deckController.my_deck.Leader = leaderId;

                // Update middle info


                // Save deck
                deckCollection.OnDeckEdit();

                // Disable Leader Picker
                leaderPickerGo.SetActive(false);
            }
            else
            {
                // Single Click
            }
            lastClick = Time.time;
        }
    }
}
