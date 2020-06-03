using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelManager : MonoBehaviour
{
    public GameObject container;
    public GameObject cardBtn;

    public void ShowToPlay(string field, List<int> discard_list, bool is_player_card)
    {
        Debug.Log("Showing Discarded !");
        for (int i = 0; i < container.transform.childCount; i++)
            Destroy(container.transform.GetChild(i).gameObject);
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        container.transform.DetachChildren();

        for (int i = 0; i < discard_list.Count; i++)
        {
            GameObject instantiatedCardbtn = Instantiate(cardBtn);
            instantiatedCardbtn.GetComponent<CardBtn>().cardId = discard_list[i];
            instantiatedCardbtn.GetComponent<CardBtn>().type = "play";
            instantiatedCardbtn.GetComponent<CardBtn>().field = field;
            instantiatedCardbtn.GetComponent<CardBtn>().isPlayerCard = is_player_card;
            instantiatedCardbtn.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/List/591x380/" + discard_list[i]);
            instantiatedCardbtn.transform.SetParent(container.transform, false);
        }

        ResizeContainer(discard_list.Count);
    }

    public void ShowToDraw(string field, List<int> deck_list, bool is_player_card)
    {
        // Show the deck then shuffle after picking
        Debug.Log("Choose a card from your deck !");
        for (int i = 0; i < container.transform.childCount; i++)
            Destroy(container.transform.GetChild(i).gameObject);
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        container.transform.DetachChildren();

        for (int i = 0; i < deck_list.Count; i++)
        {
            GameObject instantiatedCardbtn = Instantiate(cardBtn);
            instantiatedCardbtn.GetComponent<CardBtn>().cardId = deck_list[i];
            instantiatedCardbtn.GetComponent<CardBtn>().type = "draw";
            instantiatedCardbtn.GetComponent<CardBtn>().field = field;
            instantiatedCardbtn.GetComponent<CardBtn>().isPlayerCard = is_player_card;
            instantiatedCardbtn.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/List/591x380/" + deck_list[i]);
            instantiatedCardbtn.transform.SetParent(container.transform, false);
        }

        ResizeContainer(deck_list.Count);
    }

    public void ShowToView(List<int> hand_list, bool is_player_card)
    {
        Debug.Log("Showing opponent's hand!");
        for (int i = 0; i < container.transform.childCount; i++)
            Destroy(container.transform.GetChild(i).gameObject);
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        container.transform.DetachChildren();

        for (int i = 0; i < hand_list.Count; i++)
        {
            GameObject instantiatedCardbtn = Instantiate(cardBtn);
            instantiatedCardbtn.GetComponent<CardBtn>().cardId = hand_list[i];
            instantiatedCardbtn.GetComponent<CardBtn>().type = "view";
            instantiatedCardbtn.GetComponent<CardBtn>().isPlayerCard = is_player_card;
            instantiatedCardbtn.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/List/591x380/" + hand_list[i]);
            instantiatedCardbtn.transform.SetParent(container.transform, false);
        }

        ResizeContainer(hand_list.Count);
    }

    public void ResizeContainer(int cardsNum)
    {
        RectTransform containerTr = container.GetComponent<RectTransform>();
        cardsNum -= 3;                                           // 3 Cards already have space
        float width = (cardsNum * 380) + (cardsNum - 1) * 25;   // Card width: 380, Spacing: 25
        containerTr.offsetMin = new Vector2(0, 0);
        containerTr.offsetMax = new Vector2(width, 0);
    }
}
