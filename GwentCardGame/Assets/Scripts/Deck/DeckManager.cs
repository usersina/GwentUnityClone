using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public GameObject CardBackObject;

    // Updates the number of CardBacks on top of each other and the card number
    public void UIUpdateDeckBase(Sprite my_sprite, int cards_number) // Pass the sprite as a parameter
    {
        transform.Find("DeckCount").Find("Number").GetComponent<TextMeshProUGUI>().text = cards_number.ToString();
        //Debug.Log("Number of children card backs BEFORE deletion: " + transform.Find("DeckBase").childCount);
        //Clean any previous cards present;
        for (int i = 0; i < transform.Find("DeckBase").childCount; i++)
        {
            Destroy(transform.Find("DeckBase").GetChild(i).gameObject);
        }
        transform.Find("DeckBase").DetachChildren();
        //Debug.Log("Number of children card backs AFTER deletion: " + transform.Find("DeckBase").childCount);

        // Add the cards based on the number of card objects in deck with a slite offset
        float x = 0;
        float y = 0;
        for (int i = 0; i < cards_number; i++)
        {
            GameObject instantiatedCardBack = Instantiate(CardBackObject, new Vector2(0 + x, 0 + y), Quaternion.identity);
            instantiatedCardBack.GetComponent<Image>().sprite = my_sprite;
            instantiatedCardBack.name = "card_back_" + i.ToString();
            instantiatedCardBack.transform.SetParent(transform.Find("DeckBase").transform, false);
            x -= 0.5f;
            y += 0.5f;
        }
    }
}
