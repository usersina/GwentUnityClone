using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CCardManager : MonoBehaviour, IPointerClickHandler
{
    public List<Sprite> normalPatch;
    public List<Sprite> uniquePatch;

    float lastClick = 0f;
    float interval = 0.4f;

    [HideInInspector]
    public GameObject deckControllerGO;
    [HideInInspector]
    public DeckController deckController;

    [HideInInspector]
    public GameObject deckCollectionGO;
    [HideInInspector]
    public CollectionManager deckCollection;

    private void Start()
    {
        deckControllerGO = GameObject.Find("DeckController");
        deckController = deckControllerGO.GetComponent<DeckController>();

        deckCollectionGO = GameObject.Find("DeckCollection");
        deckCollection = deckCollectionGO.GetComponent<CollectionManager>();

        UpdateCardGUI();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Detect Card Right Click
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ShowBigCard();
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if ((lastClick + interval) > Time.time)
            {
                // Double Click
                CardStats cardStats = GetComponent<CardStats>();
                if (transform.parent.parent.parent.CompareTag("Player"))
                {
                    // Remove card from deck
                    Debug.Log("Removing card from deck: " + deckController.my_deck.Name);
                    deckController.my_deck.Cards.Remove(cardStats._id);
                    deckCollection.OnDeckEdit();
                }
                else
                {
                    // Add card to selected deck if exists
                    if (deckController.my_deck.Name == "__emptyname__")
                        Debug.Log("No selected deck to add card to !");
                    else
                    {
                        int occurences = deckController.my_deck.Cards.Where(x => x.Equals(cardStats._id)).Count();

                        // Hero Card
                        if (cardStats.unique)
                        {
                            if (occurences > 0)
                            {
                                Debug.Log("Cannot add hero card to deck, already contains: " + occurences);
                                return;
                            }
                        }

                        // Normal Unit or Special Card
                        if (!cardStats.unique)
                        {
                            if (occurences >= 3)
                            {
                                Debug.Log("Cannot add card to deck, already contains: " + occurences);
                                return;
                            }
                        }

                        // If Special occurences is less than 3, check total specials count
                        if (cardStats.faction == "Special")
                        {
                            int specialsOcc = deckController.GetSpecialsOccurence(deckController.my_deck);
                            if (specialsOcc >= 10)
                            {
                                Debug.Log("Cannot add special card to deck, deck already contains " + specialsOcc + " specials.");
                                return;
                            }
                        }

                        // If all checks don't match, add the card to deck
                        deckController.my_deck.Cards.Add(cardStats._id);
                        deckCollection.OnDeckEdit();
                    }
                }
            }
            else
            {
                //Debug.Log("Single Click :)");
            }
            lastClick = Time.time;
        }
    }

    //------------------------------------------------------Functions---------------------------------------------------//
    private void UpdateCardGUI()
    {
        string myText = Regex.Replace(GetComponent<CardStats>().name, @"[\d-]", string.Empty).Trim();
        transform.Find("Name").GetComponent<TextMeshProUGUI>().text = myText;
        if (GetComponent<CardStats>().faction == "N" || GetComponent<CardStats>().faction == "Special")
        {
            float y = transform.Find("Name").GetComponent<RectTransform>().localPosition.y;
            transform.Find("Name").GetComponent<RectTransform>().localPosition = new Vector2(0, y);
            if (GetComponent<CardStats>().unique)
            {
                transform.Find("Patch").GetComponent<Image>().sprite = uniquePatch[1];
            }
            else
            {
                transform.Find("Patch").GetComponent<Image>().sprite = normalPatch[1];
            }
        }
        else
        {
            if (GetComponent<CardStats>().unique)
            {
                transform.Find("Patch").GetComponent<Image>().sprite = uniquePatch[0];
            }
            else
            {
                transform.Find("Patch").GetComponent<Image>().sprite = normalPatch[0];
            }
        }
    }

    private void ShowBigCard()
    {
        GameObject cardDetails = transform.parent.parent.parent.GetComponent<CollectionManager>().cardDetails;
        CardStats cardStats = GetComponent<CardStats>();

        cardDetails.SetActive(true);
        cardDetails.transform.Find("BigImage")
            .GetComponent<Image>()
            .sprite = Resources.Load<Sprite>("Cards/List/591x380/" + cardStats._id);

        if (cardStats.faction == "Special")
        {// Special Card
            cardDetails.transform.Find("BigEffect")
                .GetComponent<Image>()
                .sprite = Resources.Load<Sprite>("Cards/EffectBox/" + cardStats._idstr);
        }
        else
        {// Unit Card or Leader
            if (cardStats.ability == "leader") // Leader Card
                cardDetails.transform.Find("BigEffect")
                    .GetComponent<Image>()
                    .sprite = Resources.Load<Sprite>("Cards/EffectBox/Leader/" + cardStats._id);
            else // Unit Card
            {
                if (cardStats.unique)
                {
                    cardDetails.transform.Find("BigEffect")
                        .GetComponent<Image>()
                        .sprite = Resources.Load<Sprite>("Cards/EffectBox/hero");
                }

                if (cardStats.ability != "")
                    cardDetails.transform.Find("BigEffect")
                        .GetComponent<Image>()
                        .sprite = Resources.Load<Sprite>("Cards/EffectBox/" + cardStats.ability);
                else if (!cardStats.unique)
                    // No ability (normal unit)
                    if (cardStats.row == "close_range")
                        cardDetails.transform.Find("BigEffect")
                            .GetComponent<Image>()
                            .sprite = Resources.Load<Sprite>("Cards/EffectBox/agile");
                    else
                        cardDetails.transform.Find("BigEffect")
                            .GetComponent<Image>()
                            .sprite = Resources.Load<Sprite>("Cards/EffectBox/normal_unit");
            }
        }
    }

}
