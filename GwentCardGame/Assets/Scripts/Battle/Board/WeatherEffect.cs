using System.Threading;
using Cysharp.Threading.Tasks;
using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A row's weather, as The Witcher 3 draws it: its own art of the weather (Biting Frost's ice, Impenetrable Fog's
    // clouds, Torrential Rain), a capture of a whole row, laid over the row's horn slot and lane and their cards. It fades in with its weather card and out once cleared
    // (half a second, as gwent-classic does). The fog billows up as it comes, then drifts and swells for as long as it
    // lasts, as in The Witcher 3; the frost and the rain keep still. In game time: the pause menu stops them.
    public class WeatherEffect : MonoBehaviour
    {
        public enum Kind { Frost, Fog, Rain }

        private const float FadeSeconds = 0.5f;
        private const float ArriveSeconds = 0.9f; // the fog's billowing arrival

        private Kind kind;
        private Image art;
        private Image drift; // the fog's second layer, mirrored, swelling and fading in its own time
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
                current.leaving = true;
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
            WeatherEffect effect = root.gameObject.AddComponent<WeatherEffect>();
            effect.kind = kind;
            Sprite sprite = SpriteCache.Load("Cards/EffectBox/overlay_" + (kind == Kind.Frost ? "frost" : kind == Kind.Fog ? "fog" : "rain"));
            effect.art = Layer(root, "Art", sprite);
            if (kind == Kind.Fog)
            {
                effect.drift = Layer(root, "Drift", sprite);
                effect.drift.rectTransform.localScale = new Vector3(-1, 1, 1); // mirrored: its clouds are elsewhere
            }
            effect.Apply();
        }

        // The art over the whole row, from its bottom edge (the fog rises from there)
        private static Image Layer(RectTransform root, string name, Sprite sprite)
        {
            Image image = UIKit.NewImage(root, name, sprite, new Color(1, 1, 1, 0));
            image.rectTransform.Stretch();
            image.rectTransform.pivot = new Vector2(0.5f, 0);
            image.rectTransform.anchoredPosition = Vector2.zero;
            return image;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            fade = Mathf.Clamp01(fade + (leaving ? -dt : dt) / FadeSeconds);
            Apply();
            if (leaving && fade <= 0)
                Destroy(gameObject);
        }

        private void Apply()
        {
            if (art == null)
                return;
            float shown = fade * fade * (3 - 2 * fade); // eased
            if (kind != Kind.Fog)
            {
                art.color = new Color(1, 1, 1, shown);
                return;
            }

            // The fog billows up from the row's floor as it comes, thick at first, then settles into a drifting layer
            float arrive = Mathf.Clamp01(age / ArriveSeconds);
            float rise = 1 - (1 - arrive) * (1 - arrive);
            float swell = 1 + 0.18f * Mathf.Sin(Mathf.PI * arrive) * (1 - arrive * 0.5f);
            art.rectTransform.localScale = new Vector3(1, Mathf.Lerp(0.55f, 1f, rise) * swell, 1);
            art.color = new Color(1, 1, 1, shown);

            // Its second layer drifts the other way and swells and thins slowly: the clouds seem to roll along the row
            float wave = Mathf.Sin(age * 0.9f) * 0.5f + 0.5f;
            float width = ((RectTransform)transform).rect.width;
            drift.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(age * 0.35f) * width * 0.03f, 0);
            art.rectTransform.anchoredPosition = new Vector2(-Mathf.Sin(age * 0.27f) * width * 0.02f, 0);
            drift.rectTransform.localScale = new Vector3(-1, Mathf.Lerp(0.55f, 1f, rise) * (0.9f + 0.15f * wave), 1);
            drift.color = new Color(1, 1, 1, shown * Mathf.Lerp(0.2f, 0.55f, wave) * rise);
        }
    }
}
