using System.Collections.Generic;
using Gwent.Core;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A row's weather, laid over the row's horn slot and lane and their cards, as The Witcher 3 does. It fades in with its
    // weather card and out once cleared (half a second, as gwent-classic does). Everything of it is drawn in one layer that
    // clips to the row (its ends softened) and fades as one: nothing leaves the row, however it moves. What the layer shows
    // is a WeatherLook, particles: Biting Frost's ice (FrostParticles), Impenetrable Fog rolling along the row
    // (FogParticles), Torrential Rain falling on it (RainParticles). Skellige Storm's fog and rain are wilder, and its
    // lightning flashes over them (StormFlash, StormLightning). In game time: the pause menu stops it.
    public class WeatherEffect : MonoBehaviour
    {
        public enum Kind { Frost, Fog, Rain }

        private const float FadeSeconds = 0.5f;

        private CanvasGroup group; // the whole layer's fade
        private RectMask2D clip;
        private WeatherLook look;
        private StormFlash flash;  // a storm's lightning
        private bool storm;
        private float age, fade; // fade: 0 (gone) to 1 (shown)
        private bool leaving;
        private float wipe = -1;   // as Clear Weather's sun wipes it away: how far through (0 to 1; -1: it fades)

        // Shows the weather over a row (its horn slot and its lane, whose parent is the row), or lets it fade out. A
        // storm's look is its own: a calm one there gives way to it, crossfading. True when weather began to leave the row.
        public static bool Show(RectTransform slot, RectTransform lane, Kind kind, bool on, bool storm = false)
        {
            WeatherEffect current = null;
            foreach (WeatherEffect effect in lane.parent.GetComponentsInChildren<WeatherEffect>())
                if (!effect.leaving)
                    current = effect;
            if (current != null && (!on || current.storm != storm))
            {
                current.Leave();
                if (!on)
                    return true;
                current = null;
            }
            if (on && current == null)
                Create(slot, lane, kind, storm);
            return false;
        }

        // A layer over a row (its horn slot and its lane together), drawn after both and clipped to them, its ends softened
        // (the row's edge and score stay over it)
        internal static RectTransform RowLayer(string name, RectTransform slot, RectTransform lane)
        {
            Transform row = lane.parent;
            RectTransform root = UIKit.Rect(name, row);
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
            root.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(24, 6); // the ends, the top and bottom
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; // the cards under it stay clickable
            group.interactable = false;
            return root;
        }

        private static void Create(RectTransform slot, RectTransform lane, Kind kind, bool storm)
        {
            RectTransform root = RowLayer("Weather", slot, lane);
            WeatherEffect effect = root.gameObject.AddComponent<WeatherEffect>();
            effect.group = root.GetComponent<CanvasGroup>();
            effect.clip = root.GetComponent<RectMask2D>();
            effect.group.alpha = 0;
            effect.storm = storm;
            effect.look = kind == Kind.Frost ? new FrostParticles(root)
                : kind == Kind.Fog ? new FogParticles(root, storm)
                : (WeatherLook)new RainParticles(root, storm);
            effect.look.Hold();
            if (storm)
            {
                effect.flash = new StormFlash(root);
                StormLightning.Join(effect);
            }
        }

        // The weather leaving goes in Clear Weather's sun instead of fading: wiped away from the left, just behind its light
        internal void Wipe()
        {
            if (leaving && wipe < 0)
            {
                wipe = 0;
                look.Wiped();
                clip.softness = new Vector2Int(90, clip.softness.y); // a soft edge
            }
        }

        // Lightning: this row flashes, a bolt striking in it or not
        internal void Strike(bool bolt)
        {
            if (flash != null && !leaving)
                flash.Strike(bolt);
        }

        private void Leave()
        {
            leaving = true;
            look.Leave();
            StormLightning.Quit(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (wipe >= 0)
            {
                wipe = Mathf.Clamp01(wipe + dt / SunSweep.SweepSeconds);
                float width = ((RectTransform)transform).rect.width;
                clip.padding = new Vector4(Mathf.Clamp(SunSweep.Along(wipe, width) + width / 2 - 60, 0, width), 0, 0, 0);
            }
            else
                fade = Mathf.Clamp01(fade + (leaving ? -dt : dt) / FadeSeconds);
            group.alpha = fade * fade * (3 - 2 * fade); // eased
            look.Tick(dt, age);
            look.Hold();
            if (storm && !leaving)
                StormLightning.Tick();
            if (flash != null)
                flash.Tick(dt);
            if (leaving && (fade <= 0 || wipe >= 1))
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            StormLightning.Quit(this);
        }
    }

    // Skellige Storm's lightning over the whole board: every 3 to 7 seconds of game time, every stormy row flashes at once,
    // a bolt strikes in one of them and it thunders (Strike: now, for the editor's preview)
    public static class StormLightning
    {
        private static readonly List<WeatherEffect> rows = new List<WeatherEffect>();
        private static float next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            rows.Clear();
        }

        internal static void Join(WeatherEffect row)
        {
            rows.RemoveAll(r => r == null);
            if (rows.Count == 0)
                next = Time.time + Random.Range(1f, 2.5f); // soon after the storm comes
            rows.Add(row);
        }

        internal static void Quit(WeatherEffect row)
        {
            rows.Remove(row);
        }

        internal static void Tick()
        {
            if (Time.time >= next)
                Strike();
        }

        public static void Strike()
        {
            rows.RemoveAll(r => r == null);
            if (rows.Count == 0)
                return;
            next = Time.time + Random.Range(3f, 7f);
            int bolt = Random.Range(0, rows.Count);
            for (int i = 0; i < rows.Count; i++)
                rows[i].Strike(i == bolt);
            GameAudio.PlaySfx("weather_thunder");
        }
    }

    // Lightning over a stormy row: the row flashes from above (Flash: the cold's glow turned over, added on) and a forked
    // bolt (Bolt) may strike from its top to its floor, flickering as lightning does: a flash, a dip, a second flash,
    // then dying away
    internal sealed class StormFlash
    {
        private const float Seconds = 0.45f;
        private static readonly Color Light = new Color(0.6f, 0.75f, 1f);

        private readonly RawImage glow, bolt;
        private readonly float width, height;
        private float age = -1; // since the strike (-1: none)

        public StormFlash(RectTransform layer)
        {
            width = layer.rect.width;
            height = layer.rect.height;
            glow = Image(layer, "Flash", "Band");
            glow.rectTransform.Place(new Vector2(0.5f, 1), Vector2.zero, new Vector2(width + 100, height * 1.3f)); // the row's upper half
            glow.rectTransform.localEulerAngles = new Vector3(0, 0, 180); // its glow from the top down
            bolt = Image(layer, "Bolt", "Bolt");
        }

        private static RawImage Image(RectTransform layer, string material, string art)
        {
            RawImage image = UIKit.Rect(material, layer).gameObject.AddComponent<RawImage>();
            image.material = Resources.Load<Material>("W3/Particles/" + material);
            image.texture = ParticleFx.Art(art);
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        public void Strike(bool with_bolt)
        {
            age = 0;
            bolt.enabled = with_bolt;
            if (!with_bolt)
                return;
            // From above the row to its floor, somewhere along its lane, leaning a little; one of the four bolts
            float tall = height * Random.Range(1.05f, 1.25f);
            bolt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(Random.Range(-0.35f, 0.4f) * width, height / 2 - tall / 2 + 4),
                new Vector2(tall * 0.5f, tall));
            bolt.rectTransform.localEulerAngles = new Vector3(0, 0, Random.Range(-12f, 12f));
            int cell = Random.Range(0, 4);
            bolt.uvRect = new Rect((cell % 2) * 0.5f, (cell / 2) * 0.5f, 0.5f, 0.5f);
        }

        public void Tick(float dt)
        {
            if (age < 0)
                return;
            age += dt;
            float light = age < Seconds ? Flicker(age) : 0;
            glow.color = new Color(Light.r, Light.g, Light.b, 0.5f * light);
            bolt.color = new Color(Light.r, Light.g, Light.b, Mathf.Min(1, 1.3f * light));
            if (age >= Seconds)
                age = -1;
        }

        private static float Flicker(float t)
        {
            if (t < 0.04f)
                return t / 0.04f;
            if (t < 0.09f)
                return Mathf.Lerp(1, 0.2f, (t - 0.04f) / 0.05f);
            if (t < 0.14f)
                return Mathf.Lerp(0.2f, 0.85f, (t - 0.09f) / 0.05f);
            return 0.85f * Mathf.Exp(-(t - 0.14f) / 0.06f);
        }
    }

    // Clear Weather's sun breaking through over a row that had weather: a warm shaft of light (Beam, added on) sweeps along
    // it from left to right, wiping the weather away just behind it (WeatherEffect.Wipe), golden motes rising in its wake.
    // In game time.
    public class SunSweep : MonoBehaviour
    {
        public const float SweepSeconds = 1f;
        private const float MotesLive = 1.2f;
        private static readonly Color Sun = new Color(1f, 0.82f, 0.5f);

        private RawImage beam;
        private ParticleFx fx;
        private ParticleSystem motes;
        private float width, height, age, moteDue;

        // Over a row (its horn slot and lane), under the weather leaving it: the light glows through the weather still
        // there (over it, light and fog would add up to a glare)
        public static void Over(RectTransform slot, RectTransform lane)
        {
            RectTransform root = WeatherEffect.RowLayer("Sun", slot, lane);
            foreach (WeatherEffect weather in lane.parent.GetComponentsInChildren<WeatherEffect>())
                weather.Wipe();
            SunSweep sun = root.gameObject.AddComponent<SunSweep>();
            sun.width = root.rect.width;
            sun.height = root.rect.height;
            sun.beam = UIKit.Rect("Beam", root).gameObject.AddComponent<RawImage>();
            sun.beam.material = Resources.Load<Material>("W3/Particles/Beam");
            sun.beam.texture = ParticleFx.Art("Beam");
            sun.beam.raycastTarget = false;
            sun.beam.color = Color.clear;
            sun.beam.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(Along(0, sun.width), 0), new Vector2(300, sun.height * 1.7f));
            sun.beam.rectTransform.localEulerAngles = new Vector3(0, 0, -18); // the light slants in
            sun.fx = ParticleFx.In("Motes", root);
            Color clear = new Color(1, 1, 1, 0);
            sun.motes = sun.fx.Add("Spark", ParticleFx.Colors((0, clear), (0.2f, Color.white), (1, clear)), ParticleFx.Sizes((0, 1), (1, 0.4f)),
                gravity: -25, turbulence: 10);
        }

        // Where the light is (from the row's middle) how far through its sweep (0 to 1)
        public static float Along(float t, float width)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(-width / 2 - 150, width / 2 + 200, t * t * (3 - 2 * t));
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            float t = Mathf.Clamp01(age / SweepSeconds);
            float x = Along(t, width);
            beam.rectTransform.anchoredPosition = new Vector2(x, 0);
            beam.color = new Color(Sun.r, Sun.g, Sun.b, 0.55f * Mathf.Sin(Mathf.PI * t));
            if (age < SweepSeconds)
            {
                for (moteDue += 60 * dt; moteDue >= 1; moteDue--)
                    ParticleFx.Emit(motes, new Vector2(x + Random.Range(-110f, 50f), Random.Range(-height / 2, height / 3)),
                        new Vector2(Random.Range(-8f, 8f), Random.Range(5f, 20f)), Random.Range(4f, 8f), Random.Range(0.6f, MotesLive),
                        new Color(1f, 0.85f, 0.55f, Random.Range(0.4f, 0.8f)));
                fx.Hold();
            }
            if (age > SweepSeconds + MotesLive)
                Destroy(gameObject);
        }
    }

    // What a row's weather shows in its layer (WeatherEffect): made over the layer, run every frame (dt and age in game
    // seconds), told when the weather goes (the layer then fades it out). Its particles are in its hosts' systems, held on
    // pause once it has emitted (ParticleFx.Hold: they age and move at the game's pace).
    internal abstract class WeatherLook
    {
        private readonly List<ParticleFx> hosts = new List<ParticleFx>();
        private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];

        // A particle host over the layer, drawn over what the layer had before it
        protected ParticleFx Host(string name, RectTransform layer)
        {
            ParticleFx fx = ParticleFx.In(name, layer);
            hosts.Add(fx);
            return fx;
        }

        public virtual void Tick(float dt, float age) { }

        public void Hold()
        {
            foreach (ParticleFx fx in hosts)
                fx.Hold();
        }

        public virtual void Leave() { }

        // Told once it has been told to leave, when Clear Weather's sun wipes it away instead of it fading: it holds as it
        // was until the light has passed over it
        public virtual void Wiped() { }

        protected delegate void ParticleChange(ref ParticleSystem.Particle particle, int index);

        // Changes each of a system's particles (index: in the order they were born, while none has died)
        protected void Change(ParticleSystem system, ParticleChange change)
        {
            int count = system.particleCount;
            if (buffer.Length < count)
                buffer = new ParticleSystem.Particle[count * 2];
            count = system.GetParticles(buffer, count);
            for (int i = 0; i < count; i++)
                change(ref buffer[i], i);
            system.SetParticles(buffer, count);
        }

        // Particles rising (billowing up as a weather comes) slow to a halt, drag being how fast (1/s); those barely
        // moving up are left be. False once none rises.
        protected bool Settle(ParticleSystem system, float drag, float dt)
        {
            bool moving = false;
            float slow = Mathf.Exp(-drag * dt);
            Change(system, (ref ParticleSystem.Particle particle, int index) =>
            {
                Vector3 velocity = particle.velocity;
                if (velocity.y <= 1)
                    return;
                velocity.y *= slow;
                particle.velocity = velocity;
                moving = true;
            });
            return moving;
        }
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
