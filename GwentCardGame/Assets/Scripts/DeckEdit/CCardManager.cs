using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gwent.Rules;

public class CCardManager : MonoBehaviour, IPointerClickHandler
{
    public List<Sprite> normalPatch;
    public List<Sprite> uniquePatch;

    private readonly DoubleClick doubleClick = new DoubleClick();

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
            if (UseSingleTapActions() || doubleClick.Register())
                EditDeckWithThisCard();
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
        CardStats stats = GetComponent<CardStats>();
        TextMeshProUGUI name_text = transform.Find("Name").GetComponent<TextMeshProUGUI>();
        Image patch = transform.Find("Patch").GetComponent<Image>();
        name_text.text = Regex.Replace(stats.name, @"[\d-]", string.Empty).Trim();

        // Neutral and special cards have no faction banner: their name is centered on a wider patch
        bool bannerless = stats.faction == Factions.Neutral || stats.faction == Factions.Special;
        if (bannerless)
        {
            RectTransform name_rect = name_text.rectTransform;
            name_rect.localPosition = new Vector2(0, name_rect.localPosition.y);
        }
        int variant = bannerless ? 1 : 0;
        patch.sprite = stats.unique ? uniquePatch[variant] : normalPatch[variant];
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
