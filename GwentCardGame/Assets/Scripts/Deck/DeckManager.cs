using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    public GameObject CardBackObject;

    private Transform deckBase;
    private TextMeshProUGUI deckCount;

    private void CacheReferences()
    {
        if (deckBase != null)
            return;
        deckBase = transform.Find("DeckBase");
        deckCount = transform.Find("DeckCount").Find("Number").GetComponent<TextMeshProUGUI>();
    }

    // Updates the card number and the stack of card backs (only adds/removes the difference)
    public void UIUpdateDeckBase(Sprite my_sprite, int cards_number)
    {
        CacheReferences();
        deckCount.text = cards_number.ToString();

        // Remove extra card backs from the top of the stack
        for (int i = deckBase.childCount - 1; i >= cards_number; i--)
        {
            GameObject card_back = deckBase.GetChild(i).gameObject;
            card_back.transform.SetParent(null, false);
            Destroy(card_back);
        }

        // Add missing ones with a slight offset each
        for (int i = deckBase.childCount; i < cards_number; i++)
        {
            GameObject instantiatedCardBack = Instantiate(CardBackObject, new Vector2(-0.5f * i, 0.5f * i), Quaternion.identity);
            instantiatedCardBack.name = "card_back_" + i;
            instantiatedCardBack.transform.SetParent(deckBase, false);
        }

        foreach (Transform card_back in deckBase)
        {
            Image image = card_back.GetComponent<Image>();
            if (image.sprite != my_sprite)
                image.sprite = my_sprite;
        }
    }
}
