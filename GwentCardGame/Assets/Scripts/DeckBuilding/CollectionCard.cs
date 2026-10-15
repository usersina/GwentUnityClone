using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gwent.DeckBuilding
{
    public class CollectionCard : MonoBehaviour, IPointerClickHandler
    {
        public List<Sprite> normalPatch;
        public List<Sprite> uniquePatch;

        private readonly DoubleClick doubleClick = new DoubleClick();

        [HideInInspector]
        public DeckBuilder deckBuilder;

        // The "Cards in Deck" collection (the one tagged Player)
        [HideInInspector]
        public CardGrid deckCollection;

        private void Start()
        {
            deckBuilder = DeckBuilder.Instance;
            deckCollection = deckBuilder.deckCollection;

            UpdateCardGUI();
            // Touch controls have no right click: a finger held on the card shows it large until it lifts
            LongPress.On(gameObject, () => { ShowBigCard(); return true; }, () => Details().SetActive(false));
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
            return GameInput.IsTouch;
        }

        private void EditDeckWithThisCard()
        {
            Card cardStats = GetComponent<CardRef>().Card;
            if (transform.parent.parent.parent.CompareTag("Player"))
            {
                // Remove card from deck
                GameAudio.PlaySfx("deck_remove");
                GameLog.Event(LogCategory.Deck, "card_removed", "deck", deckBuilder.my_deck.Name, "card", cardStats.name);
                deckBuilder.my_deck.Cards.Remove(cardStats._id);
                deckCollection.OnDeckEdit();
            }
            else
            {
                AddCardToSelectedDeck(cardStats);
            }
        }

        private void AddCardToSelectedDeck(Card cardStats)
        {
            if (deckBuilder.my_deck.Name == Gwent.Rules.DeckRules.EmptyDeckName)
            {
                GameAudio.PlaySfx("invalid");
                UIOverlay.Toast("Create a deck first: type a name and press +");
                return;
            }

            int occurences = deckBuilder.my_deck.Cards.Where(x => x.Equals(cardStats._id)).Count();

            // Only the deck's faction, neutral and special cards
            Gwent.Rules.Card card = deckBuilder.Db.Get(cardStats._id);
            if (!card.IsSpecial && card.faction != Gwent.Rules.Faction.Neutral && card.faction != deckBuilder.my_deck.Faction)
            {
                GameAudio.PlaySfx("invalid");
                UIOverlay.Toast(card.DisplayName + " does not belong to this deck's faction");
                return;
            }

            // The copies of the card that exist in The Witcher 3 (one of a hero)
            if (occurences >= Gwent.Rules.DeckRules.MaxCopies(card))
            {
                GameAudio.PlaySfx("invalid");
                UIOverlay.Toast(Gwent.Rules.DeckRules.TooManyCopies(card));
                return;
            }

            // If Special occurences is less than 3, check total specials count
            if (cardStats.faction == Faction.Special)
            {
                int specialsOcc = deckBuilder.GetSpecialsOccurence(deckBuilder.my_deck);
                if (specialsOcc >= Gwent.Rules.DeckRules.MaxSpecials)
                {
                    GameAudio.PlaySfx("invalid");
                    UIOverlay.Toast("A deck can hold at most " + Gwent.Rules.DeckRules.MaxSpecials + " special cards");
                    return;
                }
            }

            // If all checks don't match, add the card to deck
            GameAudio.PlaySfx("deck_add");
            deckBuilder.my_deck.Cards.Add(cardStats._id);
            GameLog.Event(LogCategory.Deck, "card_added", "deck", deckBuilder.my_deck.Name, "card", cardStats.name);
            deckCollection.OnDeckEdit();
        }

        //------------------------------------------------------Functions---------------------------------------------------//
        private void UpdateCardGUI()
        {
            Card stats = GetComponent<CardRef>().Card;
            TextMeshProUGUI name_text = transform.Find("Name").GetComponent<TextMeshProUGUI>();
            Image patch = transform.Find("Patch").GetComponent<Image>();
            name_text.text = Card.DisplayNameOf(stats.name);

            // Neutral and special cards have no faction banner: their name is centered on a wider patch
            bool bannerless = stats.faction == Faction.Neutral || stats.faction == Faction.Special;
            if (bannerless)
            {
                RectTransform name_rect = name_text.rectTransform;
                name_rect.localPosition = new Vector2(0, name_rect.localPosition.y);
            }
            int variant = bannerless ? 1 : 0;
            patch.sprite = stats.unique ? uniquePatch[variant] : normalPatch[variant];
        }

        private GameObject Details()
        {
            return transform.parent.parent.parent.GetComponent<CardGrid>().cardDetails;
        }

        private void ShowBigCard()
        {
            GameObject cardDetails = Details();
            Card cardStats = GetComponent<CardRef>().Card;

            cardDetails.SetActive(true);
            CardArt.Show(cardDetails.transform.Find("BigImage").GetComponent<Image>(), cardStats._id);
            // A plain unit has no description
            GameObject effect = cardDetails.transform.Find("BigEffect").gameObject;
            effect.SetActive(DescriptionBox.Show(effect.GetComponent<Image>(), cardStats));
        }

    }
}
