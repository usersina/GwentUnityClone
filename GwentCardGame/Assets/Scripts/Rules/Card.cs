using System;

// Pure game data and rules: no UnityEngine dependency so it can be unit tested in isolation.
namespace Gwent.Rules
{
    // Card structure (one entry of Resources/Cards/cards_db.json)
    [Serializable]
    public class Card
    {
        public string _idstr = null;
        public string name = null;
        public int _id = -1;
        public string faction = null;
        public bool unique = false;
        public int strength = 0;
        public string row = null;
        public string ability = null;
        public int copies = 1;      // how many copies The Witcher 3 has: the most a deck can hold (0: never in a deck)

        public bool IsSpecial { get { return faction == Factions.Special; } }
        public bool IsLeader { get { return ability == Abilities.Leader; } }
        public bool IsUnit { get { return !IsSpecial && !IsLeader; } }

        // The name as players know it: without the copy number some entries carry ("Nekker 3" is a Nekker)
        public string DisplayName { get { return DisplayNameOf(name); } }

        public static string DisplayNameOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            int end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1]))
                end--;
            return end < name.Length && end > 0 && name[end - 1] == ' ' ? name.Substring(0, end - 1) : name;
        }
    }
}
