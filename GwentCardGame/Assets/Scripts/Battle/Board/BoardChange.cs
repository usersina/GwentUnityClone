using System.Collections.Generic;
using Gwent.Rules;
using UnityEngine;

namespace Gwent.Battle
{
    // What one play did to the board's card objects, for the battle's choreography. BoardView records it between
    // BeginChange and EndChange while the board still looks as it was: a card that left a place stays where it stood as
    // a ghost on the overlay (a spacer keeps its room in its line), a card that arrived stays hidden and out of its line.
    // Pairing them by card gives the moves (hand to row, row to discard pile, discard pile to row...); the other arrivals
    // came from off the board (the deck), the other departures left it for good. BoardPresenter plays it, Flush ends it.
    public class BoardChange
    {
        public enum Place { Hand, Row, Horn, Weather, Discard }

        public class Arrival
        {
            public int CardId;
            public bool IsPlayer;       // whose side it arrived on
            public Place To;
            public GameObject Card;     // its object, hidden until it is brought in
        }

        public class Departure
        {
            public int CardId;
            public bool IsPlayer;
            public Place From;
            public RectTransform Ghost; // its object, on the overlay where it stood
            public GameObject Spacer;   // keeps its room in its line (null outside a line)
        }

        public class Move
        {
            public Departure From;
            public Arrival To;
        }

        public readonly List<Arrival> Arrivals;
        public readonly List<Departure> Departures;
        public readonly List<Move> Moves = new List<Move>();
        public readonly List<Arrival> New = new List<Arrival>();      // arrived from off the board
        public readonly List<Departure> Gone = new List<Departure>(); // left the board for good

        // Pairs each arrival with a departure of the same card: on the same side first (a Berserker reaching its discard
        // pile, with the bear it was), else from a hand (a spy goes to the other side) or the weather slot (it has no side),
        // else from anywhere (a spy a medic brings back goes to the other side; Emhyr the Relentless takes a card from the
        // other side's discard pile)
        public BoardChange(List<Arrival> arrivals, List<Departure> departures)
        {
            Arrivals = arrivals;
            Departures = departures;
            List<Departure> left = new List<Departure>(departures);
            foreach (Arrival arrival in arrivals)
            {
                Departure from = left.Find(d => d.CardId == arrival.CardId && d.IsPlayer == arrival.IsPlayer)
                    ?? left.Find(d => arrival.To == Place.Discard && d.IsPlayer == arrival.IsPlayer && d.CardId == CardIds.TransformedOf(arrival.CardId)) // a bear gone: its Berserker
                    ?? left.Find(d => d.CardId == arrival.CardId && (d.From == Place.Hand || d.From == Place.Weather || arrival.To == Place.Weather))
                    ?? left.Find(d => d.CardId == arrival.CardId);
                if (from == null)
                {
                    New.Add(arrival);
                    continue;
                }
                left.Remove(from);
                Moves.Add(new Move { From = from, To = arrival });
            }
            Gone.AddRange(left);
        }

        // Ends the change at once: the ghosts and spacers go, every card shows in its place
        public void Flush()
        {
            foreach (Departure departure in Departures)
            {
                if (departure.Ghost != null)
                    Object.Destroy(departure.Ghost.gameObject);
                LaneHold.Release(departure.Spacer);
            }
            foreach (Arrival arrival in Arrivals)
            {
                LaneHold.Release(arrival.Card);
                CardMotion.Hide(arrival.Card, false);
            }
        }
    }
}
