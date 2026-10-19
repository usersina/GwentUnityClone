using System.Collections.Generic;
using UnityEngine;

namespace Gwent.Core
{
    // The phone's screen kept on while something needs it: a battle on screen (BattleController), a LAN room or battle
    // (LanSession). Left untouched through the other side's turn, a phone would sleep, and the game with it: on LAN that is
    // a player away, whom the other may claim the win from after a minute. Once nothing holds it, the phone's own sleep
    // setting is back. Desktops and browsers ignore it.
    public static class ScreenAwake
    {
        private static readonly HashSet<object> holders = new HashSet<object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            holders.Clear();
            Apply();
        }

        public static void Hold(object holder)
        {
            if (holders.Add(holder))
                Apply();
        }

        public static void Release(object holder)
        {
            if (holders.Remove(holder))
                Apply();
        }

        private static void Apply()
        {
            Screen.sleepTimeout = holders.Count > 0 ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }
    }
}
