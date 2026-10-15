using System.Collections.Generic;

namespace Gwent.Rules
{
    // All game cards indexed by id (each card id has unique stats)
    public class CardDatabase
    {
        private readonly Dictionary<int, Card> byId = new Dictionary<int, Card>();

        public List<Card> All { get; private set; }

        public CardDatabase(IEnumerable<Card> cards)
        {
            All = new List<Card>();
            foreach (Card card in cards)
            {
                if (card == null || byId.ContainsKey(card._id))
                    continue;
                byId.Add(card._id, card);
                All.Add(card);
            }
        }

        public bool Contains(int card_id)
        {
            return byId.ContainsKey(card_id);
        }

        public bool TryGet(int card_id, out Card card)
        {
            return byId.TryGetValue(card_id, out card);
        }

        // Unknown ids return an empty placeholder card instead of throwing
        public Card Get(int card_id)
        {
            Card card;
            if (byId.TryGetValue(card_id, out card))
                return card;
            return new Card { _id = card_id, _idstr = "", name = "", faction = Faction.Neutral, row = Row.Special, ability = Ability.None };
        }

        public List<Card> GetCards(IEnumerable<int> card_ids)
        {
            List<Card> returned_list = new List<Card>();
            foreach (int card_id in card_ids)
                returned_list.Add(Get(card_id));
            return returned_list;
        }
    }
}
