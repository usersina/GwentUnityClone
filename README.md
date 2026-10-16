# GwentUnityClone

<p align="center">
  <img src="Screenshots/banner.jpg" alt="The main menu: Geralt and Ciri playing Gwent">
</p>

 Gwent, the card game of The Witcher 3: Wild Hunt (the original in-game version, not the standalone Gwent), remade in Unity for Windows and Android.

## About this project

This is a fan remake of Gwent, the famous card game of The Witcher 3.
I'm aware that CD PROJEKT RED made an official standalone Gwent, but it plays very differently. I'm enthusiastic about the original version and its mechanics, which is one of the reasons I got so immersed in the world of The Witcher 3. So here it is, made nearly from scratch in the Unity game engine.

The card art comes from [matt77hias/Gwent](https://github.com/matt77hias/Gwent), which also explains the rules if you're new to the game. Skellige's cards (Blood and Wine) come from Black Swan's English print set, in the same style, as gathered by the community's [printing guide](https://www.reddit.com/r/gwent/comments/4rabye/another_guide_for_printing_gwent_cards_complete/) (King Bran with Black Swan's own fix).

I made the original interface in Adobe Photoshop. Today most of it is drawn by the game's own sprite generator, next to some of The Witcher 3's own interface art and a few free assets (see [Credits](#credits)).

The rules follow The Witcher 3's Gwent closely and are covered by automated tests (see [Tests](#tests)).

This game supports full deck editing and has an AI opponent with three difficulties (Easy, Normal, Hard) and 11 decks across the five factions, Skellige (Blood and Wine) included.

Sorry, no multiplayer :/ (yet)

## Screenshots

<h3 align="center">Northern Realms vs Monsters, the deciding round</h3>

![Northern Realms against Monsters in the third round](Screenshots/battle.jpg)

<h3 align="center">A card in the spotlight, a medic's choice</h3>

<p align="center">
  <img src="Screenshots/showcase.jpg" width="49%" alt="Philippa Eilhart selected, shown on the right with her description, her row lit; Biting Frost on the close rows">
  <img src="Screenshots/medic.jpg" width="49%" alt="Yennefer's revival: units from the discard pile in the card carousel">
</p>

<h3 align="center">The redraw, and the end of the battle</h3>

<p align="center">
  <img src="Screenshots/redraw.jpg" width="49%" alt="Choosing a card to redraw at the start of the battle">
  <img src="Screenshots/victory.jpg" width="49%" alt="The victory screen with every round's score">
</p>

<h3 align="center">Choosing an opponent, building a deck</h3>

<p align="center">
  <img src="Screenshots/opponents.jpg" width="49%" alt="The opponent picker: 11 decks across the five factions">
  <img src="Screenshots/deck-builder.jpg" width="49%" alt="The deck builder on a Northern Realms preset">
</p>

## How to play

| Action                                                                          | Desktop                                                                                                                                                                                                                                                                                                                                                                                                                                           | Android                                                                                                     |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Start a battle                                                                  | **Play**, then choose your opponent (one of 11 decks across the five factions) or **Random opponent**; the choice is remembered                                                                                                                                                                                                                                                                                                                    | Same, with taps                                                                                             |
| Redraw a card at the start (up to 2)                                            | Click a card in the redraw carousel to bring it forward, click it again (or Enter) to redraw it; arrow keys or the mouse wheel browse. In your hand, a click brings a card forward and a double-click on the front card redraws it (a quick double-click on another card only brings it forward). **Done**, Space or right-click keeps your hand                                                                                                  | Tap a card to bring it forward, tap it again to redraw it, then **Done**                                    |
| Select a card from your hand                                                    | Click it: it goes to the showcase on the right, with its description, while you choose its place (click its empty slot in the hand to put it back). Keyboard: Left/Right (or A/D) go through the hand, Enter takes the card                                                                                                                                                                                                                       | Tap it                                                                                                      |
| Play the selected card                                                          | Click a highlighted row (weather/scorch: the lit weather board, decoy: one of your framed units). The cursor turns gold where the card can go and shows a red "no" where it can't; clicking there plays a warning sound and keeps the card in your hand. Keyboard: Up/Down (or W/S) choose among the places it fits, Enter plays it there (or where it fits, when only one place does); Esc or right-click puts it back                           | Same, with taps (the warning sound, no cursor)                                                              |
| Use your leader                                                                 | Click the leader card: it leaves its slot for the right, as a card about to be played, shows where its ability acts (a row, a horn slot, the weather board, a discard pile), and its crown glows. Click the crown to use it; its empty slot, Esc, right-click or another card puts it back. Keyboard: X, then X again or Enter. The crown is gold while the ability can still be used this round, grey once used or after passing                 | Tap it, then tap the glowing crown. Hold a finger on the crown to see whether it can still be used          |
| Browse a discard pile (either side)                                             | Click it: it opens in the card carousel, its top card in front (arrow keys or the wheel browse, Esc or **Close** puts it away)                                                                                                                                                                                                                                                                                                                    | Tap it                                                                                                      |
| Pass (for the rest of the round)                                                | Hold Space, or hold the **Hold to pass** button                                                                                                                                                                                                                                                                                                                                                                                                   | Hold the button                                                                                             |
| Scoia'tael: choose who starts                                                   | **Go first** or **Go second** in the dialog at the start of the battle                                                                                                                                                                                                                                                                                                                                                                            | Tap                                                                                                         |
| After the battle                                                                | The end screen shows The Witcher 3's emblem of the outcome and every round's score; **Play again** or **Main menu**                                                                                                                                                                                                                                                                                                                               | Tap                                                                                                         |
| Pause (resume, settings, quit)                                                  | Esc or the menu button at the top right                                                                                                                                                                                                                                                                                                                                                                                                           | Back button or the menu button                                                                              |
| Give up (Yu-Gi-Oh style: a hand on your deck)                                   | In your turn, hover your deck: the cursor turns into a hand. Click it and confirm; it counts as a loss                                                                                                                                                                                                                                                                                                                                            | Hold a finger on your deck in your turn, then confirm                                                       |
| Choose a card in a picker (medic, leaders, Emhyr's look at the opponent's hand) | The cards show in the carousel under a title, as in the redraw: click a card to bring it forward, click it again (or Enter) to choose it; arrow keys or the wheel browse; Down goes to **Skip**, which asks first (**Close** for a look)                                                                                                                                                                                                                            | Tap a card to bring it forward, tap it again to choose it                                                   |
| Preview a card (board, hand, weather, discard pile, leaders)                    | Hover it                                                                                                                                                                                                                                                                                                                                                                                                                                          | Hold a finger on it: it shows on the right until you lift the finger (a hand card: tap it to select it)     |
| Deck builder: add/remove a card                                                 | Double-click it. It flies to the other grid: one copy while others are left, the card itself when it is the last. Each card shows once with its count: the copies in the deck, or in the collection the copies left to add (a card whose copies are all in the deck leaves the collection). Tabs: all, close, ranged, siege, heroes, weather, special cards                                                                                       | Tap it                                                                                                      |
| Deck builder: card details                                                      | Right-click                                                                                                                                                                                                                                                                                                                                                                                                                                       | Hold a finger on it                                                                                         |
| Deck builder: presets                                                           | Each faction's AI decks are listed after your own decks, marked "(preset)". Pick one to play with it as it is; your first change saves your own copy ("The Monster Horde (copy)") and the preset stays as it was. Presets can't be renamed or deleted                                                                                                                                                                                             | Same, with taps                                                                                             |
| Settings (audio, display, graphics, game speed, difficulty)                     | Main menu or pause menu > **Settings**. **Game > Game speed** (Slow, Normal, Fast) sets how fast cards move, notices pass and the opponent plays. **Game > Difficulty** (Easy, Normal, Hard) sets how well the opponent plays (see [The AI](#the-ai)). **Graphics** has a preset (Low, Medium, High; Custom once you change one) over how many particles the effects emit and each effect on or off, under Weather, Battle effects, Motion and Menus: animated weather (off: still pictures), the storm's lightning and thunder, Clear Weather's sun, the hero's landing, the scorch's fire, the ability flares (off, each keeps a light stand-in: a gold edge, the cards charring, the icon), the medic's flames, the horn's glow, the landing wisps, the shine and pulsing, the menu animations. A phone starts on Medium, the rest on High                                                                                                                                                                                             | Same                                                                                                        |
| Rules of the game                                                               | Main menu > **How to play**: the rules with the game's own pieces (the battle, the board, cards, abilities, special cards, the factions with their perks and leaders, controls); hover a leader to read its ability                                                                                                                                                                                                                               | Same; hold a finger on a leader                                                                             |

The deck you last selected in the deck builder (one of yours or a preset) is used in battle, as long as it is valid (at least 22 unit cards, at most 10 special cards,
and each card at most as many times as The Witcher 3 has copies of it: heroes and most cards once, up to 5 Mahakaman Defenders). The line
at the bottom of the deck builder shows which deck that is. Without a valid deck, the battle uses the built-in Northern Realms deck. A deck
saved with more copies than that loses the extra ones when it is loaded.

## Rules, as in The Witcher 3

- Each turn you do one thing: play a card, use your leader or pass. Playing a card ends your turn. Passing is final for the round:
  the other side keeps playing until it passes too, then the higher total wins the round. A side with no card left and no leader
  ability it could use right now passes automatically, and a side that starts a round with no card in hand sits that round out,
  even with its leader unused.
- The winner of a round starts the next one (after a drawn round, whoever passed last). Scoia'tael decide who starts the battle.
- A leader ability that would do nothing right now (no weather to clear, an enemy row under 10 for a scorch, no matching weather card
  in the deck...) can't be used, so it is never wasted. Passive leaders (The Treacherous, Invader of the North, The White Flame,
  Daisy of the Valley) work from the start; The White Flame cancels the other leader, passive ones included.
- A decoy on a row is not a unit: morale, horns and scorch ignore it, and Monsters never keep it (nor a hero) at the end of a round.
- The Cow summons the Bovine Defense Force (8, close combat). Killed by a scorch, it is avenged at once: the Defense Force takes the
  field as soon as the scorch is over (a choice of this game, see below). Discarded any other way (cleared at a round's end, or from the hand by Destroyer of Worlds), it arrives when the board is cleared
  for the next round, never if the battle ends first. A decoy taking the Cow back to the hand calls nothing. The game says so when a
  Cow leaves during a round, and the Cow's description box explains it.
- Summoned cards are tokens, as in Yu-Gi-Oh: the Bovine Defense Force, Skellige's Hemdall and the Berserkers' bears only exist on
  the board. They never go to a hand or a discard pile (a decoy can't take them back, no medic brings them back, no deck holds
  them) and vanish when they leave the board. The card that brought one is in its discard pile already: a killed Cow or Kambi, a
  Berserker that turned.
- Muster also pulls the matching cards from your hand, not only from your deck (The Witcher 3 does this too, although the card only
  says "deck"). The Arachas Behemoth calls the Arachas and Gaunter O'Dimm calls the Darkness, but not the other way round.
- Francesca, Hope of the Aen Seidhe moves an agile unit when its own strength would be higher on the other row (ranged row first),
  as The Witcher 3 judges it, not when your total would be.
- The Eredin leaders do what their cards say in The Witcher 3 (as the Witcher wiki lists them): Commander of the Red Riders doubles
  your close combat row, Bringer of Death restores a card from your discard pile, Destroyer of Worlds discards 2 cards to draw 1 of
  your choice, King of the Wild Hunt plays any weather card from your deck.
- Skellige (Blood and Wine): as the third round opens, two random units of its discard pile return to the battlefield, on their own
  rows (no heroes; no spies either, which would fight for the other side; they come without using their abilities). Crach an
  Craite shuffles both discard piles back into the decks; under King Bran, bad weather only halves his units (rounded down, at
  least 1), unless The White Flame cancels him.
- Mardroeme goes in a row's horn slot (a row holds a horn or a Mardroeme, not both) and fits only Skellige's decks; Skellige Storm is
  weather on the ranged and siege rows, for any faction. A Berserker on a row with a Mardroeme turns into its bear, whether the
  Mardroeme came first or it did; Ermion is a Mardroeme on his row. The bear is a token: the Berserker goes to the discard pile as it
  turns, so a medic can bring it back, to turn again on a Mardroeme's row.
- Kambi is an Avenger like the Cow: killed by a scorch, Hemdall (11, a hero) takes the field at once; discarded any other way, he
  comes next round. Cerys musters the Clan Drummond Shield Maidens; the Clan Dimun Pirate's scorch is the Scorch card's (the strongest
  units on the battlefield as he lands, never himself).

Differences that remain: a revived agile unit goes where you put it (The Witcher 3 puts it on the row where it is stronger), and a
leader's horn leaves no card in the discard pile. Some are choices: a scorched Cow's Bovine Defense Force (a scorched Kambi's
Hemdall) comes at once, not next round; summoned cards are tokens (a decoy can't take them back); a Berserker goes to the discard
pile as it turns.

## The AI

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

Over the 11 decks, every one against every other, Normal beats Easy in about 70% of battles and Hard beats Normal in about 77%.

Each deck's leader suits its cards: the Monster Horde, all close combat, brings fog and rain down on the back rows (King of the Wild
Hunt); the Riders of the Wild Hunt are their own close combat horn (Commander of the Red Riders, so no Dandelion); the Rangers of Dol
Blathanna are archers and agile units under a ranged horn (Francesca the Beautiful). Skellige's two come from gwent-classic's: the
an Craite Warband (Crach an Craite) and the Berserkers of Ard Skellig, now under King Bran, whose halved weather suits their own
weather cards (a Berserker and a second Mardroeme added for their theme).

## Development

Open `GwentCardGame/` with **Unity 6.6 (6000.6.5f1)**. The project was upgraded from 2019.4; the last 2019.4 commit is `a228947`.
Rendering uses the Universal Render Pipeline (URP 17.6, settings in `Assets/Settings`). The game is all UI, so HDR, MSAA, shadows and post-processing are off.

| Folder                               | What's inside                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Assets/Scripts/Rules`               | The game rules as plain C# with no Unity dependency (`Gwent.Rules` assembly): card data (factions, rows and abilities are enums; the JSON files are read through `CardEntry` and `DeckFile`, which keep their codes), strength calculation (weather, tight bond, morale, horns), scorch, muster, medic, agile, rounds, deck validation and the AI's decisions (`AiBrain`, with Hard's look-ahead in `AiSearch` and `BattleSim`)                                                                                                                                                                                                                                                                                                                                            |
| `Assets/Scripts/Battle`              | The battle (`Gwent.Battle`): `BattleController` runs turns and rounds on top of the rules, `AiPlayer` carries out the AI's decisions at the difficulty of the settings (Hard thinks on another thread while the turn's notice shows); `Board/`: `BoardView` draws the board from the model, `CardLane` lays out the hand and the rows in The Witcher 3's order, and `BoardPresenter` plays each play as The Witcher 3 does (the showcase, the card gliding in from the board's edge, a hero's fire, ability flares, scorch flames, the medic's sign, cards flying from the deck, a lost round's gem breaking) before the turn passes; `Cards/`, `Field/`, `Pickers/`: the card objects, the rows, weather, leaders, decks and side panels, and the redraw and card pickers |
| `Assets/Scripts/DeckBuilding`        | The deck builder (`Gwent.DeckBuilding`): `DeckBuilder`, the card grids, the faction header, the deck's summary and the leader picker                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `Assets/Scripts/Menus`               | Main menu, opponent picker, How to play, settings screen, pause menu (`Gwent.Menus`), in UI Toolkit: each binds its UXML document (`Assets/UI/Toolkit`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `Assets/Scripts/UI`                  | The interface (`Gwent.UI`): theme (`Resources/UITheme.asset`: fonts, colors, sprites, the UI Toolkit documents), `UIScreens` (a document on a Panel Renderer: menus at sort order 400, dialogs at 600, above the uGUI layer at 500; keyboard focus), the menus' controls (`MenuEntry`, `DialogButton`, `Switch`, `Stepper`, `ElementTooltip`), the uGUI kit the battle and the deck builder use, the overlay (Escape and the dialogs' stack, confirmations, toasts), the card carousel, the cursor                                                                                                                                                                                                                                                                         |
| `Assets/Scripts/Core`                | Settings, audio, input, deck files and the battle deck, the card database, the AI's decks, logging (`Gwent.Core`). Everything in `Assets/Scripts` but the rules is the `Gwent.Game` assembly; the editor tools in `Assets/Editor` are `Gwent.EditorTools`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `Assets/Editor/UISpriteGenerator.cs` | Draws the UI sprites (`Assets/UI/Sprites`) and the board pieces (`Assets/UI/Board`: portrait frame, card shadow, the glow ring of the battle's highlights, row frames, the rows' emblems shown on empty rows, the table; and `Assets/Resources/UI/Rules`: the How to play page's ability medallions, **Gwent > UI > Regenerate Rules Page Art**; the gems and score discs are The Witcher 3's art): **Gwent > UI > Regenerate Sprites and Theme**                                                                                                                                                                                                                                                                                                                          |
| `Assets/UI/Toolkit`                  | The menus' UXML documents and `Gwent.uss`: The Witcher 3's look as USS (the theme's colors as variables, the entries' and buttons' states with transitions), the `GwentTheme.tss` theme and the `Menus` panel settings                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `Assets/Content`                     | Loaded on demand through Addressables, a bundle apiece: each card's full art (`CardArt`, shown by `CardArt.Show`) and the music (`Music`). `Assets/Editor/GwentContent.cs` makes the Addressables groups from these folders (**Gwent > Content > Update Addressables**; every build runs it)                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| `Assets/Resources/Cards`             | `cards_db.json` (every card's stats, including how many copies The Witcher 3 has of it), the pictures on the board's cards, the card backs and icons                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `Assets/Resources/Decks`             | `player_default.json` (the built-in deck), `__empty__.json`, and the AI's 11 decks in `AI/`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| `Assets/Resources/W3`                | The Witcher 3's interface art: notices' emblems, ability effects, the end of battle emblems (see `CREDITS.md`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `Assets/Tests/EditMode`              | Unit tests for the rules and the AI's difficulties, and a fuzz test that plays hundreds of random battles                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `Assets/Tests/Content`               | Checks of the content: every card has its art and the description box it should (with its icon), every track its label, each group holds its whole folder, nothing is also in Resources |
| `Assets/Editor/GwentAutoplay.cs`     | End-to-end smoke test that plays full battles                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `Assets/Editor/GwentMonkey.cs`       | Monkey test: battles under a storm of random input                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |

Saved decks live in `Application.persistentDataPath/Decks/<faction>/<file name>.json`, the file name being the deck's name in lower case
with anything unsafe removed (`My Deck!` is `my_deck.json`); settings are saved in `PlayerPrefs`.
### The game log

Everything that happens is written to a log of events, one JSON object per line, meant to be read by people and by AI
agents alike: `GwentCardGame/Logs/game.jsonl` in the editor (one Play session; the one before is
`game.previous.jsonl`), `<persistentDataPath>/Logs/game.jsonl` in a development build. Each event is also shown on one
console line. Release builds have none of it: the calls are compiled out (`GameLog`, `BattleLog`), so nothing is
written; only the editor, development builds (`DEBUG`) and the `GWENT_VERBOSE_LOGS` define keep them.

Every line has `t` (seconds since the session began), `f` (frame), `scene`, `cat`, `ev`, and during a battle `battle`,
`round` and `turn`, then the event's own fields:

| `cat`    | Events                                                                                                         |
| -------- | -------------------------------------------------------------------------------------------------------------- |
| `game`   | `session_start` (version, platform, device), `scene`, `settings`, `opponent_chosen`, `music`, `autoplay_match`, `autoplay_failure`, `monkey_failure` |
| `battle` | `battle_start` (decks, leaders, difficulty), `redraw`, `round_start`, `turn_start` (with `board`: both sides' hands, rows with strengths, decks, lives, leaders, weather), `card_played`, `decoy_played`, `leader_used`, `leader_refused`, `picked`, `card_discarded`, `pass` (and why), `monsters_kept`, `northern_realms_draw`, `cow_avenged`, `summon_pending`, `first_turn_chosen`, `redraw_skipped`, `round_end`, `battle_end` (with `gave_up`), `battle_left` |
| `ai`     | `redraw`, `decision` (the move, `think_ms`, and `considered`: the Normal AI's gain, board change and cost of each move; Hard's `win_rate` over its play-outs) |
| `ui`     | `dialog_open`, `dialog_close`, `notice`, `toast`, `give_up_asked`, `settings_saved`, `quit`                     |
| `deck`   | `decks_folder`, `battle_deck`, `builder_open`, `deck_added`, `deck_saved`, `deck_renamed`, `deck_deleted`, `card_added`, `card_removed`, `battle_deck_chosen`, `preset_copied` |
| `perf`   | `slow_frame` (a frame over 100 ms), `card_art_loaded`                                                          |
| `unity`  | Unity's own `warning`, `error`, `exception`, `assert`, with `message` and `stack`                                  |

A play's `effects` say what moved between the zones (`player.hand`, `enemy.close`, `enemy.close_horn`,
`player.discard`, `weather`... as `in` and `out` lists, a deck as a count), so a scorch, a muster or a medic's chain
shows as what it did. A few ways in, with [jq](https://jqlang.org):

```bash
jq -c 'select(.cat=="battle" and (.ev|test("played|leader_used")))|{turn,side,card,row,effects}' GwentCardGame/Logs/game.jsonl
jq -c 'select(.ev=="decision")|{turn,action,think_ms,considered}' GwentCardGame/Logs/game.jsonl
jq -c 'select(.ev=="turn_start" and .turn==5)|.board' GwentCardGame/Logs/game.jsonl
jq -c 'select(.cat=="unity" or .ev=="autoplay_failure")' GwentCardGame/Logs/game.jsonl
```

### Release builds

Menu **Gwent > Build > All platforms** (or one platform) builds the game for Linux, Windows, macOS (Apple Silicon),
Android and the web, each from the same editor, into `GwentCardGame/Builds/<version>/` with archives ready to hand out
(`GwentLegacy-Linux.tar.gz`, `-Windows.zip`, `-macOS.zip`, `-Android.apk`, `-Web.zip`) and `build-report.txt`. A platform
whose build support is not installed (Unity Hub > Installs > Add modules) is skipped. The run goes on with the editor in the
background, starts with the platform the editor is on and switches back to it at the end (each platform's imports are cached,
so a switch takes seconds); all five take about 4 minutes once they have been built once. From the command line, with the
editor closed:

```bash
Unity -batchmode -projectPath GwentCardGame -executeMethod Gwent.EditorTools.GwentBuild.BuildAllBatch -buildTargets linux,windows
```

- The Android build is a 64-bit (ARM64) `.apk` to install directly. It is signed with a release key when the environment has
  `GWENT_KEYSTORE` (the keystore file), `GWENT_KEYSTORE_PASS`, `GWENT_KEY_ALIAS` and `GWENT_KEY_PASS`; otherwise with Unity's
  debug key, and a build from another machine then only installs after the old one is removed (which deletes its decks).
  The keystore path and version code it sets are put back in the project's settings after the build.
- The macOS app is ad-hoc signed by Unity, not notarized: macOS asks once to allow it (System Settings > Privacy & Security >
  Open Anyway). Windows shows "Windows protected your PC" once (More info > Run anyway).
- The cards' full art and the music are not in a build's data: they are Addressables bundles beside it (`StreamingAssets/aa`,
  built with each player), loaded the first time they are shown or played. The web build starts once the rest is downloaded;
  a battle fetches its two decks' art as it begins, the deck builder the cards it shows, and the browser caches them.
- The web build is compressed with a fallback, so any static host serves it. In a browser Hard thinks a few milliseconds a frame
  (there is no other thread), Quit is gone, the display settings are a Fullscreen switch, and decks are stored in the browser.
- Images are kept to sizes that are multiples of 4 so they compress on every platform (a card at 380x591 would ship uncompressed,
  four times larger).
- One graphics API per platform (Windows Direct3D 11, Linux OpenGL Core, macOS Metal, Android OpenGL ES 3, the web WebGL 2) and no
  post-processing in the renderer (the game uses none): shaders are compiled once per platform, not for every effect and API.

### Releases

**Every push to `main` is a release.** `.github/workflows/release.yml` runs the EditMode tests, builds the five platforms on
GitHub's machines ([GameCI](https://game.ci), Unity 6000.6.5f1, `Gwent.EditorTools.GwentBuild.BuildAllBatch` for each) and replaces the one
release, tagged `latest`, with the new archives; its notes are the newest section of [`CHANGELOG.md`](CHANGELOG.md), followed
by the downloads and their checksums. Nothing is published unless the tests and all five builds pass, and a push whose
`CHANGELOG.md` has no new section since the last release is not released (its run checks the changelog, skips the rest and
passes): add a section, headed by the date, before pushing.
Work on other branches and merge into `main` only to release.

The download links stay the same from one release to the next:
`https://github.com/usersina/GwentUnityClone/releases/latest/download/GwentLegacy-<Windows.zip|macOS.zip|Linux.tar.gz|Android.apk|Web.zip>`.

The workflow needs the Unity license and the Android release key as repository secrets; `tools/release-secrets.sh` sets them
once (it reads the license Unity Hub keeps on the machine, asks for the Unity account, and makes the Android key in
`~/.config/gwentlegacy/`, to back up: a new key would sign APKs that no longer install over the ones already out).

## Tests

- **Rules unit tests**: Window > General > Test Runner > EditMode > Run All. They include a fuzz test (`RulesFuzzTests`, the harness
  is `RulesFuzzer`): 400 battles between random and absurd decks (all spies, all medics, musters, weather spam, decoys, a handful of
  cards...) making random moves the way the battle does, with every unit's strength checked against an independent copy of The
  Witcher 3's strength formula, and card conservation, scores, horn slots, the weather board, round ends and round winners checked
  after every move; the AI makes some of those moves, at every difficulty. `AiDifficultyTests` play the AI's 11 decks against each
  other: Normal must beat Easy and Hard must beat Normal, no card may be lost or made, and every deck's active leader must come into
  play. `AiBrainTests` check that every difficulty only makes legal moves, and that Hard's move does not depend on which of your
  unseen cards are in your hand. The content tests (`Gwent.Content.Tests`, run with them) check the Addressables content and that
  every card has the description box it should, with its icon.
- **Autoplay smoke test**: menu **Gwent > Run Autoplay Smoke Test** (or Ctrl+Shift+Alt+G). It plays one battle with the default deck and one with a generated deck for every leader, with a random player against the AI (cycling through the AI's 11 decks and its three difficulties), and fails on errors in the log, on a game that stops progressing, on cards appearing or disappearing, or on a board that shows other cards than the game. Parts of it go through real input (a virtual mouse and keyboard at human speed): the redraw (clicks, keys, Space), card selection, holding Space to pass, the pause menu, the question a hand on the deck asks (giving up), playing a card with the keyboard, the touch controls (long presses on the leader, its button and a hand card; taps), the game speed and difficulty settings, the card picker's keys (and the question Skip asks), a discard pile opened in the carousel, and choosing then using the leader. Then the deck builder opens on a test deck it made: real clicks add and remove cards (the counts follow, a hero already in the deck is no longer offered, an 11th special is refused), go through the weather and specials tabs, change the leader, switch factions and pick the deck in the dropdown, and its API is checked (presets: picked, never renamed, deleted or changed, an edit saved as a copy; renaming, deleting decks, invalid and corrupt files). Last, the main menu: its settings and How to play, then **Play**, the opponent picker and a battle with the deck chosen in the builder, the fallback to the default deck when the battle deck is invalid, and a preset as the battle deck. The run restores the battle deck, the opponent and the settings it changes. The report is written to `Logs/autoplay-report.txt`.
- **Monkey test**: menu **Gwent > Run Monkey Test** (10 minutes). Battles between random AI decks played under a storm of random input:
  a virtual mouse and keyboard click, double-click, right-click, hold, drag and scroll on whatever the screen offers and mash the
  game's keys and others at any moment (during plays, banners, the redraw, pickers, the pause menu, the end screen), switching the
  touch controls, the game speed and the difficulty now and then; when nothing moves for a while, a random legal move or an Escape keeps it going.
  Fails on errors in the log, cards appearing or disappearing, a board that shows other cards than the game when idle, and a game
  that stops progressing. It restores every setting and deck file it may change. The report is written to `Logs/monkey-report.txt`.
- **Touch controls in the editor**: menu **Gwent > Simulate Touch Controls** plays with the phone's controls using the mouse (a click is a tap, a held click is a finger held on a card).

From the command line (close the project in the editor first), with the [Unity CLI](https://docs.unity.com) signed in (`unity auth`):

```bash
unity test GwentCardGame --mode EditMode --output editmode-results.xml
```

The autoplay test needs the editor to stay open after the method returns, so it runs the editor directly (the CLI's `run` always adds `-quit`):

```bash
<path-to>/6000.6.5f1/Editor/Unity -batchmode -nographics -projectPath GwentCardGame -executeMethod Gwent.EditorTools.GwentAutoplay.RunBatch -autoplayReport autoplay-report.txt -logFile autoplay.log
```

## AI agents (MCP)

The project includes the experimental `com.unity.pipeline` package, which lets the Unity CLI talk to a running editor.
To use it from an MCP client such as Claude Code, register the CLI's MCP server and open the project in the editor:

```bash
claude mcp add --scope user unity-editor-mcp -- /absolute/path/to/unity mcp
```

`unity status` lists the editors the server can reach.

## Credits

It goes without saying, but I don't claim ownership of the game idea, the card art or any other material from The Witcher 3: Wild Hunt; all credit goes to its owner, CD PROJEKT RED.
This is a non-commercial fan project.

## License

- The source code is released under the [MIT License](LICENSE).
- The license does not cover the Gwent/Witcher card art, names and other CD PROJEKT RED material, which remain the property of their owners.
- The fonts keep their own licenses: Cinzel and Alegreya Sans (SIL Open Font License 1.1); see `GwentCardGame/Assets/Fonts/FONTS-LICENSE.md`.
- The battle's flow runs on [UniTask](https://github.com/Cysharp/UniTask) by Cysharp (MIT), and the interface's animations on [LitMotion](https://github.com/annulusgames/LitMotion) by Annulus Games (MIT), both fetched by the package manager (`Packages/manifest.json`).
- The battle's smoke, flames, sparks and weather are particles drawn in the interface by [ParticleEffectForUGUI](https://github.com/mob-sakai/ParticleEffectForUGUI) by mob-sakai (MIT), which the package manager fetches too; their textures are generated (**Gwent > UI > Regenerate Particle Art**).
- The opponent decks (`Resources/Decks/AI`) are adapted from the default decks of [gwent-classic](https://github.com/asundr/gwent-classic) by Arun Sundaram (MIT with the Commons Clause), as kept in [gwent-classic-v3.1](https://github.com/RandomPianist/gwent-classic-v3.1); three were changed so that their leader suits their cards (see [The AI](#the-ai)).
- The cards' names, strengths, abilities and copies follow the community's [Gwent Card List](https://docs.google.com/spreadsheets/d/15RUB-aG3Y2mRY3oWyNY1TlfDTRE-ySgNURUfTpMH0XQ/htmlview) spreadsheet, Blood and Wine's Skellige cards included.
- The battle's notification emblems, ability flares, end of battle emblems, gems and row-score circles are The Witcher 3's own interface art, as extracted by gwent-classic (see `GwentCardGame/Assets/Resources/W3/CREDITS.md`); so are Skellige's card back and its abilities' glyphs (Berserker, Mardroeme, Skellige Storm, Avenger), set in this game's icon discs.
- The table under the board is "Dark Wood" from [Poly Haven](https://polyhaven.com/a/dark_wood) (CC0); see `GwentCardGame/Assets/UI/Board/Sources/CREDITS.md`.
- Skellige Storm's thunder is cut from "Rain + Long Thunder" by WuxiaScrub on [OpenGameArt](https://opengameart.org/content/rain-long-thunder) (CC0); see `GwentCardGame/Assets/Resources/Audio/README.txt`.
