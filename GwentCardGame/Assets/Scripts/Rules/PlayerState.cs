using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Everything one side owns during a battle, as lists of card ids.
    // Field names are serialized in Game.unity (SceneController.PlayerInfo/EnemyInfo), keep them stable.
    [Serializable]
    public class PlayerState
    {
        public string Faction;
        public int Leader;
        public bool canLeader = true;
        public bool leaderDisabled = false;               // Cancelled by the opponent's White Flame
        public List<int> DeckList = new List<int>();      // List of card_ids in deck
        public List<int> HandList = new List<int>();      // List of card_ids in hand
        public List<int> DiscardList = new List<int>();   // List of card_ids in discard

        public List<int> SpCloseList = new List<int>();   // List of card_ids in Special Close Field
        public List<int> SpRangeList = new List<int>();
        public List<int> SpSiegeList = new List<int>();

        // List of Unit Cards on the field (Length L)
        public List<int> CloseList = new List<int>();
        public List<int> RangeList = new List<int>();
        public List<int> SiegeList = new List<int>();

        // List of Original Strengths respectively (Length L)
        public List<int> CloseStrengthL = new List<int>();
        public List<int> RangeStrengthL = new List<int>();
        public List<int> SiegeStrengthL = new List<int>();

        // List of Modified Strengths respectively (Length L)
        public List<int> MCloseStrength = new List<int>();
        public List<int> MRangeStrength = new List<int>();
        public List<int> MSiegeStrength = new List<int>();

        public List<int> WeatherPlayed = new List<int>();     // Weather cards this side put on the board
        public List<string> LeaderHornRows = new List<string>(); // Rows whose horn comes from the leader (no physical card)
        public List<int> PendingSummons = new List<int>();    // Cards summoned when the board clears (a destroyed Cow's defenders)

        public bool CanExchange = true;
        public int CardsExchanged = 0;

        public bool CanDiscard = false;
        public int CardsDiscarded = 0;

        public bool hasPassed = false;
        public int Lives = 2;
        public List<int> RoundsScore = new List<int>(3);
        public List<bool> RoundsWon = new List<bool>(3);      // per round: did this side win it (a draw is nobody's)

        //----------------------------------------Row helpers----------------------------------------//
        public List<int> UnitRow(string row)
        {
            switch (row)
            {
                case Rows.Close: return CloseList;
                case Rows.Range: return RangeList;
                case Rows.Siege: return SiegeList;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public List<int> SpecialRow(string row)
        {
            switch (row)
            {
                case Rows.Close: return SpCloseList;
                case Rows.Range: return SpRangeList;
                case Rows.Siege: return SpSiegeList;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public List<int> BaseStrengths(string row)
        {
            switch (row)
            {
                case Rows.Close: return CloseStrengthL;
                case Rows.Range: return RangeStrengthL;
                case Rows.Siege: return SiegeStrengthL;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public List<int> ModifiedStrengths(string row)
        {
            switch (row)
            {
                case Rows.Close: return MCloseStrength;
                case Rows.Range: return MRangeStrength;
                case Rows.Siege: return MSiegeStrength;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public void SetStrengths(string row, List<int> base_strengths, List<int> modified_strengths)
        {
            switch (row)
            {
                case Rows.Close: CloseStrengthL = base_strengths; MCloseStrength = modified_strengths; break;
                case Rows.Range: RangeStrengthL = base_strengths; MRangeStrength = modified_strengths; break;
                case Rows.Siege: SiegeStrengthL = base_strengths; MSiegeStrength = modified_strengths; break;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public bool HasHorn(string row)
        {
            return SpecialRow(row).Contains(CardIds.CommanderHorn);
        }

        public int RowScore(string row)
        {
            int total = 0;
            foreach (int score in ModifiedStrengths(row))
                total += score;
            return total;
        }

        public int TotalScore()
        {
            return RowScore(Rows.Close) + RowScore(Rows.Range) + RowScore(Rows.Siege);
        }

        public int UnitsOnBoard()
        {
            return CloseList.Count + RangeList.Count + SiegeList.Count;
        }

        // Every physical card this side currently holds (used for card-conservation checks)
        public IEnumerable<int> AllCards()
        {
            foreach (List<int> list in new[] { DeckList, HandList, DiscardList, SpCloseList, SpRangeList, SpSiegeList, CloseList, RangeList, SiegeList, WeatherPlayed })
                foreach (int card_id in list)
                    yield return card_id;
        }

        // Deep copy of the lists and flags (used by the AI to simulate moves)
        public PlayerState Clone()
        {
            PlayerState copy = (PlayerState)MemberwiseClone();
            copy.DeckList = new List<int>(DeckList);
            copy.HandList = new List<int>(HandList);
            copy.DiscardList = new List<int>(DiscardList);
            copy.SpCloseList = new List<int>(SpCloseList);
            copy.SpRangeList = new List<int>(SpRangeList);
            copy.SpSiegeList = new List<int>(SpSiegeList);
            copy.CloseList = new List<int>(CloseList);
            copy.RangeList = new List<int>(RangeList);
            copy.SiegeList = new List<int>(SiegeList);
            copy.CloseStrengthL = new List<int>(CloseStrengthL);
            copy.RangeStrengthL = new List<int>(RangeStrengthL);
            copy.SiegeStrengthL = new List<int>(SiegeStrengthL);
            copy.MCloseStrength = new List<int>(MCloseStrength);
            copy.MRangeStrength = new List<int>(MRangeStrength);
            copy.MSiegeStrength = new List<int>(MSiegeStrength);
            copy.WeatherPlayed = new List<int>(WeatherPlayed);
            copy.LeaderHornRows = new List<string>(LeaderHornRows);
            copy.PendingSummons = new List<int>(PendingSummons);
            copy.RoundsScore = new List<int>(RoundsScore);
            copy.RoundsWon = new List<bool>(RoundsWon);
            return copy;
        }
    }
}
