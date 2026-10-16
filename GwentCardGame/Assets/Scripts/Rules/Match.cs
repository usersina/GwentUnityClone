using System;
using System.Collections.Generic;
using System.Linq;

namespace Gwent.Rules
{
    // A whole battle, without a screen: its two sides, its own random source (seeded: MatchSetup), whose turn it is, what
    // is to choose. The sides act through commands (Apply), which it checks and refuses when they are not allowed (not
    // the side's turn, a card it does not hold, a row the card cannot go on); everything that follows on its own (a turn
    // ending, a side with nothing left to play passing, a round's end, the next round's opening) happens at once. Each
    // step is an event (Events) carrying the battle as it left it: a screen shows them one after the other, at its own
    // pace, each side seeing them its own way (MatchEvent.For). Who decides for a side (a player, the AI, the other device
    // of a LAN battle) is none of its business: it is asked through the events (TurnStarted, ChoiceAsked) and answers
    // with commands. Its setup and the commands taken (Commands) are the whole battle: replayed, it comes out the same.
    //
    // The Player side is the one playing alone, or the host of a LAN battle; the Enemy side the AI, or the guest.
    public sealed class Match
    {
        public readonly MatchSetup Setup;
        public readonly Board Board;
        public readonly List<MatchEvent> Events = new List<MatchEvent>();
        public readonly List<(bool seat, MatchCommand command)> Commands = new List<(bool, MatchCommand)>();

        public MatchPhase Phase { get; private set; }
        public bool Turn { get; private set; }          // whose turn (or choice)
        public Choice Choice { get; private set; }      // what is to choose, if anything
        public int Round { get; private set; }          // 1 to 3 once the first round started
        public RoundWinner Result { get; private set; } // once over

        private readonly CardDatabase db;
        private bool firstPlayer;
        private bool lastPasser;
        private readonly List<int> playerKnown = new List<int>(), enemyKnown = new List<int>();

        public Match(MatchSetup setup, CardDatabase db)
        {
            Setup = setup;
            this.db = db;
            Board = new Board
            {
                Player = new PlayerState(),
                Enemy = new PlayerState(),
                Weather = new List<int>(),
                Db = db,
                Rnd = new SeededRandom(setup.Seed),
            };
        }

        public bool IsOver { get { return Phase == MatchPhase.Over; } }

        // A battle played again from its setup and its commands (each taken as the first time)
        public static Match Replay(MatchSetup setup, CardDatabase db, IEnumerable<(bool seat, MatchCommand command)> commands)
        {
            Match match = new Match(setup, db);
            match.Start();
            foreach ((bool seat, MatchCommand command) in commands)
                if (!match.Apply(seat, command))
                    throw new InvalidOperationException("Replay: " + (seat ? "player" : "enemy") + " " + command + " was refused");
            return match;
        }

        // Whether the match waits for that side (its turn, its choice, its redraw)
        public bool Awaits(bool seat)
        {
            switch (Phase)
            {
                case MatchPhase.Opening:
                case MatchPhase.Choice:
                    return Choice != null && Choice.Seat == seat;
                case MatchPhase.Redraw:
                    return Board.Side(seat).CanExchange;
                case MatchPhase.Turn:
                    return Turn == seat;
                default:
                    return false;
            }
        }

        //----------------------------------------The opening----------------------------------------//
        // The decks shuffled and the hands dealt (Francesca, Daisy of the Valley: one card more); Emhyr, the White Flame
        // cancels the other leader (passive ones too: Daisy's card, Invader's random medics, Treacherous' spies) unless
        // both play him. Then who goes first: the Scoia'tael side chooses (when only one side is), else a coin toss; then
        // the redraw.
        public void Start()
        {
            if (Events.Count > 0)
                return;
            Deck player = Setup.Player, enemy = Setup.Enemy;
            bool white_flame = player.Leader != enemy.Leader
                && (player.Leader == LeaderIds.EmhyrTheWhiteFlame || enemy.Leader == LeaderIds.EmhyrTheWhiteFlame);
            foreach (bool seat in new[] { true, false })
            {
                Deck deck = seat ? player : enemy;
                PlayerState side = Board.Side(seat);
                side.Faction = deck.Faction;
                side.Leader = deck.Leader;
                side.DeckList = Rng.Shuffled(deck.Cards, Board.Rnd);
                RoundRules.Draw(side, deck.Leader == LeaderIds.FrancescaDaisyOfTheValley && !white_flame ? 11 : 10);
                side.leaderDisabled = white_flame && deck.Leader != LeaderIds.EmhyrTheWhiteFlame;
            }
            bool either(int leader) { return !white_flame && (player.Leader == leader || enemy.Leader == leader); }
            Board.DoubleSpies = either(LeaderIds.EredinTheTreacherous);
            Board.RandomMedic = either(LeaderIds.EmhyrInvaderOfTheNorth);
            Board.Recalculate();
            Emit(MatchEventKind.Started, true);

            bool player_scoiatael = player.Faction == Faction.Scoiatael, enemy_scoiatael = enemy.Faction == Faction.Scoiatael;
            if (player_scoiatael != enemy_scoiatael)
            {
                Ask(new Choice { Kind = ChoiceKind.FirstPlayer, Seat = player_scoiatael }, MatchPhase.Opening);
                return;
            }
            firstPlayer = Board.Rnd.Next(0, 2) != 0;
            BeginRedraw();
            Emit(MatchEventKind.FirstPlayer, firstPlayer);
        }

        private void BeginRedraw()
        {
            Phase = MatchPhase.Redraw;
            foreach (bool seat in new[] { true, false })
            {
                Board.Side(seat).CanExchange = true;
                Board.Side(seat).CardsExchanged = 0;
            }
        }

        //----------------------------------------Commands----------------------------------------//
        // Takes a side's command, and everything that follows from it; false (nothing changes) when it is not allowed now
        public bool Apply(bool seat, MatchCommand command)
        {
            if (IsOver || Events.Count == 0)
                return false;
            bool done;
            switch (command.Kind)
            {
                case CommandKind.GiveUp: done = GiveUp(seat); break;
                case CommandKind.ChooseFirst: done = ChooseFirst(seat, command.Flag); break;
                case CommandKind.Redraw: done = Redraw(seat, command.CardId, command.HandIndex); break;
                case CommandKind.EndRedraw: done = EndRedraw(seat); break;
                case CommandKind.Play: done = Play(seat, command); break;
                case CommandKind.Leader: done = UseLeader(seat, command.CardId); break;
                case CommandKind.Pass: done = Pass(seat); break;
                case CommandKind.Choose: done = Choose(seat, command.CardId, command.Row); break;
                case CommandKind.Discard: done = Discard(seat, command.CardId, command.HandIndex); break;
                case CommandKind.Skip: done = Skip(seat); break;
                default: done = false; break;
            }
            if (!done)
                return false;
            Commands.Add((seat, command));
            return true;
        }

        private bool GiveUp(bool seat)
        {
            Phase = MatchPhase.Over;
            Choice = null;
            Result = seat ? RoundWinner.Enemy : RoundWinner.Player;
            Emit(MatchEventKind.Ended, seat, e => { e.Winner = Result; e.Flag = true; });
            return true;
        }

        private bool ChooseFirst(bool seat, bool me_first)
        {
            if (Phase != MatchPhase.Opening || Choice == null || Choice.Seat != seat)
                return false;
            Choice = null;
            firstPlayer = me_first ? seat : !seat;
            BeginRedraw();
            Emit(MatchEventKind.FirstPlayer, firstPlayer, e => e.Flag = true);
            return true;
        }

        // The deck's top card takes the card's place in the hand, the card goes to the bottom of the deck (two at most)
        private bool Redraw(bool seat, int card_id, int hand_index)
        {
            PlayerState side = Board.Side(seat);
            if (Phase != MatchPhase.Redraw || !side.CanExchange || side.CardsExchanged >= 2)
                return false;
            int drawn = RoundRules.Redraw(side, card_id, hand_index);
            if (drawn == -1)
                return false;
            side.CardsExchanged++;
            Emit(MatchEventKind.Redrawn, seat, e => { e.Card = card_id; e.Other = drawn; });
            if (side.CardsExchanged >= 2)
                EndRedraw(seat);
            return true;
        }

        // The hand is kept; once both sides are done, the first round starts
        private bool EndRedraw(bool seat)
        {
            PlayerState side = Board.Side(seat);
            if (Phase != MatchPhase.Redraw || !side.CanExchange)
                return false;
            side.CanExchange = false;
            Emit(MatchEventKind.RedrawDone, seat);
            if (!Board.Player.CanExchange && !Board.Enemy.CanExchange)
                StartRound(firstPlayer);
            return true;
        }

        // A card from the hand: on the row given (an agile unit's; a horn's or a Mardroeme's, whose slot must be free),
        // weather and Scorch on the weather board, a decoy on one of the side's units (TargetId) to take it back
        private bool Play(bool seat, MatchCommand command)
        {
            if (Phase != MatchPhase.Turn || Turn != seat)
                return false;
            PlayerState side = Board.Side(seat);
            int index = HandIndex(side.HandList, command.CardId, command.HandIndex);
            if (index < 0)
                return false;
            Card card = db.Get(command.CardId);
            if (card.ability == Ability.Decoy)
                return PlayDecoy(seat, card, index, command.TargetId, command.Row);
            Row row;
            if (!PlayRow(side, card, command.Row, out row))
                return false;

            side.HandList.RemoveAt(index);
            PlayOutcome outcome = PlayRules.Resolve(Board, card, row, seat, manual_medic: true, transform: false);
            Played(seat, card._id, row, PlaySource.Hand, outcome);
            return true;
        }

        // Where a card from the hand (or a revived unit, or a leader's weather card) goes; false when it cannot go there
        private static bool PlayRow(PlayerState side, Card card, Row given, out Row row)
        {
            row = given;
            if (card.IsUnit)
            {
                if (card.row == Row.Agile)
                    return given == Row.Close || given == Row.Range;
                row = Rows.IsUnitRow(card.row) ? card.row : Row.Close;
                return true;
            }
            switch (card.row)
            {
                case Row.Special: // a Commander's Horn or a Mardroeme: one card per slot
                    return Rows.IsUnitRow(given) && !side.SlotTaken(given);
                case Row.Weather:
                case Row.OneTime:
                    row = Row.Weather;
                    return true;
                default:
                    return false;
            }
        }

        private bool PlayDecoy(bool seat, Card decoy, int index, int target_id, Row row)
        {
            PlayerState side = Board.Side(seat);
            if (target_id < 0 || !Rows.IsUnitRow(row) || !side.UnitRow(row).Contains(target_id) || !PlayRules.IsDecoyTarget(db.Get(target_id)))
                return false;
            side.HandList.RemoveAt(index);
            PlayRules.Decoy(Board, target_id, row, seat, decoy._id);
            Known(seat).Add(target_id); // both saw it go back
            Emit(MatchEventKind.Played, seat, e => { e.Card = decoy._id; e.Other = target_id; e.Row = row; });
            EndTurn(seat);
            return true;
        }

        // A play resolved: shown, then Berserkers meeting a Mardroeme turn, then a medic's choice (if it has a unit to
        // bring back), else the turn ends
        private void Played(bool seat, int card_id, Row row, PlaySource from, PlayOutcome outcome)
        {
            bool medic_choice = outcome == PlayOutcome.WaitingForMedicChoice && AbilityRules.MedicCandidates(Board.Side(seat).DiscardList, db).Count > 0;
            Emit(MatchEventKind.Played, seat, e => { e.Card = card_id; e.Row = row; e.From = from; e.Flag = medic_choice; });

            if (AbilityRules.CanTransform(Board.Player, db) || AbilityRules.CanTransform(Board.Enemy, db))
            {
                AbilityRules.Transform(Board.Player, db);
                AbilityRules.Transform(Board.Enemy, db);
                Board.Recalculate();
                Emit(MatchEventKind.Transformed, seat);
            }

            if (outcome == PlayOutcome.WaitingForMedicChoice)
            {
                List<int> units = AbilityRules.MedicCandidates(Board.Side(seat).DiscardList, db);
                if (units.Count > 0)
                {
                    Ask(new Choice { Kind = ChoiceKind.Revive, Seat = seat, Options = units }, MatchPhase.Choice);
                    return;
                }
            }
            EndTurn(seat);
        }

        // The leader's ability (as The Witcher 3: never one that would do nothing). A weather leader plays the card
        // given, or has the side choose; the abilities that take a card ask which (at random under Emhyr, Invader of the
        // North); the rest resolve at once.
        private bool UseLeader(bool seat, int weather_card_id)
        {
            PlayerState side = Board.Side(seat);
            int leader = side.Leader;
            if (Phase != MatchPhase.Turn || Turn != seat || !side.canLeader || side.leaderDisabled || db.Get(leader).row != Row.LeaderActive
                || LeaderRules.Unavailable(Board, leader, seat) != null)
                return false;

            Row? weather_row = LeaderIds.WeatherPickRow(leader);
            if (weather_row != null)
            {
                List<int> weather = LeaderRules.WeatherCardsFor(side, weather_row.Value, db);
                side.canLeader = false;
                if (weather.Contains(weather_card_id))
                {
                    Emit(MatchEventKind.LeaderUsed, seat, e => e.Card = leader);
                    PlayFromDeck(seat, weather_card_id);
                }
                else
                {
                    Emit(MatchEventKind.LeaderUsed, seat, e => { e.Card = leader; e.Flag = true; });
                    Ask(new Choice { Kind = ChoiceKind.WeatherFromDeck, Seat = seat, Options = weather }, MatchPhase.Choice);
                }
                return true;
            }

            if (LeaderRules.IsInstant(leader))
            {
                if (!LeaderRules.ApplyInstant(Board, leader, seat, out _))
                    return false;
                side.canLeader = false;
                Board.Recalculate();
                Emit(MatchEventKind.LeaderUsed, seat, e => e.Card = leader);
                EndTurn(seat);
                return true;
            }

            switch (leader)
            {
                case LeaderIds.EredinDestroyerOfWorlds:
                    side.canLeader = false;
                    side.CanDiscard = true;
                    side.CardsDiscarded = 0;
                    Emit(MatchEventKind.LeaderUsed, seat, e => { e.Card = leader; e.Flag = true; });
                    Ask(new Choice { Kind = ChoiceKind.Discard, Seat = seat, Left = 2 }, MatchPhase.Choice);
                    return true;
                case LeaderIds.EredinBringerOfDeath:
                case LeaderIds.EmhyrTheRelentless:
                    bool theirs = leader == LeaderIds.EmhyrTheRelentless;
                    List<int> units = AbilityRules.MedicCandidates((theirs ? Board.Opponent(seat) : side).DiscardList, db);
                    side.canLeader = false;
                    ChoiceKind kind = theirs ? ChoiceKind.RestoreFromOpponentDiscard : ChoiceKind.RestoreFromDiscard;
                    if (Board.RandomMedic)
                    {
                        Emit(MatchEventKind.LeaderUsed, seat, e => e.Card = leader);
                        TakeToHand(seat, kind, units[Board.Rnd.Next(units.Count)]);
                        EndTurn(seat);
                    }
                    else
                    {
                        Emit(MatchEventKind.LeaderUsed, seat, e => { e.Card = leader; e.Flag = true; });
                        Ask(new Choice { Kind = kind, Seat = seat, Options = units }, MatchPhase.Choice);
                    }
                    return true;
                case LeaderIds.EmhyrEmperorOfNilfgaard:
                    List<int> seen = Rng.Shuffled(Board.Opponent(seat).HandList, Board.Rnd).Take(3).ToList();
                    side.canLeader = false;
                    Emit(MatchEventKind.LeaderUsed, seat, e => { e.Card = leader; e.Flag = true; });
                    Ask(new Choice { Kind = ChoiceKind.LookAtHand, Seat = seat, Options = seen }, MatchPhase.Choice);
                    return true;
            }
            return false; // passive
        }

        // A weather leader's card, from the deck onto the weather board
        private void PlayFromDeck(bool seat, int card_id)
        {
            Board.Side(seat).DeckList.Remove(card_id);
            PlayOutcome outcome = PlayRules.Resolve(Board, db.Get(card_id), Row.Weather, seat, manual_medic: true, transform: false);
            Played(seat, card_id, Row.Weather, PlaySource.Deck, outcome);
        }

        private bool Pass(bool seat)
        {
            if (Phase != MatchPhase.Turn || Turn != seat)
                return false;
            Passed(seat, PassReason.Chose);
            EndTurn(seat);
            return true;
        }

        // The pending choice's card: a medic's unit (on the row given, for an agile one), a weather leader's card, a card
        // taken into the hand; for a look, the end of it
        private bool Choose(bool seat, int card_id, Row row)
        {
            if (Phase != MatchPhase.Choice || Choice == null || Choice.Seat != seat)
                return false;
            PlayerState side = Board.Side(seat);
            Choice choice = Choice;
            if (choice.Kind == ChoiceKind.LookAtHand)
            {
                Choice = null;
                Emit(MatchEventKind.Skipped, seat);
                EndTurn(seat);
                return true;
            }
            if (!choice.Options.Contains(card_id))
                return false;
            switch (choice.Kind)
            {
                case ChoiceKind.Revive:
                    Card card = db.Get(card_id);
                    if (!side.DiscardList.Contains(card_id) || !PlayRow(side, card, row, out row))
                        return false;
                    Choice = null;
                    Phase = MatchPhase.Turn;
                    side.DiscardList.Remove(card_id);
                    // Played like any other (spies draw, musters muster, medics chain)
                    PlayOutcome outcome = PlayRules.Resolve(Board, card, row, seat, manual_medic: true, transform: false);
                    Played(seat, card_id, row, PlaySource.Discard, outcome);
                    return true;
                case ChoiceKind.WeatherFromDeck:
                    if (!side.DeckList.Contains(card_id))
                        return false;
                    Choice = null;
                    Phase = MatchPhase.Turn;
                    PlayFromDeck(seat, card_id);
                    return true;
                case ChoiceKind.RestoreFromDiscard:
                case ChoiceKind.RestoreFromOpponentDiscard:
                case ChoiceKind.DeckPick:
                    Choice = null;
                    if (!TakeToHand(seat, choice.Kind, card_id))
                    {
                        Choice = choice;
                        return false;
                    }
                    EndTurn(seat);
                    return true;
            }
            return false;
        }

        // A card into the side's hand: from its discard pile or the opponent's (both see it), or its deck
        private bool TakeToHand(bool seat, ChoiceKind kind, int card_id)
        {
            PlayerState side = Board.Side(seat);
            List<int> pile = kind == ChoiceKind.DeckPick ? side.DeckList
                : kind == ChoiceKind.RestoreFromOpponentDiscard ? Board.Opponent(seat).DiscardList : side.DiscardList;
            if (!pile.Remove(card_id))
                return false;
            side.HandList.Add(card_id);
            side.CanDiscard = false;
            PlaySource from = kind == ChoiceKind.DeckPick ? PlaySource.Deck : kind == ChoiceKind.RestoreFromOpponentDiscard ? PlaySource.OpponentDiscard : PlaySource.Discard;
            if (from != PlaySource.Deck)
                Known(seat).Add(card_id);
            Emit(MatchEventKind.Picked, seat, e => { e.Card = card_id; e.From = from; });
            return true;
        }

        // Eredin, Destroyer of Worlds: a card of the hand discarded; after the second, a card of the deck chosen (if any)
        private bool Discard(bool seat, int card_id, int hand_index)
        {
            if (Phase != MatchPhase.Choice || Choice == null || Choice.Seat != seat || Choice.Kind != ChoiceKind.Discard)
                return false;
            PlayerState side = Board.Side(seat);
            int index = HandIndex(side.HandList, card_id, hand_index);
            if (index < 0)
                return false;
            side.HandList.RemoveAt(index);
            AbilityRules.Discard(side, card_id); // a discarded Cow still calls the Bovine Defense Force, next round
            side.CardsDiscarded++;
            Choice.Left--;
            Emit(MatchEventKind.Discarded, seat, e => e.Card = card_id);
            if (Choice.Left > 0 && side.HandList.Count > 0)
                return true;
            side.CanDiscard = false;
            if (side.DeckList.Count > 0)
            {
                List<int> deck = new List<int>(side.DeckList);
                deck.Sort(); // what it holds, not in what order
                Ask(new Choice { Kind = ChoiceKind.DeckPick, Seat = seat, Options = deck }, MatchPhase.Choice);
            }
            else
            {
                Choice = null;
                EndTurn(seat);
            }
            return true;
        }

        // The choice given up: the turn ends (a medic stays without its revival, a spent leader stays spent)
        private bool Skip(bool seat)
        {
            if (Phase != MatchPhase.Choice || Choice == null || Choice.Seat != seat || Choice.Kind == ChoiceKind.Discard)
                return false;
            Choice = null;
            Board.Side(seat).CanDiscard = false;
            Emit(MatchEventKind.Skipped, seat);
            EndTurn(seat);
            return true;
        }

        //----------------------------------------Turns and rounds----------------------------------------//
        // A round opens: a side with no card in hand sits it out (The Witcher 3), then the given side's turn
        private void StartRound(bool starts)
        {
            Round++;
            Phase = MatchPhase.Turn;
            Turn = starts;
            Emit(MatchEventKind.RoundStarted, starts);
            foreach (bool seat in new[] { true, false })
                if (Board.Side(seat).HandList.Count == 0 && !Board.Side(seat).hasPassed)
                    Passed(seat, PassReason.EmptyHand);
            StartTurn(starts);
        }

        // The given side's turn; one with nothing left to play passes (The Witcher 3); one that passed is skipped; once
        // both have passed, the round ends
        private void StartTurn(bool seat)
        {
            for (int i = 0; i < 2; i++, seat = !seat)
            {
                PlayerState side = Board.Side(seat);
                if (!side.hasPassed && RoundRules.OutOfPlays(Board, seat))
                    Passed(seat, PassReason.NothingToPlay);
                if (!side.hasPassed)
                {
                    Phase = MatchPhase.Turn;
                    Turn = seat;
                    Emit(MatchEventKind.TurnStarted, seat);
                    return;
                }
                if (Board.Opponent(seat).hasPassed)
                    break;
            }
            EndRound();
        }

        private void EndTurn(bool seat)
        {
            Choice = null;
            StartTurn(!seat);
        }

        private void Passed(bool seat, PassReason why)
        {
            Board.Side(seat).hasPassed = true;
            lastPasser = seat;
            Emit(MatchEventKind.Passed, seat, e => e.Why = why);
        }

        // Both sides passed: the round's result; the battle ends, or the board clears for the next round (Monsters keep a
        // unit, the Northern Realms draw, Skellige brings two units back at the third's start) and it opens with the
        // round's winner (after a draw, whoever passed last)
        private void EndRound()
        {
            RoundWinner winner = RoundRules.EndRound(Board, out int player_total, out int enemy_total);
            Emit(MatchEventKind.RoundEnded, true, e =>
            {
                e.Winner = winner;
                e.PlayerTotal = player_total;
                e.EnemyTotal = enemy_total;
                e.Flag = player_total == enemy_total && winner != RoundWinner.Draw;
            });
            RoundWinner? result = RoundRules.BattleResult(Board);
            if (result.HasValue)
            {
                Phase = MatchPhase.Over;
                Result = result.Value;
                Emit(MatchEventKind.Ended, true, e => e.Winner = Result);
                return;
            }

            RoundRules.ClearForNextRound(Board, winner, out int player_kept, out int enemy_kept, out bool player_drew, out bool enemy_drew);
            Emit(MatchEventKind.BoardCleared, true, e =>
            {
                e.PlayerKept = player_kept;
                e.EnemyKept = enemy_kept;
                e.PlayerDrew = player_drew;
                e.EnemyDrew = enemy_drew;
            });
            RoundRules.Reinforce(Board, out List<int> player_placed, out List<int> enemy_placed);
            if (player_placed.Count + enemy_placed.Count > 0)
                Emit(MatchEventKind.Reinforced, true, e => { e.PlayerCards = player_placed; e.EnemyCards = enemy_placed; });
            StartRound(RoundRules.PlayerStarts(winner, lastPasser));
        }

        //----------------------------------------Events----------------------------------------//
        private void Ask(Choice choice, MatchPhase phase)
        {
            Phase = phase;
            Choice = choice;
            Turn = choice.Seat;
            Emit(MatchEventKind.ChoiceAsked, choice.Seat);
        }

        private void Emit(MatchEventKind kind, bool seat, Action<MatchEvent> fill = null)
        {
            PruneKnown();
            MatchEvent e = new MatchEvent { Kind = kind, Seat = seat };
            fill?.Invoke(e);
            e.State = Snapshot();
            Events.Add(e);
        }

        // The battle as it is now (copies)
        public MatchState Snapshot()
        {
            return new MatchState
            {
                Player = Board.Player.Clone(),
                Enemy = Board.Enemy.Clone(),
                Weather = new List<int>(Board.Weather),
                DoubleSpies = Board.DoubleSpies,
                RandomMedic = Board.RandomMedic,
                Round = Round,
                Phase = Phase,
                Turn = Turn,
                Choice = Choice?.Copy(),
                Result = Result,
                PlayerKnown = new List<int>(playerKnown),
                EnemyKnown = new List<int>(enemyKnown),
            };
        }

        //----------------------------------------Helpers----------------------------------------//
        // The card's place in the hand: the one given if it holds that card (with two alike, the one chosen), else its last
        // copy (a card picked from a pile joins the end of the hand); -1 when the hand does not hold it
        private static int HandIndex(List<int> hand, int card_id, int hand_index)
        {
            return hand_index >= 0 && hand_index < hand.Count && hand[hand_index] == card_id ? hand_index : hand.LastIndexOf(card_id);
        }

        private List<int> Known(bool seat)
        {
            return seat ? playerKnown : enemyKnown;
        }

        // The cards both sides saw go into a hand stay seen while the hand holds them
        private void PruneKnown()
        {
            foreach (bool seat in new[] { true, false })
            {
                List<int> known = Known(seat);
                List<int> hand = new List<int>(Board.Side(seat).HandList);
                for (int i = known.Count - 1; i >= 0; i--)
                    if (!hand.Remove(known[i]))
                        known.RemoveAt(i);
            }
        }
    }
}
