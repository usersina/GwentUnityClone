# The AI

[Docs](README.md) › The AI

## Contents

- [Difficulties](#difficulties)
- [How the difficulties compare](#how-the-difficulties-compare)
- [The AI's decks](#the-ais-decks)

## Difficulties

The opponent plays by the same rules as you, at the difficulty set in **Settings > Game**:

- **Easy** plays like a beginner: each turn, the card that adds the most strength right now, round 1 included, so it runs short of
  cards later. A spy is just a weak card to it, now and then it plays a card that is not its best, and it keeps the hand it was dealt.
- **Normal** weighs every move on a copy of the board (weather, horns, morale, bonds, scorch): it plays its spies first, chains its
  medics, saves its cards in a round it can afford to lose, gives up a round it cannot win, and uses its leader when it pays off. It
  plays the Cow while the battle goes on (its Bovine Defense Force comes next round), and sees that scorching your Cow arms you.
- **Hard** looks ahead. For each move worth considering, it plays the rest of the battle out 24 times, both sides playing as Normal,
  and plays the move that wins most often. It never reads your hand: it knows which cards your deck holds (like a rival who has seen
  it), and for each of those battles deals your hand anew from the cards it has not seen yet. It thinks while the turn's notice shows,
  at most 1.5 seconds a move; a slower device plays out fewer battles, and Hard still beats Normal with a third of them.

## How the difficulties compare

Over the 11 decks, every one against every other, Normal beats Easy in about 70% of battles and Hard beats Normal in about 77%.
The tests hold them to it (`AiDifficultyTests`, see [Testing](testing.md)).

## The AI's decks

The 11 decks (`Assets/Resources/Decks/AI`) are adapted from [gwent-classic](https://github.com/asundr/gwent-classic)'s default
decks (see [Credits](credits.md)). Each deck's leader suits its cards: the Monster Horde, all close combat, brings fog and rain down
on the back rows (King of the Wild Hunt); the Riders of the Wild Hunt are their own close combat horn (Commander of the Red Riders,
so no Dandelion); the Rangers of Dol Blathanna are archers and agile units under a ranged horn (Francesca the Beautiful). Skellige's
two come from gwent-classic's: the an Craite Warband (Crach an Craite) and the Berserkers of Ard Skellig, now under King Bran, whose
halved weather suits their own weather cards (a Berserker and a second Mardroeme added for their theme).
