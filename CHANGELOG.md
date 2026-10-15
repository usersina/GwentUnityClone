# Changelog

Every push to `main` is a release: it replaces the one GitHub release, `latest`, and the newest section below becomes its
notes. A push to `main` is only released when this file has a new section since the last release: add one, headed by the
date, before pushing.

## 2026-10-15

A faster start on the web, the menus rebuilt, closer to The Witcher 3 in battle, and most of the game's insides renewed.

### The battle, closer to The Witcher 3

- Your hand stays in the order the cards were drawn, as the redraw shows it: a redrawn card takes the place of the one
  it replaces, new cards join at the right.
- The leader: click it and it leaves its place for the right, like a card about to be played, and its crown glows
  orange; the crown uses it (X, then X again or Enter, still works). Its empty place, right-click or Esc puts it back.
- A played card appears at the end of its row already as it looks on the board and glides into place; a card put back
  turns at once into its hand version; drawn cards fly in as hand cards.
- The effects, closer to the game's: a hero lands under billowing smoke while a swarm of sparks runs around its edge, flames
  rising off the top; an ability's card flashes white and burns bright orange around its large white icon; a landing card
  puffs white wisps from its sides; a medic's pile smokes with green flames. They are real particle effects now.
- The cards' descriptions (under the big card, in the deck builder, the redraw and the leader picker) are written live in
  The Witcher 3's style: its near-black panel, the ability's icon on a cream disc, the title and text in muted gold.
- The Ice Giant is 5 strength, and Eredin's four leader cards each show their own art, name and ability, as in The
  Witcher 3.

### In battle

- Give up Yu-Gi-Oh style: in your turn, lay your hand on your deck (the cursor turns into a hand), click it or hold a
  finger on it, then confirm. The pause menu no longer has it.
- Skipping a choice (a leader's or a medic's pick) asks first, so a stray click can't waste it.
- Kill the Cow, get the punishment: a Cow killed by a scorch calls the Bovine Defense Force at once (The Witcher 3 waits
  for the next round); discarded any other way, it still comes next round. A notice says which.
- The decks' card counts are back under the decks.
- The weather looks as in The Witcher 3: its own art of the frost, the fog and the rain lies over the row and its cards, fades in
  with the card and out once cleared; the fog billows up and drifts along the row.
- On Normal and Hard the opponent now plays its Cow while the battle goes on, for the Bovine Defense Force it brings next round.

### The deck builder

- A card you add or remove flies to its place in the other grid: one copy leaves while others are left, the last copy is
  the card itself; where it lands, the count goes up as it arrives.
- Its two grids show a scroll bar in the menus' gold while their cards overflow.

### Fixes

- Editing a preset deck with a long name (such as Riders of the Wild Hunt) a second time no longer freezes the game.

### The web version

- The game starts after downloading about 26 MB instead of about 67: the cards' full art and the music now come when they
  are first shown or played (a battle fetches its two decks' art as it begins), and the browser keeps them for next time.

### Menus

- The main menu, opponent picker, settings, pause menu, How to play, confirmations and notices are rebuilt on Unity's
  UI Toolkit, with the same look and smoother highlights.
- How to play's topics show a scroll bar while they overflow (the Monsters' part was out of reach without a mouse
  wheel).

### Platforms

- The macOS build is for Apple Silicon only (Unity is dropping Intel Macs).

### Under the hood

- The battle's turns and animations run on UniTask and LitMotion; the art and music load through Addressables; the
  effects' particles are drawn by ParticleEffectForUGUI.
- The editor and development builds write a game log (`Logs/game.jsonl`): every play, AI decision, round and menu, a JSON
  line each, for people and AI agents to read. Release builds write none.

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
