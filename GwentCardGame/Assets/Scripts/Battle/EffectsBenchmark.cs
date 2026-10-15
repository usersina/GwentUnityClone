using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gwent.Battle
{
    // What the battle's heaviest effects cost, measured in a build. Started only by a flag: on the web "?bench=effects" in
    // the address, on Android the launch extra "bench" ("adb shell am start ... --es bench effects"), on the desktop
    // "-bench effects". It opens a battle, lays a hero on your close combat row and three units on the opponent's, then
    // times the frames: first with the board at rest, then while the hero lands and the three burn in a scorch at once
    // (the moment the effects cost most). Three runs; each is logged ("[bench]"), and the result shows as a notice.
    public class EffectsBenchmark : MonoBehaviour
    {
        private const string Flag = "effects";
        private const int Runs = 3;
        private const int Hero = 67;      // Geralt of Rivia
        private const int Unit = 134;     // Vesemir (6)
        private const float RestSeconds = 1.5f, EffectSeconds = 2.8f;

        private static bool started;

        // Called as the menu starts: does nothing unless the flag asks for it
        public static void StartIfRequested()
        {
            if (ReadFlag() == Flag)
                Begin();
        }

        // Runs it (once), from the menu (in the editor, from Play mode)
        public static void Begin()
        {
            if (started)
                return;
            started = true;
            GameObject host = new GameObject("EffectsBenchmark");
            DontDestroyOnLoad(host);
            host.AddComponent<EffectsBenchmark>().Run(host.GetCancellationTokenOnDestroy()).Forget();
        }

        private static string ReadFlag()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = Application.absoluteURL ?? "";
            int at = url.IndexOf("bench=", StringComparison.Ordinal);
            if (at < 0)
                return null;
            string value = url.Substring(at + "bench=".Length);
            int end = value.IndexOfAny(new[] { '&', '#' });
            return end >= 0 ? value.Substring(0, end) : value;
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                    return intent.Call<string>("getStringExtra", "bench");
            }
            catch (Exception)
            {
                return null;
            }
#else
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-bench");
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
#endif
        }

        private struct Frames
        {
            public int Count;
            public float Average, P95, Max;
            public int Particles; // the most alive at once

            public override string ToString()
            {
                return Count + " frames, " + Average.ToString("0.0") + " ms average, " + P95.ToString("0.0") + " ms p95, " + Max.ToString("0.0") + " ms max"
                    + (Particles > 0 ? ", up to " + Particles + " particles" : "");
            }
        }

        private async UniTaskVoid Run(CancellationToken ct)
        {
            SceneManager.LoadScene(SceneNames.Game);
            await UniTask.WaitUntil(() => BattleController.Instance != null, cancellationToken: ct);
            BattleController battle = BattleController.Instance;
            battle.FinishCardExchange();
            await UniTask.WaitUntil(() => battle.battleState == BattleState.PlayerTurn && battle.IsPlayerActionAllowed, cancellationToken: ct);

            StringBuilder summary = new StringBuilder("Effects benchmark (" + Application.platform + ", " + Screen.width + "x" + Screen.height + ")");
            for (int run = 1; run <= Runs; run++)
            {
                Stage(battle, false); // the last run's card objects (charred by its scorch) go
                await Wait(0.3f, ct);
                Stage(battle, true);
                await Wait(1f, ct); // the new card objects show and settle
                Frames rest = await Measure(RestSeconds, null, ct);
                GameObject hero = CardOn(battle, true, Hero).FirstOrDefault();
                RectTransform[] burnt = CardOn(battle, false, Unit).Select(card => (RectTransform)card.transform).ToArray();
                Frames effects = await Measure(EffectSeconds, async () =>
                {
                    UniTask landing = CardEffects.Hero(hero, ct);
                    await Wait(0.3f, ct); // the scorch comes while the hero's fire runs
                    await UniTask.WhenAll(landing, CardEffects.Burn(burnt, ct));
                }, ct);
                string line = "run " + run + ": at rest " + rest + " | hero landing + scorch of " + burnt.Length + ": " + effects;
                Debug.Log("[bench] " + line);
                summary.Append('\n').Append(line);
            }
            Debug.Log("[bench] done\n" + summary);
            UIOverlay.Toast(summary.ToString(), 60f);
        }

        // Clears both sides' rows, then (cards) lays the hero and the three units
        private static void Stage(BattleController battle, bool cards)
        {
            foreach (Row row in Rows.Units)
            {
                battle.PlayerInfo.UnitRow(row).Clear();
                battle.EnemyInfo.UnitRow(row).Clear();
            }
            if (cards)
            {
                battle.PlayerInfo.CloseList.Add(Hero);
                battle.EnemyInfo.CloseList.AddRange(new[] { Unit, Unit, Unit });
            }
            battle.ShowBoard();
        }

        private static IEnumerable<GameObject> CardOn(BattleController battle, bool player, int card_id)
        {
            foreach (Transform card in battle.Board.View(player).Rows[Row.Close].Unit.transform)
            {
                CardFace face = card.GetComponent<CardFace>();
                if (face != null && face.Card._id == card_id)
                    yield return card.gameObject;
            }
        }

        // Times every frame for a while (real time), what is given running meanwhile
        private static async UniTask<Frames> Measure(float seconds, Func<UniTask> meanwhile, CancellationToken ct)
        {
            List<float> times = new List<float>();
            int particles = 0, frame = 0;
            if (meanwhile != null)
                meanwhile().Forget();
            float age = 0;
            await UniTask.Yield(PlayerLoopTiming.Update, ct); // the frame that started it is not counted
            while (age < seconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                float dt = Time.unscaledDeltaTime;
                age += dt;
                times.Add(dt * 1000);
                if (frame++ % 5 == 0) // counted now and then: counting costs too
                    particles = Math.Max(particles, CardMotion.Layer.GetComponentsInChildren<ParticleSystem>().Sum(system => system.particleCount));
            }
            times.Sort();
            return new Frames
            {
                Count = times.Count,
                Average = times.Average(),
                P95 = times[Mathf.Clamp(Mathf.CeilToInt(times.Count * 0.95f) - 1, 0, times.Count - 1)],
                Max = times[times.Count - 1],
                Particles = particles,
            };
        }

        private static UniTask Wait(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);
        }
    }
}
