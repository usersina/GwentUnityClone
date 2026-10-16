GameAudio maps gameplay events to the files already in these folders.

Music:
The tracks are in Assets/Content/Music, loaded through Addressables (label "music"), each when it is to play. GameAudio plays them
in a random order, a track at a time, across scenes.

Core SFX mapping:
ui_click -> menu_buy
invalid -> warning
card_select -> card
card_draw -> card
card_discard -> discard
card_exchange -> redraw
card_play_unit -> random common1/common2/common3
card_play_hero -> hero
card_play_special -> card
pass -> pass
round_end -> round1_start
round_win -> round_win
round_lose -> round_lose
victory -> game_win
defeat -> game_lose
game_start -> game_start
turn_player -> turn_me
turn_enemy -> turn_op

Ability SFX mapping:
ability_spy -> spy
ability_medic -> med
ability_muster -> ally
ability_scorch -> scorch
ability_weather_frost -> cold
ability_weather_fog -> fog
ability_weather_rain -> rain
ability_weather_clear -> clear
ability_horn -> horn
ability_morale -> moral
ability_tight_bond -> ally
ability_decoy -> knockback
ability_mardroeme -> mardroeme (Berserkers turning into bears; gwent-classic's sound, as the others)
weather_thunder -> random thunder1/thunder2 (Skellige Storm's lightning; cut from "Rain + Long Thunder" by WuxiaScrub,
  https://opengameart.org/content/rain-long-thunder, CC0)
leader -> shield

Deck editor SFX mapping:
deck_add -> menu_buy
deck_remove -> discard
deck_select -> card
deck_save -> game_buy
