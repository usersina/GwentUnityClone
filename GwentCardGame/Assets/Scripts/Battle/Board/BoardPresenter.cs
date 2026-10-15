using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gwent.Rules;
using Place = Gwent.Battle.BoardChange.Place;
using UnityEngine;

namespace Gwent.Battle
{
    // Plays a change of the board the way The Witcher 3 does (the rules resolved it already; the board still shows it as
    // it was): the played card glides in from the board's right edge and lands with a sparkle (a hero in smoke and fire),
    // its ability flares, then what it caused follows: scorched cards burn and go to the discard piles, a medic's flare
    // over the discard pile brings a card back, called and drawn cards fly in from the deck, a decoyed card goes back to
    // the hand. In game time: the pause menu stops it.
    public class BoardPresenter
    {
        public const float LandSeconds = 0.3f;
        public const float MoveSeconds = 0.35f;
        private const float Stagger = 0.07f;  // between cards moving together
        private const float ClearStagger = 0.03f;
        private const float Arc = 50f;        // the bend of a played card's glide (overlay units)

        private readonly BoardView board;
        private readonly CardDatabase db;

        public BoardPresenter(BoardView board, CardDatabase db)
        {
            this.board = board;
            this.db = db;
        }

        // played_id: the card played (-1: none, a leader ability); medic_choice: the player is about to pick the card a
        // medic brings back
        public async UniTask Play(BoardChange change, int played_id, bool by_player, bool medic_choice, CancellationToken ct)
        {
            HashSet<object> shown = new HashSet<object>();

            // The played card lands, and its ability flares
            BoardChange.Arrival played = Played(change, played_id, by_player);
            Card card = played_id >= 0 ? db.Get(played_id) : null;
            if (played != null)
            {
                shown.Add(played);
                foreach (BoardChange.Move from_hand in change.Moves.Where(m => m.To == played))
                {
                    shown.Add(from_hand);
                    Drop(from_hand.From);
                }
                await Land(played, card, ct);
                await Flare(played, card, ct);
            }

            // Scorched cards burn (then go to the discard piles with the rest)
            RectTransform[] burned = change.Moves.Where(m => m.From.From == Place.Row && m.To.To == Place.Discard).Select(m => m.From.Ghost)
                .Concat(change.Gone.Where(d => d.From == Place.Row).Select(d => d.Ghost)).ToArray();
            if (burned.Length > 0)
                await CardEffects.Burn(burned, ct);

            // A medic's flare over its discard pile, then the card it brings back
            List<BoardChange.Move> revived = change.Moves.Where(m => m.From.From == Place.Discard && m.To.To != Place.Hand).ToList();
            if (card != null && card.ability == Ability.Medic && (revived.Count > 0 || medic_choice))
                await CardEffects.MedicPile(revived.Count > 0 && revived[0].From.Ghost != null ? revived[0].From.Ghost : board.PileTop(by_player), ct);
            if (revived.Count > 0)
            {
                shown.UnionWith(revived.Cast<object>());
                await UniTask.WhenAll(revived.Select((move, i) => Move(move, i * Stagger, ct)));
            }

            // Everything else, together
            List<UniTask> rest = new List<UniTask>();
            foreach (BoardChange.Move move in change.Moves.Where(m => !shown.Contains(m)))
                rest.Add(Move(move, rest.Count * Stagger, ct));
            foreach (BoardChange.Arrival arrival in change.New.Where(a => !shown.Contains(a)))
                rest.Add(Arrive(arrival, rest.Count * Stagger, arrival.To == Place.Hand || arrival.To == Place.Row, ct));
            foreach (BoardChange.Departure departure in change.Gone)
                rest.Add(Vanish(departure, ct));
            await UniTask.WhenAll(rest);
        }

        // The end of a round: every card on the board leaves for its discard pile at once (the Monsters' kept unit
        // stays). A card the new round brings flies from the deck to the hand (the Northern Realms' draw); a summoned one
        // (the Bovine Defense Force) appears in its place.
        public async UniTask Clear(BoardChange change, CancellationToken ct)
        {
            List<UniTask> all = new List<UniTask>();
            foreach (BoardChange.Move move in change.Moves)
                all.Add(Move(move, all.Count * ClearStagger, ct));
            foreach (BoardChange.Arrival arrival in change.New)
                all.Add(Arrive(arrival, all.Count * ClearStagger, arrival.To == Place.Hand, ct));
            foreach (BoardChange.Departure departure in change.Gone)
                all.Add(Vanish(departure, ct));
            await UniTask.WhenAll(all);
        }

        // The played card's arrival. Copies called with it are the same card: the player's played card is the one
        // that came from nowhere (its object left the hand before), the opponent's the one that left its hand.
        private static BoardChange.Arrival Played(BoardChange change, int played_id, bool by_player)
        {
            if (played_id < 0)
                return null;
            BoardChange.Arrival arrival = change.New.Find(a => a.CardId == played_id && a.To != Place.Hand);
            BoardChange.Move move = change.Moves.Find(m => m.To.CardId == played_id && m.From.From == Place.Hand && m.To.To != Place.Hand);
            if (move != null && (!by_player || arrival == null))
                return move.To;
            return arrival;
        }

        private async UniTask Land(BoardChange.Arrival played, Card card, CancellationToken ct)
        {
            LaneHold.Release(played.Card);
            Rect slot = CardMotion.ScreenRect((RectTransform)played.Card.transform);
            await CardMotion.Fly(CardMotion.Ghost(played.CardId, board.LaunchRect(slot.size)), played.Card, LandSeconds, Arc, ct);
            if (card.IsUnit && card.unique)
                await CardEffects.Hero(played.Card, ct);
            else
                await CardEffects.Sparkle(played.Card, ct);
        }

        // Its ability's icon blazes over it (a tight bond over all its cards, once there are two); a horn lights its row
        private async UniTask Flare(BoardChange.Arrival played, Card card, CancellationToken ct)
        {
            if (played.Card == null)
                return;
            string flare = CardEffects.FlareFor(card.ability);
            List<UniTask> effects = new List<UniTask>();
            if (card.ability == Ability.TightBond)
            {
                List<GameObject> bond = played.Card.transform.parent.Cast<Transform>().Where(c => c.name == played.Card.name).Select(c => c.gameObject).ToList();
                if (bond.Count > 1)
                    effects.AddRange(bond.Select(c => CardEffects.Flare(c, flare, ct)));
            }
            else if (flare != null)
                effects.Add(CardEffects.Flare(played.Card, flare, ct));
            if (card.ability == Ability.CommanderHorn || card.ability == Ability.UnitHorn)
            {
                Transform row = played.Card.transform.parent;
                effects.Add(CardEffects.RowGlow((RectTransform)(row.name == "Special" ? row.parent.Find("Unit") : row), ct));
            }
            await UniTask.WhenAll(effects);
        }

        // A card that left a place flies to its new one
        private static async UniTask Move(BoardChange.Move move, float delay, CancellationToken ct)
        {
            if (delay > 0)
                await Wait(delay, ct);
            LaneHold.Release(move.From.Spacer);
            LaneHold.Release(move.To.Card);
            await CardMotion.Fly(move.From.Ghost, move.To.Card, MoveSeconds, ct: ct);
        }

        // A card from off the board: from the deck, or appearing where it is
        private async UniTask Arrive(BoardChange.Arrival arrival, float delay, bool from_deck, CancellationToken ct)
        {
            if (delay > 0)
                await Wait(delay, ct);
            LaneHold.Release(arrival.Card);
            if (arrival.Card == null)
                return;
            if (from_deck)
            {
                await CardMotion.Fly(CardMotion.Ghost(arrival.CardId, board.DeckRect(arrival.IsPlayer)), arrival.Card, MoveSeconds, ct: ct);
                return;
            }
            CardMotion.Hide(arrival.Card, false);
            CardMotion.SettleIn(arrival.Card);
        }

        private static async UniTask Vanish(BoardChange.Departure departure, CancellationToken ct)
        {
            LaneHold.Release(departure.Spacer);
            await CardMotion.FadeOut(departure.Ghost, 0.25f, ct);
        }

        // A departure that is not shown (the opponent's played card leaving its hand: it comes in from the showcase)
        private static void Drop(BoardChange.Departure departure)
        {
            if (departure.Ghost != null)
                UnityEngine.Object.Destroy(departure.Ghost.gameObject);
            LaneHold.Release(departure.Spacer);
        }

        // A wait in game time (the pause stops it)
        private static UniTask Wait(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
        }
    }
}
