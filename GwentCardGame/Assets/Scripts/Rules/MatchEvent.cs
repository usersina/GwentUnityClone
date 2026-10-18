using System.Collections.Generic;

namespace Gwent.Rules
{
    public enum MatchPhase : byte { Opening, Redraw, Turn, Choice, Over }

    // What a side has to choose before the battle goes on
    public enum ChoiceKind : byte
    {
        FirstPlayer,                // who goes first (the Scoia'tael perk)
        Revive,                     // a medic's unit from the discard pile, played (on a row: an agile one's)
        WeatherFromDeck,            // a weather leader's card, played
        RestoreFromDiscard,         // Eredin, Bringer of Death: a unit of the own discard pile to the hand
        RestoreFromOpponentDiscard, // Emhyr, the Relentless: one of the opponent's
        Discard,                    // Eredin, Destroyer of Worlds: two cards of the hand discarded (Left: how many more)...
        DeckPick,                   // ...then one card of the deck to the hand
        LookAtHand,                 // Emhyr, Emperor of Nilfgaard: three random cards of the opponent's hand, seen
    }

    public enum PlaySource : byte { Hand, Discard, Deck, OpponentDiscard }

    // Why a side passed; TimedOut: why a round ended (RoundEnded, Seat: who ran out of time) or a redraw (RedrawDone)
    public enum PassReason : byte { Chose, EmptyHand, NothingToPlay, TimedOut }

    public enum MatchEventKind : byte
    {
        Started,        // the decks shuffled, the hands dealt (a White Flame's cancel in the state)
        ChoiceAsked,    // Seat must choose (State.Choice)
        FirstPlayer,    // Seat goes first; Flag: by the Scoia'tael perk (else a coin toss)
        Redrawn,        // Seat redrew Card for Other (the deck's top card)
        RedrawDone,     // Seat keeps its hand
        RoundStarted,   // round State.Round, Seat starting
        TurnStarted,    // Seat's turn
        Passed,         // Seat passed (Why)
        Played,         // Seat played Card on Row, from From (a decoy: for Other); Flag: a medic choice follows
        Transformed,    // Berserkers met a Mardroeme and turned into their bears
        LeaderUsed,     // Seat used its leader (Card); Flag: a choice follows
        Picked,         // Seat took Card into its hand, from From
        Discarded,      // Seat discarded Card from its hand
        Skipped,        // Seat gave up its choice (or ended its look)
        RoundEnded,     // Winner, the totals; Flag: Nilfgaard won the tie
        BoardCleared,   // the round's cards to the discard piles: the units Monsters kept, the Northern Realms' draw
        Reinforced,     // Skellige's units back from the discard piles (PlayerCards, EnemyCards)
        Ended,          // Winner (Draw: nobody); Flag: Seat gave up
    }

    // A choice a side has to make
    public sealed class Choice
    {
        public ChoiceKind Kind;
        public bool Seat;
        public List<int> Options = new List<int>();
        public int Left;            // Discard: cards still to discard

        public Choice Copy()
        {
            return new Choice { Kind = Kind, Seat = Seat, Options = new List<int>(Options), Left = Left };
        }
    }

    // The battle at one moment, as one event left it: both sides, the weather, whose turn, what is to choose. The match's
    // own (Match) holds everything; a side sees it through For.
    public sealed class MatchState
    {
        public PlayerState Player;
        public PlayerState Enemy;
        public List<int> Weather;
        public bool DoubleSpies;
        public bool RandomMedic;
        public int Round;
        public MatchPhase Phase;
        public bool Turn;           // whose turn (or choice)
        public Choice Choice;       // what is to choose, if anything
        public RoundWinner Result;  // once over
        internal List<int> PlayerKnown, EnemyKnown; // cards each hand holds that both sides saw go into it (the match's own)

        // As that side sees it: itself as the Player (the bottom of its screen), the other as the Enemy. The opponent's
        // hand shows only the cards both sides saw go into it (a decoyed unit, a unit taken from a discard pile), the rest
        // hidden (CardIds.Hidden); its deck is hidden; the side's own deck shows what it holds but not in what order
        // (sorted: the next draws stay unknown). A choice the other side has to make shows no options.
        public MatchState For(bool seat)
        {
            bool flip = !seat;
            PlayerState me = (flip ? Enemy : Player).Clone();
            PlayerState them = (flip ? Player : Enemy).Clone();
            List<int> their_known = flip ? PlayerKnown : EnemyKnown;

            List<int> seen = their_known != null ? new List<int>(their_known) : new List<int>();
            for (int i = 0; i < them.HandList.Count; i++)
                if (!seen.Remove(them.HandList[i]))
                    them.HandList[i] = CardIds.Hidden;
            for (int i = 0; i < them.DeckList.Count; i++)
                them.DeckList[i] = CardIds.Hidden;
            me.DeckList.Sort();

            Choice choice = Choice?.Copy();
            if (choice != null)
            {
                choice.Seat = choice.Seat == seat;
                if (!choice.Seat)
                    choice.Options.Clear();
            }
            return new MatchState
            {
                Player = me,
                Enemy = them,
                Weather = new List<int>(Weather),
                DoubleSpies = DoubleSpies,
                RandomMedic = RandomMedic,
                Round = Round,
                Phase = Phase,
                Turn = Turn == seat,
                Choice = choice,
                Result = flip ? Flipped(Result) : Result,
            };
        }

        public static RoundWinner Flipped(RoundWinner winner)
        {
            return winner == RoundWinner.Player ? RoundWinner.Enemy : winner == RoundWinner.Enemy ? RoundWinner.Player : winner;
        }

        // Both sides and the weather as a board for the rules (the lists are the state's own)
        public Board ToBoard(CardDatabase db)
        {
            return new Board { Player = Player, Enemy = Enemy, Weather = Weather, DoubleSpies = DoubleSpies, RandomMedic = RandomMedic, Db = db };
        }
    }

    // One thing that happened in a battle, and the battle right after it (State). A screen shows the events one after
    // the other, each as the change from the state before.
    public sealed class MatchEvent
    {
        public MatchEventKind Kind;
        public bool Seat;               // whose: the Player side's (true) or the Enemy side's
        public int Card = -1;
        public int Other = -1;
        public Row Row;
        public PlaySource From;
        public PassReason Why;
        public bool Flag;
        public RoundWinner Winner;
        public int PlayerTotal, EnemyTotal;
        public int PlayerKept = -1, EnemyKept = -1;         // BoardCleared: the unit Monsters kept
        public bool PlayerDrew, EnemyDrew;                  // BoardCleared: the Northern Realms' extra card
        public List<int> PlayerCards, EnemyCards;           // Reinforced: the units Skellige brought back
        public MatchState State;

        // As that side sees it (MatchState.For): itself as the Player; what it may not see (the other side's redraws, the
        // card it took from its deck) hidden
        public MatchEvent For(bool seat)
        {
            bool flip = !seat;
            bool theirs = Seat != seat;
            MatchEvent seen = (MatchEvent)MemberwiseClone();
            seen.State = State.For(seat);
            seen.Seat = Seat == seat;
            if (flip)
            {
                seen.Winner = MatchState.Flipped(Winner);
                seen.PlayerTotal = EnemyTotal;
                seen.EnemyTotal = PlayerTotal;
                seen.PlayerKept = EnemyKept;
                seen.EnemyKept = PlayerKept;
                seen.PlayerDrew = EnemyDrew;
                seen.EnemyDrew = PlayerDrew;
                seen.PlayerCards = EnemyCards;
                seen.EnemyCards = PlayerCards;
            }
            if (theirs && (Kind == MatchEventKind.Redrawn || Kind == MatchEventKind.Picked && From == PlaySource.Deck))
            {
                seen.Card = CardIds.Hidden;
                if (Kind == MatchEventKind.Redrawn)
                    seen.Other = CardIds.Hidden;
            }
            return seen;
        }

        public override string ToString()
        {
            string side = Seat ? "player" : "enemy";
            switch (Kind)
            {
                case MatchEventKind.Played: return side + " played " + Card + " on " + Row + (Other >= 0 ? " for " + Other : "") + (From != PlaySource.Hand ? " from " + From : "");
                case MatchEventKind.Passed: return side + " passed (" + Why + ")";
                case MatchEventKind.RoundEnded: return "round ended: " + Winner + " " + PlayerTotal + "-" + EnemyTotal + (Why == PassReason.TimedOut ? " (" + side + " out of time)" : "");
                case MatchEventKind.Ended: return "ended: " + Winner + (Flag ? " (" + side + " gave up)" : "");
                default: return Kind + " " + side + (Card != -1 ? " " + Card : "");
            }
        }
    }

    // What a battle starts from: both decks and the seed of its random source. With the commands the sides gave (in
    // order), it is the whole battle: replayed, it comes out the same (Match.Replay).
    public sealed class MatchSetup
    {
        public Deck Player;
        public Deck Enemy;
        public int Seed;
    }
}
