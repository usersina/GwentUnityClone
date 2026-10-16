using System.Collections.Generic;
using System.IO;

namespace Gwent.Rules
{
    // A battle's pieces as bytes, to travel between two devices (a LAN battle: the host sends the guest each event as the
    // guest sees it) or to be kept on disk (a battle's setup, to go on with it). Card ids as 16-bit numbers (every card's
    // fits, CardIds.Hidden too), strengths and scores as 32-bit ones.
    public static class MatchWire
    {
        //----------------------------------------Events----------------------------------------//
        public static void Write(BinaryWriter w, MatchEvent e)
        {
            w.Write((byte)e.Kind);
            w.Write(e.Seat);
            w.Write((short)e.Card);
            w.Write((short)e.Other);
            w.Write((byte)e.Row);
            w.Write((byte)e.From);
            w.Write((byte)e.Why);
            w.Write(e.Flag);
            w.Write((byte)e.Winner);
            w.Write(e.PlayerTotal);
            w.Write(e.EnemyTotal);
            w.Write((short)e.PlayerKept);
            w.Write((short)e.EnemyKept);
            w.Write(e.PlayerDrew);
            w.Write(e.EnemyDrew);
            WriteOptionalIds(w, e.PlayerCards);
            WriteOptionalIds(w, e.EnemyCards);
            Write(w, e.State);
        }

        public static MatchEvent ReadEvent(BinaryReader r)
        {
            return new MatchEvent
            {
                Kind = (MatchEventKind)r.ReadByte(),
                Seat = r.ReadBoolean(),
                Card = r.ReadInt16(),
                Other = r.ReadInt16(),
                Row = (Row)r.ReadByte(),
                From = (PlaySource)r.ReadByte(),
                Why = (PassReason)r.ReadByte(),
                Flag = r.ReadBoolean(),
                Winner = (RoundWinner)r.ReadByte(),
                PlayerTotal = r.ReadInt32(),
                EnemyTotal = r.ReadInt32(),
                PlayerKept = r.ReadInt16(),
                EnemyKept = r.ReadInt16(),
                PlayerDrew = r.ReadBoolean(),
                EnemyDrew = r.ReadBoolean(),
                PlayerCards = ReadOptionalIds(r),
                EnemyCards = ReadOptionalIds(r),
                State = ReadState(r),
            };
        }

        //----------------------------------------The battle at one moment----------------------------------------//
        // A side's view of it (MatchState.For): the cards each hand holds that both saw are not carried
        public static void Write(BinaryWriter w, MatchState s)
        {
            Write(w, s.Player);
            Write(w, s.Enemy);
            WriteIds(w, s.Weather);
            w.Write(s.DoubleSpies);
            w.Write(s.RandomMedic);
            w.Write((byte)s.Round);
            w.Write((byte)s.Phase);
            w.Write(s.Turn);
            w.Write(s.Choice != null);
            if (s.Choice != null)
            {
                w.Write((byte)s.Choice.Kind);
                w.Write(s.Choice.Seat);
                WriteIds(w, s.Choice.Options);
                w.Write((byte)s.Choice.Left);
            }
            w.Write((byte)s.Result);
        }

        public static MatchState ReadState(BinaryReader r)
        {
            MatchState s = new MatchState
            {
                Player = ReadSide(r),
                Enemy = ReadSide(r),
                Weather = ReadIds(r),
                DoubleSpies = r.ReadBoolean(),
                RandomMedic = r.ReadBoolean(),
                Round = r.ReadByte(),
                Phase = (MatchPhase)r.ReadByte(),
                Turn = r.ReadBoolean(),
            };
            if (r.ReadBoolean())
                s.Choice = new Choice { Kind = (ChoiceKind)r.ReadByte(), Seat = r.ReadBoolean(), Options = ReadIds(r), Left = r.ReadByte() };
            s.Result = (RoundWinner)r.ReadByte();
            return s;
        }

        // Every field of a side (keep in step with PlayerState, as CopyFrom is)
        public static void Write(BinaryWriter w, PlayerState p)
        {
            w.Write((byte)p.Faction);
            w.Write((short)p.Leader);
            w.Write(p.canLeader);
            w.Write(p.leaderDisabled);
            foreach (List<int> ids in new[] { p.DeckList, p.HandList, p.DiscardList, p.SpCloseList, p.SpRangeList, p.SpSiegeList,
                p.CloseList, p.RangeList, p.SiegeList, p.WeatherPlayed, p.PendingSummons })
                WriteIds(w, ids);
            foreach (List<int> numbers in new[] { p.CloseStrengthL, p.RangeStrengthL, p.SiegeStrengthL, p.MCloseStrength, p.MRangeStrength,
                p.MSiegeStrength, p.RoundsScore })
                WriteNumbers(w, numbers);
            w.Write((byte)p.LeaderHornRows.Count);
            foreach (Row row in p.LeaderHornRows)
                w.Write((byte)row);
            w.Write(p.CanExchange);
            w.Write((byte)p.CardsExchanged);
            w.Write(p.CanDiscard);
            w.Write((byte)p.CardsDiscarded);
            w.Write(p.hasPassed);
            w.Write((byte)p.Lives);
            w.Write((byte)p.RoundsWon.Count);
            foreach (bool won in p.RoundsWon)
                w.Write(won);
        }

        public static PlayerState ReadSide(BinaryReader r)
        {
            PlayerState p = new PlayerState { Faction = (Faction)r.ReadByte(), Leader = r.ReadInt16(), canLeader = r.ReadBoolean(), leaderDisabled = r.ReadBoolean() };
            p.DeckList = ReadIds(r);
            p.HandList = ReadIds(r);
            p.DiscardList = ReadIds(r);
            p.SpCloseList = ReadIds(r);
            p.SpRangeList = ReadIds(r);
            p.SpSiegeList = ReadIds(r);
            p.CloseList = ReadIds(r);
            p.RangeList = ReadIds(r);
            p.SiegeList = ReadIds(r);
            p.WeatherPlayed = ReadIds(r);
            p.PendingSummons = ReadIds(r);
            p.CloseStrengthL = ReadNumbers(r);
            p.RangeStrengthL = ReadNumbers(r);
            p.SiegeStrengthL = ReadNumbers(r);
            p.MCloseStrength = ReadNumbers(r);
            p.MRangeStrength = ReadNumbers(r);
            p.MSiegeStrength = ReadNumbers(r);
            p.RoundsScore = ReadNumbers(r);
            int horns = r.ReadByte();
            p.LeaderHornRows = new List<Row>(horns);
            for (int i = 0; i < horns; i++)
                p.LeaderHornRows.Add((Row)r.ReadByte());
            p.CanExchange = r.ReadBoolean();
            p.CardsExchanged = r.ReadByte();
            p.CanDiscard = r.ReadBoolean();
            p.CardsDiscarded = r.ReadByte();
            p.hasPassed = r.ReadBoolean();
            p.Lives = r.ReadByte();
            int rounds = r.ReadByte();
            p.RoundsWon = new List<bool>(rounds);
            for (int i = 0; i < rounds; i++)
                p.RoundsWon.Add(r.ReadBoolean());
            return p;
        }

        //----------------------------------------Decks and setups----------------------------------------//
        public static void Write(BinaryWriter w, Deck deck)
        {
            w.Write((byte)deck.Faction);
            w.Write(deck.Name ?? "");
            w.Write((short)deck.Leader);
            WriteIds(w, deck.Cards);
        }

        public static Deck ReadDeck(BinaryReader r)
        {
            return new Deck { Faction = (Faction)r.ReadByte(), Name = r.ReadString(), Leader = r.ReadInt16(), Cards = ReadIds(r) };
        }

        public static void Write(BinaryWriter w, MatchSetup setup)
        {
            Write(w, setup.Player);
            Write(w, setup.Enemy);
            w.Write(setup.Seed);
        }

        public static MatchSetup ReadSetup(BinaryReader r)
        {
            return new MatchSetup { Player = ReadDeck(r), Enemy = ReadDeck(r), Seed = r.ReadInt32() };
        }

        //----------------------------------------Lists----------------------------------------//
        private static void WriteIds(BinaryWriter w, List<int> ids)
        {
            w.Write((short)ids.Count);
            foreach (int id in ids)
                w.Write((short)id);
        }

        private static List<int> ReadIds(BinaryReader r)
        {
            int count = r.ReadInt16();
            List<int> ids = new List<int>(count);
            for (int i = 0; i < count; i++)
                ids.Add(r.ReadInt16());
            return ids;
        }

        private static void WriteOptionalIds(BinaryWriter w, List<int> ids)
        {
            w.Write(ids != null);
            if (ids != null)
                WriteIds(w, ids);
        }

        private static List<int> ReadOptionalIds(BinaryReader r)
        {
            return r.ReadBoolean() ? ReadIds(r) : null;
        }

        private static void WriteNumbers(BinaryWriter w, List<int> numbers)
        {
            w.Write((short)numbers.Count);
            foreach (int number in numbers)
                w.Write(number);
        }

        private static List<int> ReadNumbers(BinaryReader r)
        {
            int count = r.ReadInt16();
            List<int> numbers = new List<int>(count);
            for (int i = 0; i < count; i++)
                numbers.Add(r.ReadInt32());
            return numbers;
        }

        // The bytes of something written (a message's body)
        public static byte[] Bytes(System.Action<BinaryWriter> write)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                    write(writer);
                return stream.ToArray();
            }
        }
    }
}
