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
    public DeckController deckController;

    // The "Cards in Deck" collection (the one tagged Player)
    [HideInInspector]
    public CollectionManager deckCollection;

    private void Start()
    {
        deckController = DeckController.Instance;
        deckCollection = deckController.deckCollection;

        UpdateCardGUI();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Detect Card Right Click
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            GameAudio.PlaySfx("card_select");
            ShowBigCard();
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (UseSingleTapActions() || (lastClick + interval) > Time.time)
                EditDeckWithThisCard();
            lastClick = Time.time;
        }
    }

    private bool UseSingleTapActions()
    {
        return Application.isMobilePlatform;
    }

    private void EditDeckWithThisCard()
    {
        CardStats cardStats = GetComponent<CardStats>();
        if (transform.parent.parent.parent.CompareTag("Player"))
        {
            // Remove card from deck
            GameAudio.PlaySfx("deck_remove");
            GameLog.Info("Removing card from deck: " + deckController.my_deck.Name);
            deckController.my_deck.Cards.Remove(cardStats._id);
            deckCollection.OnDeckEdit();
        }
        else
        {
            AddCardToSelectedDeck(cardStats);
        }
    }

    private void AddCardToSelectedDeck(CardStats cardStats)
    {
        if (deckController.my_deck.Name == Gwent.Rules.DeckRules.EmptyDeckName)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast("Create a deck first: type a name and press +");
            return;
        }

        int occurences = deckController.my_deck.Cards.Where(x => x.Equals(cardStats._id)).Count();

        // Hero Card
        if (cardStats.unique && occurences > 0)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast(cardStats.name + " is a hero: only one copy per deck");
            return;
        }

        // Normal Unit or Special Card
        if (!cardStats.unique && occurences >= Gwent.Rules.DeckRules.MaxCopies)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast("At most " + Gwent.Rules.DeckRules.MaxCopies + " copies of a card");
            return;
        }

        // If Special occurences is less than 3, check total specials count
        if (cardStats.faction == "Special")
        {
            int specialsOcc = deckController.GetSpecialsOccurence(deckController.my_deck);
            if (specialsOcc >= Gwent.Rules.DeckRules.MaxSpecials)
            {
                GameAudio.PlaySfx("invalid");
                UIOverlay.Toast("A deck can hold at most " + Gwent.Rules.DeckRules.MaxSpecials + " special cards");
                return;
            }
        }

        // If all checks don't match, add the card to deck
        GameAudio.PlaySfx("deck_add");
        deckController.my_deck.Cards.Add(cardStats._id);
        deckCollection.OnDeckEdit();
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
        cardDetails.transform.Find("BigImage").GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/591x380/" + cardStats._id);
        cardDetails.transform.Find("BigEffect").GetComponent<Image>().sprite = SpriteCache.Load(CardHover.EffectBoxPath(cardStats));
    }

}
