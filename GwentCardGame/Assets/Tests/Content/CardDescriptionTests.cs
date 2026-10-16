using System.Collections.Generic;
using System.Linq;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Gwent.Content.Tests
{
    // Every card has the description box The Witcher 3 gives it (CardDescription), with its icon
    public class CardDescriptionTests
    {
        // A special card, a leader, a unit with an ability, a hero, an agile unit or the Cow
        private static bool Described(Card card)
        {
            return card.faction == Faction.Special || card.ability != Ability.None || card.unique || card.row == Row.Agile
                || AbilityRules.AvengerOf(card._id) != -1;
        }

        [Test]
        public void EveryCardWithSomethingToSayHasADescription()
        {
            List<string> missing = CardLibrary.Get().All.Where(card => Described(card) && CardDescription.For(card) == null)
                .Select(card => card.name).ToList();
            Assert.IsEmpty(missing, "Cards with no description");
            foreach (Card card in CardLibrary.Get().All.Where(Described))
            {
                CardDescription description = CardDescription.For(card);
                Assert.IsFalse(string.IsNullOrWhiteSpace(description.Title), card.name + " has no title");
                Assert.IsFalse(string.IsNullOrWhiteSpace(description.Text), card.name + " has no text");
            }
        }

        [Test]
        public void APlainUnitHasNone()
        {
            List<string> described = CardLibrary.Get().All.Where(card => !Described(card) && CardDescription.For(card) != null)
                .Select(card => card.name).ToList();
            Assert.IsEmpty(described, "Plain units with a description (The Witcher 3 shows none)");
        }

        [Test]
        public void EveryIconIsThere()
        {
            foreach (Card card in CardLibrary.Get().All)
            {
                CardDescription description = CardDescription.For(card);
                if (description != null && description.Icon != null)
                    Assert.IsNotNull(Resources.Load<Sprite>("Cards/DescriptionIcon/" + description.Icon), card.name + "'s icon " + description.Icon);
            }
        }
    }
}
