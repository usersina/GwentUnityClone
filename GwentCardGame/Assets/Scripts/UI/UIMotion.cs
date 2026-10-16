using Gwent.Core;
using LitMotion;
using LitMotion.Adapters;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.UI
{
    // The interface's small animations, on LitMotion. Each one is bound to its object and stops when it goes, and runs
    // in unscaled time: menus and notices go on while the game is paused. Those that go on and on (Pulse, Breathe,
    // Throb) are Settings > Graphics > Shine and pulsing: off, what they would move holds still, as it is.
    public static class UIMotion
    {
        // A screen fading and settling in, from transparent and slightly smaller
        public static void Appear(GameObject screen)
        {
            CanvasGroup group = Group(screen, true);
            Transform t = screen.transform;
            group.alpha = 0;
            t.localScale = Vector3.one * 0.96f;
            Unscaled(LMotion.Create(0f, 1f, 0.45f).WithEase(Ease.OutQuad))
                .Bind(x => { group.alpha = x; t.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, x); })
                .AddTo(screen);
        }

        // Breathes a graphic's alpha between a share of its own and all of it, to draw the eye to it
        public static void Pulse(Graphic graphic, float low = 0.35f, float period = Mathf.PI / 2)
        {
            if (!GameSettings.Shows(GameSettings.Effect.ShineAndPulsing))
                return;
            float full = graphic.color.a;
            Unscaled(LMotion.Create(low * full, full, period / 2).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo))
                .BindToColorA(graphic).AddTo(graphic);
        }

        // Breathes an element's alpha between low and full (it never catches clicks)
        public static void Breathe(GameObject element, float low, float period)
        {
            if (!GameSettings.Shows(GameSettings.Effect.ShineAndPulsing))
                return;
            Unscaled(LMotion.Create(low, 1f, period / 2).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo))
                .BindToAlpha(Group(element, false)).AddTo(element);
        }

        // Swells a little and back, over and over, to show it waits for a click; cancel the handle to stop it (and set
        // the size back). Off: an inactive handle
        public static MotionHandle Throb(Transform target, float amount = 0.08f, float period = 1.1f)
        {
            if (!GameSettings.Shows(GameSettings.Effect.ShineAndPulsing))
                return default;
            MotionHandle handle = Unscaled(LMotion.Create(1f, 1f + amount, period / 2).WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo))
                .Bind(scale => target.localScale = Vector3.one * scale);
            handle.AddTo(target.gameObject);
            return handle;
        }

        // Fades a graphic out from its alpha, then destroys it
        public static void FlashOut(Graphic graphic, float seconds)
        {
            GameObject go = graphic.gameObject;
            Unscaled(LMotion.Create(graphic.color.a, 0f, seconds).WithOnComplete(() => Object.Destroy(go)))
                .BindToColorA(graphic).AddTo(go);
        }

        private static MotionBuilder<float, NoOptions, FloatMotionAdapter> Unscaled(MotionBuilder<float, NoOptions, FloatMotionAdapter> motion)
        {
            return motion.WithScheduler(MotionScheduler.UpdateIgnoreTimeScale);
        }

        private static CanvasGroup Group(GameObject go, bool blocks_raycasts)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = go.AddComponent<CanvasGroup>();
                group.blocksRaycasts = blocks_raycasts;
            }
            return group;
        }
    }
}
