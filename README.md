# GwentUnityClone

 The Witcher 3 card game (Gwent Legacy Version), remade in Unity for Windows and Android.
 This is the original repository ([wolfofRome/GwentUnityClone](https://github.com/wolfofRome/GwentUnityClone) is a fork of it).
 Contributions from [Usama171](https://github.com/Usama171) added music, sound effects, better card handling on the battle board and Android support.

 Showcase video: [Youtube video link](https://youtu.be/k31I2_2ARas)

# About this project

This is a fan remake of The Witcher 3's famous card game Gwent.
I'm fully aware that an official standalone version of Gwent is on steam. However I'm enthusiastic about the older version of Gwent and its mechanics which is one of the reasons I got so immersed in The Witcher 3 world. Therefore, here's the original version made nearly from scratch in Unity game engine.

I got the card assets from here: [Gwent Cards Repository (game rules are here if you're unfamiliar)](https://github.com/matt77hias/Gwent)

Every other asset I made myself using Adobe Photoshop.

Some of them are clunky, but the game rules are covered by automated tests (see [Tests](#tests)).

This game supports full deck editing and has an AI playing a Monsters deck.

Sorry, no multiplayer :/ (yet)

# Screenshots:
<h3 align="center">Northern realms vs Monsters</p>

![](/Screenshots/Game2.jpg)
![](/Screenshots/Game.jpg)

<h3 align="center">Deck Editing</p>

![](/Screenshots/DeckEdit.jpg)

# How to play

| Action | Desktop | Android |
|---|---|---|
| Start a battle | **Play**, then choose your opponent (one of 9 decks across the four factions) or **Random opponent**; the choice is remembered | Same, with taps |
| Redraw a card at the start (up to 2) | Click a card in the redraw carousel to bring it forward, click it again (or Enter) to redraw it; arrow keys or the mouse wheel browse. In your hand, a click brings a card forward and a double-click on the front card redraws it (a quick double-click on another card only brings it forward). **Done** or right-click keeps your hand | Tap a card to bring it forward, tap it again to redraw it, then **Done** |
| Select a card from your hand | Click it: it goes to the showcase on the right, with its description, while you choose its place (click its empty slot in the hand to put it back) | Tap it |
| Play the selected card | Click a highlighted row (weather/scorch: the lit weather board, decoy: one of your framed units). The cursor turns gold where the card can go and shows a red "no" where it can't; clicking there plays a warning sound and keeps the card in your hand. Enter plays it when only one place fits | Same, with taps (the warning sound, no cursor) |
| Use your leader | Click the leader card or its button, or press X twice | Tap |
| Browse a discard pile (either side) | Click it (Esc closes) | Tap it |
| Pass (for the rest of the round) | Hold Space, or hold the **Hold to pass** button | Hold the button |
| Scoia'tael: choose who starts | **Go first** or **Go second** in the dialog at the start of the battle | Tap |
| After the battle | The end screen shows every round's score; **Play again** or **Main menu** | Tap |
| Pause (resume, settings, give up) | Esc or the menu button at the top right. **Give up** counts as a loss | Back button or the menu button |
| Preview a card | Hover it | Selecting it shows the preview |
| Deck builder: add/remove a card | Double-click it | Tap it |
| Deck builder: card details | Right-click | - |
| Settings (audio, display, game speed) | Main menu or pause menu > **Settings**. **Game > Game speed** (Slow, Normal, Fast) sets how fast cards move, notices pass and the opponent plays | Same |
| Rules of the game | Main menu > **How to play** (the battle, the board, cards, abilities, special cards, factions, controls) | Same |

The deck you last selected in the deck builder is used in battle, as long as it is valid (at least 22 unit cards, at most 10 special cards,
and each card at most as many times as The Witcher 3 has copies of it: heroes and most cards once, up to 5 Mahakaman Defenders). The line
at the bottom of the deck builder shows which deck that is. Without a valid deck, the battle uses the built-in Northern Realms deck. A deck
saved with more copies than that loses the extra ones when it is loaded.

## Rules, as in The Witcher 3

- Each turn you do one thing: play a card, use your leader or pass. Playing a card ends your turn. Passing is final for the round:
  the other side keeps playing until it passes too, then the higher total wins the round. A side with no card left and no leader
  ability to use passes automatically.
- The winner of a round starts the next one (after a drawn round, whoever passed last). Scoia'tael decide who starts the battle.
- A leader ability that would do nothing right now (no weather to clear, an enemy row under 10 for a scorch, no matching weather card
  in the deck...) can't be used, so it is never wasted. Passive leaders (The Treacherous, Invader of the North, The White Flame,
  Daisy of the Valley) work from the start; The White Flame cancels the other leader, passive ones included.
- A decoy on a row is not a unit: morale, horns and scorch ignore it, and Monsters never keep it (nor a hero) at the end of a round.
- The Cow summons the Bovine Defense Force when it goes to the discard pile; the Defense Force arrives when the board is cleared for
  the next round, and vanishes instead of going to the discard pile.
- Muster also pulls the matching cards from your hand, not only from your deck (The Witcher 3 does this too, although the card only
  says "deck"). The Arachas Behemoth calls the Arachas and Gaunter O'Dimm calls the Darkness, but not the other way round.

Differences that remain: the Ice Giant is 6 strength as on its card (5 in The Witcher 3), the Eredin leader cards carry swapped names
on their art (their abilities are right), and the Skellige faction (Blood and Wine) is not in the game.

# Download:
If you don't have Unity Game Engine or simply want to test this game for fun.

Here's the google drive for the installer: [Google Drive Link](https://drive.google.com/file/d/1WsCp0E4xeTDZUHbzHgFfujBZ5r5Jd4t0/view?usp=sharing)

##### (If the installer fails, simply disable any antivirus software as it is made without a digital license using Inno Setup)

Builds are not committed to the repository (the `Builds/` folder is git-ignored); build them from Unity (File > Build Settings) for Windows or Android.

# Development

Open `GwentCardGame/` with **Unity 6.6 (6000.6.5f1)**. The project was upgraded from 2019.4; the last 2019.4 commit is `a228947`.
Rendering uses the Universal Render Pipeline (URP 17.6, settings in `Assets/Settings`). The game is all UI, so HDR, MSAA, shadows and post-processing are off.

| Folder | What's inside |
|---|---|
| `Assets/Scripts/Rules` | The game rules as plain C# with no Unity dependency (`Gwent.Rules` assembly): card data, strength calculation (weather, tight bond, morale, horns), scorch, muster, medic, agile, rounds, deck validation and the AI's decisions |
| `Assets/Scripts/SceneController.cs` | The battle: turns, rounds and the board UI, built on top of the rules |
| `Assets/Scripts/Battle` | The board: `BoardView` draws it from the model, `CardLane` lays out the hand and the rows in The Witcher 3's order, and `BoardPresenter` plays each play as The Witcher 3 does (the showcase, the card gliding in from the board's edge, a hero's fire running around its edge, ability flares, scorch flames, the medic's green sign on the discard pile, cards flying from the deck, a lost round's gem breaking) before the turn passes |
| `Assets/Scripts/AI` | Executes the AI's decisions (always ends its turn, falls back to passing) |
| `Assets/Scripts/DeckController.cs`, `DeckEdit/` | The deck builder |
| `Assets/Scripts/Utility` | Audio, settings, deck files, card database, runtime UI layer (toasts, dialogs, Escape handling), logging |
| `Assets/Scripts/UI` | The menus' look: theme (`Resources/UITheme.asset`: fonts, colors, sprites), UI kit (buttons, menu entries, sliders, switches, steppers), settings screen, pause menu |
| `Assets/Editor/UISpriteGenerator.cs` | Draws the UI sprites (`Assets/UI/Sprites`) and the board pieces (`Assets/UI/Board`: score discs, gems, portrait frame, card shadow, row frames, the table): **Gwent > UI > Regenerate Sprites and Theme** |
| `Assets/Resources/Cards` | `cards_db.json` (every card's stats), `AI_Monsters.json` (the AI's deck) and the card images |
| `Assets/Tests/EditMode` | Unit tests for the rules |
| `Assets/Editor/GwentAutoplay.cs` | End-to-end smoke test that plays full battles |

Saved decks live in `Application.persistentDataPath/Decks/<faction>/<name>.json`; settings are saved in `PlayerPrefs`.
Gameplay logging (`GameLog.Info`) only runs in the editor and debug builds (or with the `GWENT_VERBOSE_LOGS` define).

## Tests

- **Rules unit tests**: Window > General > Test Runner > EditMode > Run All.
- **Autoplay smoke test**: menu **Gwent > Run Autoplay Smoke Test** (or Ctrl+Shift+Alt+G). It plays one battle with the default deck and one with a generated deck for every leader, with a random player against the AI, and fails on errors in the log, on a game that stops progressing, or on cards appearing or disappearing. Parts of it go through real input (a virtual mouse and keyboard at human speed): the redraw, card selection, the pause menu and the main menu's settings. The report is written to `Logs/autoplay-report.txt`.

From the command line (close the project in the editor first), with the [Unity CLI](https://docs.unity.com) signed in (`unity auth`):

```bash
unity test GwentCardGame --mode EditMode --output editmode-results.xml
```

The autoplay test needs the editor to stay open after the method returns, so it runs the editor directly (the CLI's `run` always adds `-quit`):

```bash
<path-to>/6000.6.5f1/Editor/Unity -batchmode -nographics -projectPath GwentCardGame -executeMethod GwentAutoplay.RunBatch -autoplayReport autoplay-report.txt -logFile autoplay.log
```

## AI agents (MCP)

The project includes the experimental `com.unity.pipeline` package, which lets the Unity CLI talk to a running editor.
To use it from an MCP client such as Claude Code, register the CLI's MCP server and open the project in the editor:

```bash
claude mcp add --scope user unity-editor-mcp -- /absolute/path/to/unity mcp
```

`unity status` lists the editors the server can reach.

# Credits:
It naturally goes without saying, but I don't claim ownership of the game idea, the card assets or any other materials related to The Witcher 3 Wild Hunt, all credits goes to the owners CD PROJEKT RED.
This is a non-commercial fan project.

# License
The source code is released under the [MIT License](LICENSE).
The license does not cover the Gwent/Witcher card art, names and other CD PROJEKT RED material, which remain the property of their owners.
The fonts keep their own licenses: Cinzel and Alegreya Sans (SIL Open Font License 1.1), Roboto (Apache 2.0); see `GwentCardGame/Assets/Fonts/FONTS-LICENSE.md`.
The opponent decks (`Resources/Decks/AI`) are adapted from the default decks of [gwent-classic](https://github.com/asundr/gwent-classic) by Arun Sundaram (MIT with the Commons Clause), as kept in [gwent-classic-v3.1](https://github.com/RandomPianist/gwent-classic-v3.1).
The battle's notification emblems, ability flares, gems and row-score circles are The Witcher 3's own interface art, as extracted by gwent-classic (see `GwentCardGame/Assets/Resources/W3/CREDITS.md`).
The table under the board is "Dark Wood" from [Poly Haven](https://polyhaven.com/a/dark_wood) (CC0); see `GwentCardGame/Assets/UI/Board/Sources/CREDITS.md`.
