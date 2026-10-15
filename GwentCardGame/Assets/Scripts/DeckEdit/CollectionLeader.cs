using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CollectionLeader : MonoBehaviour, IPointerClickHandler
{
    public DeckBuilder deckController;
    public GameObject LeaderPicker;

    private readonly DoubleClick doubleClick = new DoubleClick();

    public void OnPointerClick(PointerEventData eventData)
    {
        // Double-click (tap on mobile) opens the leader picker
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (GameInput.IsTouch || doubleClick.Register())
                OpenLeaderPicker();
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
