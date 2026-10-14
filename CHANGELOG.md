# Changelog

Every push to `main` is a release: it replaces the one GitHub release, `latest`, and the newest section below becomes its
notes. A push to `main` is only released when this file has a new section since the last release: add one, headed by the
date, before pushing.

## 2026-10-14

The whole game rebuilt on Unity 6.6, closer to The Witcher 3's Gwent in its rules and its look, with opponents for every
faction and builds for Linux, Windows, macOS, Android and the web.

### Rules, as in The Witcher 3

- A new rules engine, checked against The Witcher 3's own Gwent code: weather, tight bond, morale, Commander's Horn and
  Dandelion in the game's order, scorch, muster, medics (chaining), spies, agile units and decoys.
- Every leader does what its card says in The Witcher 3, and an ability that would do nothing can't be wasted.
- The faction perks: Northern Realms draw after a won round, Nilfgaard win ties, Monsters keep a unit, Scoia'tael choose
  who goes first.
- The Witcher 3's round flow: the winner starts the next round, a side with an empty hand sits a round out, and the Cow
  calls the Bovine Defense Force.
- Decks hold only the copies The Witcher 3 has of each card.

### Opponents

- Nine opponent decks across the four factions, picked before the battle or left to chance, each with a leader that suits
  its cards.
- Three difficulties in Settings > Game: Easy plays like a beginner; Normal saves its cards for the rounds that count;
  Hard plays each move out to the end of the battle, a few dozen times, and keeps the one that wins most often (it never
  sees your hand).

### The battle

- The board, cards and notices in The Witcher 3's style: the left column, score discs and laurel, row emblems, the turn
  glow, its emblems for each notice, the gem that breaks at the end of a round.
- Plays shown as in The Witcher 3: the card on display, the glide to its place, a hero landing in fire, ability effects.
- The redraw in The Witcher 3's carousel, and the same carousel for card pickers and discard piles.
- An end screen with The Witcher 3's emblem of the outcome, and Give up in the pause menu.
- Game speed: Slow, Normal or Fast.

### Menus and the deck builder

- A new main menu, settings (audio, display, game speed, difficulty), pause menu and an in-game How to play.
- The deck builder lists each card once with its count, has a Weather tab, and offers the opponents' decks as presets
  (your changes make your own copy).

### Controls

- Mouse, keyboard (play a card, use the leader, pass by holding Space, browse discard piles) and touch (tap, hold a finger
  on a card to read it).

### Platforms

- Linux, Windows, macOS (Intel and Apple Silicon), Android (64-bit) and the web.
- Downloads about half the size they would have been (every image compresses on every platform).

### Not yet as in The Witcher 3

- The Ice Giant is 6 strength (5 in The Witcher 3); a revived agile unit goes where you put it; a leader's horn leaves no
  card in the discard pile; no Skellige faction (Blood and Wine).
