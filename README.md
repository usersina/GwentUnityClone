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
| Redraw a card at the start (up to 2) | Double-click it, then **Done** (or right-click / Esc to skip) | Double-tap it, then **Done** |
| Select a card from your hand | Click it (click again to deselect) | Tap it |
| Play the selected card | Click a highlighted row (weather/scorch: the weather board, decoy: one of your units) | Same, with taps |
| Use your leader | Click the leader card or its button | Tap |
| Pass | Hold the **Press to pass** button | Hold |
| Preview a card | Hover it | Selecting it shows the preview |
| Deck builder: add/remove a card | Double-click it | Tap it |
| Deck builder: card details | Right-click | - |

The deck you last selected in the deck builder is used in battle, as long as it is valid (at least 22 unit cards, at most 10 special cards,
heroes once, other cards up to 3 times). The line at the bottom of the deck builder shows which deck that is. Without a valid deck,
the battle uses the built-in Northern Realms deck.

House rule: Muster also pulls the matching cards from your hand, not only from your deck (as written on the card).

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
| `Assets/Scripts/AI` | Executes the AI's decisions (always ends its turn, falls back to passing) |
| `Assets/Scripts/DeckController.cs`, `DeckEdit/` | The deck builder |
| `Assets/Scripts/Utility` | Audio, deck files, card database, runtime UI (toasts, dialogs), logging |
| `Assets/Resources/Cards` | `cards_db.json` (every card's stats), `AI_Monsters.json` (the AI's deck) and the card images |
| `Assets/Tests/EditMode` | Unit tests for the rules |
| `Assets/Editor/GwentAutoplay.cs` | End-to-end smoke test that plays full battles |

Saved decks live in `Application.persistentDataPath/Decks/<faction>/<name>.json`.
Gameplay logging (`GameLog.Info`) only runs in the editor and debug builds (or with the `GWENT_VERBOSE_LOGS` define).

## Tests

- **Rules unit tests**: Window > General > Test Runner > EditMode > Run All.
- **Autoplay smoke test**: menu **Gwent > Run Autoplay Smoke Test** (or Ctrl+Shift+Alt+G). It plays one battle with the default deck and one with a generated deck for every leader, with a random player against the AI, and fails on errors in the log, on a game that stops progressing, or on cards appearing or disappearing. The report is written to `Logs/autoplay-report.txt`.

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
