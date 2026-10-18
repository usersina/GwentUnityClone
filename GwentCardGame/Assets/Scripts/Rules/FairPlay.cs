using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Gwent.Rules
{
    // Fair play between the two devices of a LAN battle, whose match the host's device runs (docs/lan-fair-play.md: what
    // it checks, what it cannot, and the way further, private decks).
    // The shuffle's seed is both devices' together: the host promises its part (a hash of it), the guest then gives its
    // own, and the host shows its part as the battle begins; neither can choose the seed, nor try seeds until one suits it.
    // Once the battle is over, the guest gets its record (both decks, the seed, every move) and plays it again on its own
    // (Check): the seed both made, its deck as it brought it, the host's as it showed it and lawful, every move lawful,
    // every event as its screen was shown it, and no move of its own but those it made.
    public static class FairPlay
    {
        public const int PartSize = 16;

        // A device's part of the seed: random, as no one can guess it
        public static byte[] NewPart()
        {
            byte[] part = new byte[PartSize];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(part);
            return part;
        }

        // What a part is promised by (its hash): it tells nothing of the part, and no other part has it
        public static byte[] Promise(byte[] part)
        {
            using (SHA256 sha = SHA256.Create())
                return sha.ComputeHash(part);
        }

        // Whether the part shown is the one promised
        public static bool Kept(byte[] promise, byte[] part)
        {
            return promise != null && part != null && part.Length == PartSize && Promise(part).SequenceEqual(promise);
        }

        // The seed from both parts (either part alone decides nothing of it)
        public static int Seed(byte[] host_part, byte[] guest_part)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToInt32(sha.ComputeHash(host_part.Concat(guest_part).ToArray()), 0) & int.MaxValue;
        }

        // A move the guest sent: the host had it, or perhaps not (Unsure: lost with the link, or no answer yet)
        public struct Sent
        {
            public MatchCommand Command;
            public bool Unsure;
        }

        // What is wrong with a battle's record, as the guest checks it (null: nothing). shown: the events its screen was
        // given; mine: the deck it brought; theirs: the host's as the host showed it (faction and leader); seed: the one
        // both made (null: not known, a battle taken up again); sent: the guest's moves (null: not known, the same)
        public static string Check(MatchSetup setup, IList<(bool seat, MatchCommand command)> commands, IList<MatchEvent> shown,
            Deck mine, Deck theirs, int? seed, IList<Sent> sent, CardDatabase db)
        {
            if (setup == null || setup.Player == null || setup.Enemy == null || commands == null)
                return "The battle's record is missing.";
            if (seed.HasValue && setup.Seed != seed.Value)
                return "The shuffle is not the one both devices made.";
            if (!SameDeck(setup.Enemy, mine))
                return "Your deck is not the one you brought.";
            if (setup.Player.Faction != theirs.Faction || setup.Player.Leader != theirs.Leader)
                return "The host's deck is not the one it showed.";
            if (DeckRules.Validate(setup.Player, db).Count > 0)
                return "The host's deck breaks the deck rules.";

            Match replayed;
            try
            {
                replayed = Match.Replay(setup, db, commands);
            }
            catch (InvalidOperationException)
            {
                return "A move in the battle breaks the rules.";
            }
            catch (Exception)
            {
                return "The battle's record cannot be played again.";
            }
            if (!replayed.IsOver)
                return "The battle's record stops before its end.";
            if (replayed.Events.Count != shown.Count)
                return "The battle you were shown is not the one its record plays.";
            for (int i = 0; i < shown.Count; i++)
                if (!MatchWire.Bytes(w => MatchWire.Write(w, replayed.Events[i].For(false))).SequenceEqual(MatchWire.Bytes(w => MatchWire.Write(w, shown[i]))))
                    return "The battle you were shown is not the one its record plays.";
            return sent != null ? CheckMine(commands, sent) : null;
        }

        // The guest's moves in the record are the ones it made, in order: a move it was unsure of may be missing (sent as
        // the link went, then made again once back: the record may hold either copy); out of time, it made none (the host's
        // clock); a give-up last may be the host's (claiming the win, the guest away)
        private static string CheckMine(IList<(bool seat, MatchCommand command)> commands, IList<Sent> sent)
        {
            List<MatchCommand> mine = new List<MatchCommand>();
            for (int i = 0; i < commands.Count; i++)
                if (!commands[i].seat && commands[i].command.Kind != CommandKind.TimeOut)
                    mine.Add(commands[i].command);
            if (Matches(mine, sent))
                return null;
            bool claimed = commands.Count > 0 && !commands[commands.Count - 1].seat && commands[commands.Count - 1].command.Kind == CommandKind.GiveUp;
            if (claimed && Matches(mine.Take(mine.Count - 1).ToList(), sent))
                return null;
            return mine.Count < sent.Count(move => !move.Unsure) ? "A move you made is missing from the battle." : "A move was made for you that you didn't make.";
        }

        // Whether the moves are the ones sent, the unsure ones taken or left out as fits (every way, not the first that
        // seems to: the same move sent twice, unsure then sure, is in the record once)
        private static bool Matches(List<MatchCommand> mine, IList<Sent> sent)
        {
            // can[j]: the moves so far match the first j sent
            bool[] can = new bool[sent.Count + 1];
            can[0] = true;
            for (int j = 1; j <= sent.Count; j++)
                can[j] = can[j - 1] && sent[j - 1].Unsure;
            foreach (MatchCommand move in mine)
            {
                bool[] next = new bool[sent.Count + 1];
                for (int j = 1; j <= sent.Count; j++)
                    next[j] = can[j - 1] && Same(sent[j - 1].Command, move) || next[j - 1] && sent[j - 1].Unsure;
                can = next;
            }
            return can[sent.Count];
        }

        private static bool Same(MatchCommand a, MatchCommand b)
        {
            return a.Kind == b.Kind && a.CardId == b.CardId && a.HandIndex == b.HandIndex && a.Row == b.Row && a.TargetId == b.TargetId && a.Flag == b.Flag;
        }

        private static bool SameDeck(Deck a, Deck b)
        {
            return a != null && b != null && a.Faction == b.Faction && a.Leader == b.Leader && a.Cards.OrderBy(id => id).SequenceEqual(b.Cards.OrderBy(id => id));
        }
    }
}
