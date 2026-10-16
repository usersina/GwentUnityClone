using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A row's weather, laid over the row's horn slot and lane and their cards, as The Witcher 3 does. It fades in with its
    // weather card and out once cleared (half a second, as gwent-classic does). Everything of it is drawn in one layer that
    // clips to the row (its ends softened) and fades as one: nothing leaves the row, however it moves. What the layer shows
    // is a WeatherLook: Impenetrable Fog rolls along the row (FogParticles) and Torrential Rain falls on it (RainParticles)
    // as particles; Biting Frost is still The Witcher 3's own art of it (StillWeather). In game time: the pause menu stops
    // it.
    public class WeatherEffect : MonoBehaviour
    {
        public enum Kind { Frost, Fog, Rain }

        private const float FadeSeconds = 0.5f;
        private static readonly Vector2Int EdgeSoftness = new Vector2Int(24, 6); // the clip's soft edges: the ends, the top and bottom

        private CanvasGroup group; // the whole layer's fade
        private WeatherLook look;
        private float age, fade; // fade: 0 (gone) to 1 (shown)
        private bool leaving;

        // Shows the weather over a row (its horn slot and its lane, whose parent is the row), or lets it fade out
        public static void Show(RectTransform slot, RectTransform lane, Kind kind, bool on)
        {
            WeatherEffect current = null;
            foreach (WeatherEffect effect in lane.parent.GetComponentsInChildren<WeatherEffect>())
                if (!effect.leaving)
                    current = effect;
            if (on && current == null)
                Create(slot, lane, kind);
            else if (!on && current != null)
                current.Leave();
        }

        private static void Create(RectTransform slot, RectTransform lane, Kind kind)
        {
            // Over the slot and the lane together, drawn after both (the row's edge and score stay over it)
            Transform row = lane.parent;
            RectTransform root = UIKit.Rect("Weather", row);
            root.SetSiblingIndex(Mathf.Max(slot.GetSiblingIndex(), lane.GetSiblingIndex()) + 1);
            Bounds bounds = new Bounds(row.InverseTransformPoint(lane.TransformPoint(lane.rect.center)), Vector3.zero);
            foreach (RectTransform part in new[] { slot, lane })
            {
                bounds.Encapsulate(row.InverseTransformPoint(part.TransformPoint(part.rect.min)));
                bounds.Encapsulate(row.InverseTransformPoint(part.TransformPoint(part.rect.max)));
            }
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0);
            root.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
            root.gameObject.AddComponent<RectMask2D>().softness = EdgeSoftness;
            WeatherEffect effect = root.gameObject.AddComponent<WeatherEffect>();
            effect.group = root.gameObject.AddComponent<CanvasGroup>();
            effect.group.blocksRaycasts = false; // the cards under it stay clickable
            effect.group.interactable = false;
            effect.group.alpha = 0;
            effect.look = kind == Kind.Fog ? new FogParticles(root)
                : kind == Kind.Rain ? new RainParticles(root)
                : (WeatherLook)new StillWeather(root, kind);
        }

        private void Leave()
        {
            leaving = true;
            look.Leave();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            fade = Mathf.Clamp01(fade + (leaving ? -dt : dt) / FadeSeconds);
            group.alpha = fade * fade * (3 - 2 * fade); // eased
            look.Tick(dt, age);
            if (leaving && fade <= 0)
                Destroy(gameObject);
        }
    }

    // What a row's weather shows in its layer (WeatherEffect): made over the layer, run every frame (dt and age in game
    // seconds), told when the weather goes (the layer then fades it out)
    internal abstract class WeatherLook
    {
        public virtual void Tick(float dt, float age) { }

        public virtual void Leave() { }
    }

    // The Witcher 3's own art of the weather (Biting Frost's ice, Impenetrable Fog's clouds, Torrential Rain), a capture of
    // a whole row, laid still over it: its horn and emblem on the row's own
    internal sealed class StillWeather : WeatherLook
    {
        public StillWeather(RectTransform layer, WeatherEffect.Kind kind)
        {
            string name = kind == WeatherEffect.Kind.Frost ? "frost" : kind == WeatherEffect.Kind.Fog ? "fog" : "rain";
            UIKit.NewImage(layer, "Art", SpriteCache.Load("Cards/EffectBox/overlay_" + name), Color.white).rectTransform.Stretch();
        }
    }
}
