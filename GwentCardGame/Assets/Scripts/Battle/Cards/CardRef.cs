using Gwent.Rules;
using UnityEngine;

namespace Gwent.Battle
{
    // The card a card object shows (on the board, in a picker, in the deck builder): set when the object is made. The
    // card is the database's own, shared by every object of that card, and never changed.
    public class CardRef : MonoBehaviour
    {
        [System.NonSerialized] public Card Card;
    }
}
