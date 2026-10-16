using Gwent.Battle;
using Gwent.Rules;
using UnityEditor;
using UnityEngine;

namespace Gwent.EditorTools
{
    // Gwent > Preview > Weather: in Play mode, during a battle, turns a weather on or off on the board without a card
    // being played, to look at the rows' weather effects as they come, last and go. It changes the battle's own weather
    // (the scores follow); Clear Weather takes it all away, in the sun's light. Lightning Strike strikes now, in a storm.
    public static class WeatherPreview
    {
        private const string Menu = "Gwent/Preview/Weather/";

        [MenuItem(Menu + "Biting Frost")]
        private static void Frost() { Toggle(CardIds.BitingFrost); }

        [MenuItem(Menu + "Impenetrable Fog")]
        private static void Fog() { Toggle(CardIds.ImpenetrableFog); }

        [MenuItem(Menu + "Torrential Rain")]
        private static void Rain() { Toggle(CardIds.TorrentialRain); }

        [MenuItem(Menu + "Skellige Storm")]
        private static void Storm() { Toggle(CardIds.SkelligeStorm); }

        [MenuItem(Menu + "Frost, Fog and Rain")]
        private static void All() { Set(CardIds.BitingFrost, CardIds.ImpenetrableFog, CardIds.TorrentialRain); }

        [MenuItem(Menu + "Clear Weather")]
        private static void Clear() { Set(); }

        [MenuItem(Menu + "Lightning Strike")]
        private static void Lightning() { StormLightning.Strike(); }

        [MenuItem(Menu + "Biting Frost", true)]
        [MenuItem(Menu + "Impenetrable Fog", true)]
        [MenuItem(Menu + "Torrential Rain", true)]
        [MenuItem(Menu + "Skellige Storm", true)]
        [MenuItem(Menu + "Frost, Fog and Rain", true)]
        [MenuItem(Menu + "Clear Weather", true)]
        [MenuItem(Menu + "Lightning Strike", true)]
        private static bool InBattle() { return Application.isPlaying && BattleController.Instance != null; }

        private static void Toggle(int weather)
        {
            BattleController battle = BattleController.Instance;
            if (!battle.weatherList.Remove(weather))
                battle.weatherList.Add(weather);
            battle.ShowBoard();
        }

        private static void Set(params int[] weathers)
        {
            BattleController battle = BattleController.Instance;
            battle.weatherList.Clear();
            battle.weatherList.AddRange(weathers);
            battle.ShowBoard();
        }
    }
}
