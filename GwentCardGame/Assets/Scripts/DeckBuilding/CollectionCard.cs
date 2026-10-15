using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
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
                RectTransform ghost = TakeOff();
                deckBuilder.my_deck.Cards.Remove(cardStats._id);
                deckCollection.OnDeckEdit();
                Land(ghost, FindObjectsByType<CardGrid>().First(grid => grid != deckCollection), cardStats._id, deckBuilder.destroyCancellationToken).Forget();
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
            Gwent.Rules.Card card = cardStats;
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

            // At most 10 special cards in a deck
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
            RectTransform ghost = TakeOff();
            deckBuilder.my_deck.Cards.Add(cardStats._id);
            GameLog.Event(LogCategory.Deck, "card_added", "deck", deckBuilder.my_deck.Name, "card", cardStats.name);
            deckCollection.OnDeckEdit();
            Land(ghost, deckCollection, cardStats._id, deckBuilder.destroyCancellationToken).Forget();
        }

        //----------------------------------------A card's flight----------------------------------------//
        // A card added or removed flies from where it was clicked to its place in the other grid (the deck, or back to the
        // collection). The edit makes both grids anew, this object with them: a copy of it flies, taken off first. Where
        // copies are left, the card stays (one fewer) and the copy leaves it; the last copy is the card itself leaving.
        private const float FlightSeconds = 0.35f;

        private RectTransform TakeOff()
        {
            RectTransform ghost = CardMotion.Copy(gameObject, CardMotion.ScreenRect((RectTransform)transform));
            Transform badge = ghost.Find(CardGrid.CountBadge);
            if (badge != null)
                Destroy(badge.gameObject); // one copy flies, not the count
            return ghost;
        }

        // The copy glides to the card's place in the grid, which counts it once it lands (CardGrid.Arriving): a card the
        // grid showed already stays, its count going up as the copy joins it; a card new there shows when it lands. A card
        // out of the grid's view (scrolled away, or not in the tab shown) is met at the view's edge, where the copy fades.
        private static async UniTaskVoid Land(RectTransform ghost, CardGrid grid, int card_id, CancellationToken ct)
        {
            grid.Arriving(card_id, 1);
            bool landed = false;
            try
            {
                GameObject target = grid.CardShown(card_id);
                bool in_view = grid.InView(target);
                Vector2 size = CardMotion.ScreenRect(ghost).size;
                await CardMotion.Glide(ghost, () =>
                {
                    if (target == null)
                        target = grid.CardShown(card_id); // made anew by another edit on the way
                    return grid.ArrivalRect(target, size);
                }, FlightSeconds, ct: ct);
                landed = true;
                if (in_view)
                    Destroy(ghost.gameObject);
                else
                    CardMotion.FadeOut(ghost, 0.15f, ct).Forget();
            }
            finally
            {
                if (grid != null)
                    grid.Arriving(card_id, -1);
                if (!landed && ghost != null)
                    Destroy(ghost.gameObject); // the deck builder closed on the way
            }
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
