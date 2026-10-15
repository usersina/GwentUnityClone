using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Everything one side owns during a battle, as lists of card ids.
    // Field names are serialized in Game.unity (BattleController.PlayerInfo/EnemyInfo), keep them stable.
    [Serializable]
    public class PlayerState
    {
        public Faction Faction;
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
        public List<Row> LeaderHornRows = new List<Row>(); // Rows whose horn comes from the leader (no physical card)
        public List<int> PendingSummons = new List<int>();    // Cards summoned when the board clears (a discarded Cow's defenders)
        public List<int> Transformed = new List<int>();       // Berserkers turned into bears: set aside until their bears leave

        public bool CanExchange = true;
        public int CardsExchanged = 0;

        public bool CanDiscard = false;
        public int CardsDiscarded = 0;

        public bool hasPassed = false;
        public int Lives = 2;
        public List<int> RoundsScore = new List<int>(3);
        public List<bool> RoundsWon = new List<bool>(3);      // per round: did this side win it (a draw is nobody's)

        //----------------------------------------Row helpers----------------------------------------//
        public List<int> UnitRow(Row row)
        {
            switch (row)
            {
                case Row.Close: return CloseList;
                case Row.Range: return RangeList;
                case Row.Siege: return SiegeList;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public List<int> SpecialRow(Row row)
        {
            switch (row)
            {
                case Row.Close: return SpCloseList;
                case Row.Range: return SpRangeList;
                case Row.Siege: return SpSiegeList;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public List<int> BaseStrengths(Row row)
        {
            switch (row)
            {
                case Row.Close: return CloseStrengthL;
                case Row.Range: return RangeStrengthL;
                case Row.Siege: return SiegeStrengthL;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public List<int> ModifiedStrengths(Row row)
        {
            switch (row)
            {
                case Row.Close: return MCloseStrength;
                case Row.Range: return MRangeStrength;
                case Row.Siege: return MSiegeStrength;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public void SetStrengths(Row row, List<int> base_strengths, List<int> modified_strengths)
        {
            switch (row)
            {
                case Row.Close: CloseStrengthL = base_strengths; MCloseStrength = modified_strengths; break;
                case Row.Range: RangeStrengthL = base_strengths; MRangeStrength = modified_strengths; break;
                case Row.Siege: SiegeStrengthL = base_strengths; MSiegeStrength = modified_strengths; break;
                default: throw new ArgumentException("Not a unit row: " + row);
            }
        }

        public bool HasHorn(Row row)
        {
            return SpecialRow(row).Contains(CardIds.CommanderHorn);
        }

        // A row's special slot holds one card: a Commander's Horn (a leader's too) or a Mardroeme
        public bool SlotTaken(Row row)
        {
            return SpecialRow(row).Count > 0;
        }

        public int RowScore(Row row)
        {
            int total = 0;
            foreach (int score in ModifiedStrengths(row))
                total += score;
            return total;
        }

        public int TotalScore()
        {
            return RowScore(Row.Close) + RowScore(Row.Range) + RowScore(Row.Siege);
        }

        public int UnitsOnBoard()
        {
            return CloseList.Count + RangeList.Count + SiegeList.Count;
        }

        // Every physical card this side currently holds (used for card-conservation checks)
        public IEnumerable<int> AllCards()
        {
            foreach (List<int> list in new[] { DeckList, HandList, DiscardList, SpCloseList, SpRangeList, SpSiegeList, CloseList, RangeList, SiegeList, WeatherPlayed, Transformed })
                foreach (int card_id in list)
                    yield return card_id;
        }

        // Makes this side a copy of another, reusing its own lists: the AI tries thousands of moves on the same copy
        // instead of making new lists for each (keep in step with the fields above; a test compares it with Clone)
        public PlayerState CopyFrom(PlayerState other)
        {
            Faction = other.Faction;
            Leader = other.Leader;
            canLeader = other.canLeader;
            leaderDisabled = other.leaderDisabled;
            Copy(other.DeckList, ref DeckList);
            Copy(other.HandList, ref HandList);
            Copy(other.DiscardList, ref DiscardList);
            Copy(other.SpCloseList, ref SpCloseList);
            Copy(other.SpRangeList, ref SpRangeList);
            Copy(other.SpSiegeList, ref SpSiegeList);
            Copy(other.CloseList, ref CloseList);
            Copy(other.RangeList, ref RangeList);
            Copy(other.SiegeList, ref SiegeList);
            Copy(other.CloseStrengthL, ref CloseStrengthL);
            Copy(other.RangeStrengthL, ref RangeStrengthL);
            Copy(other.SiegeStrengthL, ref SiegeStrengthL);
            Copy(other.MCloseStrength, ref MCloseStrength);
            Copy(other.MRangeStrength, ref MRangeStrength);
            Copy(other.MSiegeStrength, ref MSiegeStrength);
            Copy(other.WeatherPlayed, ref WeatherPlayed);
            Copy(other.LeaderHornRows, ref LeaderHornRows);
            Copy(other.PendingSummons, ref PendingSummons);
            Copy(other.Transformed, ref Transformed);
            CanExchange = other.CanExchange;
            CardsExchanged = other.CardsExchanged;
            CanDiscard = other.CanDiscard;
            CardsDiscarded = other.CardsDiscarded;
            hasPassed = other.hasPassed;
            Lives = other.Lives;
            Copy(other.RoundsScore, ref RoundsScore);
            Copy(other.RoundsWon, ref RoundsWon);
            return this;
        }

        private static void Copy<T>(List<T> from, ref List<T> to)
        {
            if (to == null || ReferenceEquals(to, from))
                to = new List<T>(from);
            else
            {
                to.Clear();
                to.AddRange(from);
            }
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
            copy.LeaderHornRows = new List<Row>(LeaderHornRows);
            copy.PendingSummons = new List<int>(PendingSummons);
            copy.Transformed = new List<int>(Transformed);
            copy.RoundsScore = new List<int>(RoundsScore);
            copy.RoundsWon = new List<bool>(RoundsWon);
            return copy;
        }
    }
}
