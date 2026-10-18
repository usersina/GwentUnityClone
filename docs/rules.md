# Rules

[Docs](README.md) › Rules

The rules follow The Witcher 3's Gwent closely and are covered by automated tests (see [Testing](testing.md)). This page lists
how the game settles the cases the cards leave open, and where it differs from The Witcher 3.

## Contents

- [Turns and rounds](#turns-and-rounds)
- [Leaders](#leaders)
- [Cards](#cards)
- [Skellige](#skellige)
- [Differences from The Witcher 3](#differences-from-the-witcher-3)

## Turns and rounds

- Each turn you do one thing: play a card, use your leader or pass. Playing a card ends your turn. Passing is final for the round:
  the other side keeps playing until it passes too, then the higher total wins the round. A side with no card left and no leader
  ability it could use right now passes automatically, and a side that starts a round with no card in hand sits that round out,
  even with its leader unused.
- The winner of a round starts the next one (after a drawn round, whoever passed last). Scoia'tael decide who starts the battle.

## Leaders

- A leader ability that would do nothing right now (no weather to clear, an enemy row under 10 for a scorch, no matching weather card
  in the deck...) can't be used, so it is never wasted. Passive leaders (The Treacherous, Invader of the North, The White Flame,
  Daisy of the Valley) work from the start; The White Flame cancels the other leader, passive ones included.
- Francesca, Hope of the Aen Seidhe moves an agile unit when its own strength would be higher on the other row (ranged row first),
  as The Witcher 3 judges it, not when your total would be.
- The Eredin leaders do what their cards say in The Witcher 3 (as the Witcher wiki lists them): Commander of the Red Riders doubles
  your close combat row, Bringer of Death restores a card from your discard pile, Destroyer of Worlds discards 2 cards to draw 1 of
  your choice, King of the Wild Hunt plays any weather card from your deck.

## Cards

- A decoy on a row is not a unit: morale, horns and scorch ignore it, and Monsters never keep it (nor a hero) at the end of a round.
- The Cow summons the Bovine Defense Force (8, close combat). Killed by a scorch, it is avenged at once: the Defense Force takes the
  field as soon as the scorch is over (a choice of this game, see below). Discarded any other way (cleared at a round's end, or from
  the hand by Destroyer of Worlds), it arrives when the board is cleared for the next round, never if the battle ends first. A decoy
  taking the Cow back to the hand calls nothing. The game says so when a Cow leaves during a round, and the Cow's description box
  explains it.
- Summoned cards are tokens, as in Yu-Gi-Oh: the Bovine Defense Force, Skellige's Hemdall and the Berserkers' bears only exist on
  the board. They never go to a hand or a discard pile (a decoy can't take them back, no medic brings them back, no deck holds
  them) and vanish when they leave the board. The card that brought one is in its discard pile already: a killed Cow or Kambi, a
  Berserker that turned.
- Muster also pulls the matching cards from your hand, not only from your deck (The Witcher 3 does this too, although the card only
  says "deck"). The Arachas Behemoth calls the Arachas and Gaunter O'Dimm calls the Darkness, but not the other way round.

## Skellige

Skellige comes from Blood and Wine.

- As the third round opens, two random units of its discard pile return to the battlefield, on their own rows (no heroes; no spies
  either, which would fight for the other side; they come without using their abilities). Crach an Craite shuffles both discard
  piles back into the decks; under King Bran, bad weather only halves his units (rounded down, at least 1), unless The White Flame
  cancels him.
- Mardroeme goes in a row's horn slot (a row holds a horn or a Mardroeme, not both) and fits only Skellige's decks; Skellige Storm is
  weather on the ranged and siege rows, for any faction. A Berserker on a row with a Mardroeme turns into its bear, whether the
  Mardroeme came first or it did; Ermion is a Mardroeme on his row. The bear is a token: the Berserker goes to the discard pile as it
  turns, so a medic can bring it back, to turn again on a Mardroeme's row.
- Kambi is an Avenger like the Cow: killed by a scorch, Hemdall (11, a hero) takes the field at once; discarded any other way, he
  comes next round. Cerys musters the Clan Drummond Shield Maidens; the Clan Dimun Pirate's scorch is the Scorch card's (the strongest
  units on the battlefield as he lands, never himself).

## Differences from The Witcher 3

Differences that remain: a revived agile unit goes where you put it (The Witcher 3 puts it on the row where it is stronger), and a
leader's horn leaves no card in the discard pile. Some are choices: a scorched Cow's Bovine Defense Force (a scorched Kambi's
Hemdall) comes at once, not next round; summoned cards are tokens (a decoy can't take them back); a Berserker goes to the discard
pile as it turns.
