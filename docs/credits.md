# Credits

[Docs](README.md) › Credits

It goes without saying, but I don't claim ownership of the game idea, the card art or any other material from The Witcher 3: Wild
Hunt; all credit goes to its owner, CD PROJEKT RED. This is a non-commercial fan project.

## Contents

- [The game's material](#the-games-material)
- [Code and packages](#code-and-packages)
- [Art, fonts and sound](#art-fonts-and-sound)
- [License](#license)

## The game's material

- Card art & rules come from [matt77hias/Gwent](https://github.com/matt77hias/Gwent).
- Skellige's cards (Blood and Wine) come from Black Swan's English print set, in the same style, as gathered by the community's
  [printing guide](https://www.reddit.com/r/gwent/comments/4rabye/another_guide_for_printing_gwent_cards_complete/) (King Bran with
  Black Swan's own fix).
- The cards' names, strengths, abilities and copies follow the community's
  [Gwent Card List](https://docs.google.com/spreadsheets/d/15RUB-aG3Y2mRY3oWyNY1TlfDTRE-ySgNURUfTpMH0XQ/htmlview) spreadsheet,
  Blood and Wine's Skellige cards included.
- The opponent decks (`Resources/Decks/AI`) are adapted from the default decks of
  [gwent-classic](https://github.com/asundr/gwent-classic) by Arun Sundaram (MIT with the Commons Clause), as kept in
  [gwent-classic-v3.1](https://github.com/RandomPianist/gwent-classic-v3.1); three were changed so that their leader suits their
  cards (see [The AI](ai.md#the-ais-decks)).
- The battle's notification emblems, ability flares, end of battle emblems, gems and row-score circles are The Witcher 3's own
  interface art, as extracted by gwent-classic (see `GwentCardGame/Assets/Resources/W3/CREDITS.md`); so are Skellige's card back and
  its abilities' glyphs (Berserker, Mardroeme, Skellige Storm, Avenger), set in this game's icon discs.

## Code and packages

- The battle's flow runs on [UniTask](https://github.com/Cysharp/UniTask) by Cysharp (MIT), and the interface's animations on
  [LitMotion](https://github.com/annulusgames/LitMotion) by Annulus Games (MIT), both fetched by the package manager
  (`Packages/manifest.json`).
- The battle's smoke, flames, sparks and weather are particles drawn in the interface by
  [ParticleEffectForUGUI](https://github.com/mob-sakai/ParticleEffectForUGUI) by mob-sakai (MIT), which the package manager fetches
  too; their textures are generated (**Gwent > UI > Regenerate Particle Art**).

## Art, fonts and sound

- I made the original interface in Adobe Photoshop. Today most of it is drawn by the game's own sprite generator
  (`Assets/Editor/UISpriteGenerator.cs`), next to The Witcher 3's interface art above.
- The fonts keep their own licenses: Cinzel and Alegreya Sans (SIL Open Font License 1.1); see
  `GwentCardGame/Assets/Fonts/FONTS-LICENSE.md`.
- The table under the board is "Dark Wood" from [Poly Haven](https://polyhaven.com/a/dark_wood) (CC0); see
  `GwentCardGame/Assets/UI/Board/Sources/CREDITS.md`.
- Skellige Storm's thunder is cut from "Rain + Long Thunder" by WuxiaScrub on
  [OpenGameArt](https://opengameart.org/content/rain-long-thunder) (CC0); see `GwentCardGame/Assets/Resources/Audio/README.txt`.

## License

The source code is released under the [MIT License](../LICENSE). The license does not cover the Gwent/Witcher card art, names and
other CD PROJEKT RED material, which remain the property of their owners.
