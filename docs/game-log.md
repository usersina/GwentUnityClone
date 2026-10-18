# The game log

[Docs](README.md) › The game log

Everything that happens is written to a log of events, one JSON object per line, meant to be read by people and by AI agents
alike.

## Contents

- [Where it is](#where-it-is)
- [What a line holds](#what-a-line-holds)
- [Events](#events)
- [Reading it with jq](#reading-it-with-jq)

## Where it is

`GwentCardGame/Logs/game.jsonl` in the editor (one Play session; the one before is `game.previous.jsonl`),
`<persistentDataPath>/Logs/game.jsonl` in a development build. Each event is also shown on one console line. Release builds have
none of it: the calls are compiled out (`GameLog`, `BattleLog`), so nothing is written; only the editor, development builds
(`DEBUG`) and the `GWENT_VERBOSE_LOGS` define keep them.

## What a line holds

Every line has `t` (seconds since the session began), `f` (frame), `scene`, `cat`, `ev`, and during a battle `battle`, `round`
and `turn`, then the event's own fields.

A play's `effects` say what moved between the zones (`player.hand`, `enemy.close`, `enemy.close_horn`, `player.discard`,
`weather`... as `in` and `out` lists, a deck as a count), so a scorch, a muster or a medic's chain shows as what it did.

## Events

| `cat` | Events |
| --- | --- |
| `game` | `session_start` (version and build, platform, device), `scene`, `scene_loaded` (how long it took), `settings`, `opponent_chosen`, `music`, `autoplay_match`, `autoplay_failure`, `monkey_failure`; LAN: `lan_host`, `lan_join`, `lan_guest`, `lan_battle`, `lan_rematch`, `lan_timeout`, `lan_fair_play` |
| `battle` | `battle_start` (decks, leaders, difficulty), `redraw`, `round_start`, `turn_start` (with `board`: both sides' hands, rows with strengths, decks, lives, leaders, weather), `card_played`, `decoy_played`, `leader_used`, `leader_refused`, `picked`, `card_discarded`, `pass` (and why), `monsters_kept`, `northern_realms_draw`, `skellige_reinforcements`, `berserkers_transformed`, `avenged`, `summon_pending`, `first_turn_chosen`, `redraw_skipped`, `round_end`, `battle_end` (with `gave_up`), `battle_left`; LAN: `command_refused`, `talk` |
| `ai` | `redraw`, `decision` (the move, `think_ms`, and `considered`: the Normal AI's gain, board change and cost of each move; Hard's `win_rate` over its play-outs) |
| `ui` | `dialog_open`, `dialog_close`, `notice`, `toast`, `give_up_asked`, `settings_saved`, `quit` |
| `deck` | `decks_folder`, `battle_deck`, `builder_open`, `deck_added`, `deck_saved`, `deck_renamed`, `deck_deleted`, `card_added`, `card_removed`, `battle_deck_chosen`, `preset_copied` |
| `perf` | `slow_frame` (a frame over 100 ms), `card_art_loaded` |
| `unity` | Unity's own `warning`, `error`, `exception`, `assert`, with `message` and `stack` |

A LAN battle's connection also logs `[lan]` lines to the player's log (`Player.log`, or logcat on Android): the session's steps,
a lost link and its return, why a join was turned away.

## Reading it with jq

A few ways in, with [jq](https://jqlang.org):

```bash
jq -c 'select(.cat=="battle" and (.ev|test("played|leader_used")))|{turn,side,card,row,effects}' GwentCardGame/Logs/game.jsonl
jq -c 'select(.ev=="decision")|{turn,action,think_ms,considered}' GwentCardGame/Logs/game.jsonl
jq -c 'select(.ev=="turn_start" and .turn==5)|.board' GwentCardGame/Logs/game.jsonl
jq -c 'select(.cat=="unity" or .ev=="autoplay_failure")' GwentCardGame/Logs/game.jsonl
```
