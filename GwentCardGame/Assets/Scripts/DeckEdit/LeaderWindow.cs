using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class LeaderWindow : MonoBehaviour, IPointerClickHandler
{
    public int leaderId;

    private readonly DoubleClick doubleClick = new DoubleClick();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (GameInput.IsTouch || doubleClick.Register())
                SelectLeader();
        }
    }

    private void SelectLeader()
    {
        GameAudio.PlaySfx("deck_select");
        GameObject leaderPickerGo = transform.parent.parent.parent.gameObject;
        DeckBuilder deckBuilder = leaderPickerGo.GetComponent<LeaderPicker>().deckBuilder;
        CardGrid deckCollection = leaderPickerGo.GetComponent<LeaderPicker>().deckCollection;
        // Change the leader in deck
        deckBuilder.my_deck.Leader = leaderId;

        // Save deck and update middle info
        deckCollection.OnDeckEdit();

        // Disable Leader Picker
        leaderPickerGo.SetActive(false);
    }
}
