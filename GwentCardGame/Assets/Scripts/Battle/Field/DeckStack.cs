using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A side's deck: its stack of card backs, and the count of cards left in a box just under it (the large card on the
    // right covers the opponent's count while a card is in focus, a moment)
    public class DeckStack : MonoBehaviour
    {
        public GameObject CardBackObject;

        private Transform deckBase;
        private TextMeshProUGUI deckCount;
        private GameObject shadow;

        private void CacheReferences()
        {
            if (deckBase != null)
                return;
            deckBase = transform.Find("DeckBase");
            deckCount = transform.Find("DeckCount").Find("Number").GetComponent<TextMeshProUGUI>();

            // Soft shadow under the stack, like the cards on the board
            Image shadow_image = UIKit.NewImage(transform, "Shadow", UITheme.Current.softShadow, new Color(0, 0, 0, 0.65f));
            RectTransform card = (RectTransform)transform.Find("DeckHolder");
            shadow_image.rectTransform.Place(card.anchorMin, card.anchoredPosition + new Vector2(5, -8), card.sizeDelta + new Vector2(30, 34), card.pivot);
            shadow_image.transform.SetAsFirstSibling();
            shadow = shadow_image.gameObject;
        }

        // Updates the card number and the stack of card backs (only adds/removes the difference)
        public void ShowStack(Sprite my_sprite, int cards_number)
        {
            CacheReferences();
            deckCount.text = cards_number.ToString();
            shadow.SetActive(cards_number > 0);

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
}
