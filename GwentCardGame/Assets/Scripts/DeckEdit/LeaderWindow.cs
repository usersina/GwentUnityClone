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
            if (Application.isMobilePlatform || (lastClick + interval) > Time.time)
                SelectLeader();
            else
            {
                // Single Click
            }
            lastClick = Time.time;
        }
    }

    private void SelectLeader()
    {
        GameAudio.PlaySfx("deck_select");
        GameObject leaderPickerGo = transform.parent.parent.parent.gameObject;
        DeckController deckController = leaderPickerGo.GetComponent<LeaderPicker>().deckController;
        CollectionManager deckCollection = leaderPickerGo.GetComponent<LeaderPicker>().deckCollection;
        // Change the leader in deck
        deckController.my_deck.Leader = leaderId;

        // Save deck and update middle info
        deckCollection.OnDeckEdit();

        // Disable Leader Picker
        leaderPickerGo.SetActive(false);
    }
}
