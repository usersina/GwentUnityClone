using System;

namespace Gwent.Rules
{
    // One card as Resources/Cards/cards_db.json writes it: its faction, row and ability by their codes
    [Serializable]
    public class CardEntry
    {
        public string _idstr = null;
        public string name = null;
        public int _id = -1;
        public string faction = null;
        public bool unique = false;
        public int strength = 0;
        public string row = null;
        public string ability = null;
        public int copies = 1;

        // The card, its codes read (the database is the game's own: an unknown code is a mistake in it)
        public Card ToCard()
        {
            Faction card_faction;
            Row card_row;
            Ability card_ability;
            if (!Factions.TryParse(faction, out card_faction) || !Rows.TryParse(row, out card_row) || !Abilities.TryParse(ability, out card_ability))
                throw new FormatException("Card " + _id + " (" + name + "): unknown faction, row or ability (" + faction + ", " + row + ", " + ability + ")");
            return new Card
            {
                _idstr = _idstr, name = name, _id = _id, faction = card_faction, unique = unique, strength = strength,
                row = card_row, ability = card_ability, copies = copies,
            };
        }
    }
}
