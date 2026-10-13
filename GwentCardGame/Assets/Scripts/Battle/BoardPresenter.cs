using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;
using Place = BoardChange.Place;

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
    private const float Arc = 50f;        // the bend of a played card's glide (overlay units)

    private readonly MonoBehaviour host;
    private readonly BoardView board;
    private readonly CardDatabase db;

    public BoardPresenter(MonoBehaviour host, BoardView board, CardDatabase db)
    {
        this.host = host;
        this.board = board;
        this.db = db;
    }

    // played_id: the card played (-1: none, a leader ability); medic_choice: the player is about to pick the card a
    // medic brings back
    public IEnumerator Play(BoardChange change, int played_id, bool by_player, bool medic_choice)
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
            yield return Land(played, card);
            yield return Flare(played, card);
        }

        // Scorched cards burn (then go to the discard piles with the rest)
        RectTransform[] burned = change.Moves.Where(m => m.From.From == Place.Row && m.To.To == Place.Discard).Select(m => m.From.Ghost)
            .Concat(change.Gone.Where(d => d.From == Place.Row).Select(d => d.Ghost)).ToArray();
        if (burned.Length > 0)
            yield return CardEffects.Burn(burned);

        // A medic's flare over its discard pile, then the card it brings back
        List<BoardChange.Move> revived = change.Moves.Where(m => m.From.From == Place.Discard && m.To.To != Place.Hand).ToList();
        if (card != null && card.ability == Abilities.Medic && (revived.Count > 0 || medic_choice))
            yield return CardEffects.PileFlare((RectTransform)board.View(by_player).DiscardPile.transform.parent);
        if (revived.Count > 0)
        {
            shown.UnionWith(revived.Cast<object>());
            yield return All(revived.Select((move, i) => Move(move, i * Stagger)));
        }

        // Everything else, together
        List<IEnumerator> rest = new List<IEnumerator>();
        foreach (BoardChange.Move move in change.Moves.Where(m => !shown.Contains(m)))
            rest.Add(Move(move, rest.Count * Stagger));
        foreach (BoardChange.Arrival arrival in change.New.Where(a => !shown.Contains(a)))
            rest.Add(Arrive(arrival, rest.Count * Stagger));
        foreach (BoardChange.Departure departure in change.Gone)
            rest.Add(Vanish(departure));
        yield return All(rest);
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

    private IEnumerator Land(BoardChange.Arrival played, Card card)
    {
        LaneHold.Release(played.Card);
        Rect slot = CardMotion.ScreenRect((RectTransform)played.Card.transform);
        yield return CardMotion.Fly(CardMotion.Ghost(played.CardId, board.LaunchRect(slot.size)), played.Card, LandSeconds, Arc);
        if (card.IsUnit && card.unique)
            yield return CardEffects.Hero(played.Card);
        else
            yield return CardEffects.Sparkle(played.Card);
    }

    // Its ability's icon blazes over it (a tight bond over all its cards, once there are two); a horn lights its row
    private IEnumerator Flare(BoardChange.Arrival played, Card card)
    {
        if (played.Card == null)
            yield break;
        string flare = CardEffects.FlareFor(card.ability);
        List<IEnumerator> effects = new List<IEnumerator>();
        if (card.ability == Abilities.TightBond)
        {
            List<GameObject> bond = played.Card.transform.parent.Cast<Transform>().Where(c => c.name == played.Card.name).Select(c => c.gameObject).ToList();
            if (bond.Count > 1)
                effects.AddRange(bond.Select(c => CardEffects.Flare(c, flare)));
        }
        else if (flare != null)
            effects.Add(CardEffects.Flare(played.Card, flare));
        if (card.ability == Abilities.CommanderHorn || card.ability == Abilities.UnitHorn)
        {
            Transform row = played.Card.transform.parent;
            effects.Add(CardEffects.RowGlow((RectTransform)(row.name == "Special" ? row.parent.Find("Unit") : row)));
        }
        yield return All(effects);
    }

    // A card that left a place flies to its new one
    private static IEnumerator Move(BoardChange.Move move, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        LaneHold.Release(move.From.Spacer);
        LaneHold.Release(move.To.Card);
        yield return CardMotion.Fly(move.From.Ghost, move.To.Card, MoveSeconds);
    }

    // A card from off the board: from the deck into a hand or a row; anything else appears where it is
    private IEnumerator Arrive(BoardChange.Arrival arrival, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        LaneHold.Release(arrival.Card);
        if (arrival.Card == null)
            yield break;
        if (arrival.To == Place.Hand || arrival.To == Place.Row)
        {
            yield return CardMotion.Fly(CardMotion.Ghost(arrival.CardId, board.DeckRect(arrival.IsPlayer)), arrival.Card, MoveSeconds);
            yield break;
        }
        CardMotion.Hide(arrival.Card, false);
        CardMotion.SettleIn(arrival.Card);
    }

    private static IEnumerator Vanish(BoardChange.Departure departure)
    {
        LaneHold.Release(departure.Spacer);
        yield return CardMotion.FadeOut(departure.Ghost, 0.25f);
    }

    // A departure that is not shown (the opponent's played card leaving its hand: it comes in from the showcase)
    private static void Drop(BoardChange.Departure departure)
    {
        if (departure.Ghost != null)
            UnityEngine.Object.Destroy(departure.Ghost.gameObject);
        LaneHold.Release(departure.Spacer);
    }

    // Runs these together, until the last one ends
    private IEnumerator All(IEnumerable<IEnumerator> routines)
    {
        int running = 0;
        foreach (IEnumerator routine in routines.ToList())
        {
            running++;
            host.StartCoroutine(Run(routine, () => running--));
        }
        while (running > 0)
            yield return null;
    }

    private static IEnumerator Run(IEnumerator routine, Action done)
    {
        yield return routine;
        done();
    }
}
