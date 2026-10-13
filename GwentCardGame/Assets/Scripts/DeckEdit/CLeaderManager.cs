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
        // Double-click (tap on mobile) opens the leader picker
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (Application.isMobilePlatform || (lastClick + interval) > Time.time)
                OpenLeaderPicker();
            lastClick = Time.time;
        }
    }

    private void OpenLeaderPicker()
    {
        if (deckController.my_deck.Name == Gwent.Rules.DeckRules.EmptyDeckName)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast("Create a deck first: type a name and press +");
            return;
        }

        LeaderPicker.SetActive(true);
        LeaderPicker.GetComponent<LeaderPicker>().ClearLeaderContent();
        LeaderPicker.GetComponent<LeaderPicker>().SetLeaderContent(GetComponent<CardStats>().faction);
    }
}
