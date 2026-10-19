using System.Collections;
using System.Collections.Generic;
using Gwent.Core;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // This is instansiated on every card creation ! 
    public class CardFace : MonoBehaviour
    {
        // Gameplay related
        private CardRef cardRef;
        // The card shown (its CardRef is on the card's Stats child)
        public Card Card { get { if (cardRef == null) cardRef = GetComponentInChildren<CardRef>(true); return cardRef.Card; } }
        // Display related
        public GameObject artworkImage;
        public GameObject natureImage;
        public GameObject strengthImage;
        public GameObject rowImage;
        public GameObject abilityImage;

        private bool shown;

        void Start()
        {
            EnsureShown();
        }

        // Censored artwork turned on or off: the picture's other version, at once
        void OnEnable()
        {
            CensoredArt.Changed += ShowPicture;
        }

        void OnDisable()
        {
            CensoredArt.Changed -= ShowPicture;
        }

        private void ShowPicture()
        {
            if (shown && Card._id != CardIds.Hidden)
                artworkImage.GetComponent<Image>().sprite = CensoredArt.BoardPicture(Card._id);
        }

        // Its art, strength and icons, at once if not done yet (a card object made this frame is only drawn from its Start:
        // a copy of it for a flight needs them now)
        public void EnsureShown()
        {
            if (!shown)
                OnCreation();
        }

        //----------------------------------------On card creation-----------------------------------------------//
        private void OnCreation()
        {
            shown = true;
            Card card = Card;

            // A card this side cannot see (the opponent's hand): its back
            if (card._id == CardIds.Hidden)
            {
                artworkImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Back/BACK");
                foreach (GameObject part in new[] { natureImage, rowImage, abilityImage })
                    part.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                strengthImage.GetComponent<TextMeshProUGUI>().text = "";
                return;
            }

            // Its picture (the censored one when the setting is on and there is one)
            artworkImage.GetComponent<Image>().sprite = CensoredArt.BoardPicture(card._id);

            if (card.faction == Faction.Special)
            {// Special Card:
                // Change to the corresponding special Icon
                natureImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Special/" + card._idstr);
                rowImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                abilityImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            }
            else
            {// Unit Card or Leader:
                if (card.ability == Ability.Leader)
                {
                    natureImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                    rowImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                    abilityImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                }
                else
                {
                    // Change the strength, strength text color and uniqueness image
                    strengthImage.GetComponent<TextMeshProUGUI>().text = card.strength.ToString();
                    if (card.unique)
                    {
                        natureImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Nature/unique");
                        strengthImage.GetComponent<TextMeshProUGUI>().color = Color.white;
                    }
                    else
                    {
                        natureImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Nature/not_unique");
                        strengthImage.GetComponent<TextMeshProUGUI>().color = new Color(0, 0, 0);
                    }
                    // Change row Image
                    rowImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Row/" + card.row.Code());
                    // Change the ability image
                    if (card.ability == Ability.None)
                    {
                        abilityImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                    }
                    else
                    {
                        if (card.ability == Ability.Scorch || card.ability.IsRowScorch())
                            abilityImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Ability/scorch");
                        else
                            abilityImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Ability/" + card.ability.Code());
                    }
                }
            }
        }
    }
}
