using UnityEngine;

namespace Gwent.Battle
{
    // Torrential Rain on a row (WeatherEffect), as in The Witcher 3's art of it: many fine, short, faint streaks of falling
    // drops (Streak), thickest in the row's lower two thirds, splashes along its front edge, a few droplets bouncing off
    // them, and a grey haze of spray low in the row, thickening towards the floor (Mist). It starts light and grows heavy
    // as it comes; as it goes, it thins out and stops as it fades. Skellige Storm's is heavier and driven by the wind:
    // faster, slanting, the slant swaying with the gusts, its spray blown along the row. In layout units and game
    // seconds; one row's rain is about 330 particles (a storm's 420).
    internal sealed class RainParticles : WeatherLook
    {
        private const float FallSpeed = 900;          // a drop's: no further a frame than its streak is long, or it flickers
        private const float StormSpeed = 1150, StormSlant = 18; // a storm's (degrees from the vertical, to the right); calm, it falls straight, as in the art
        private const int Drops = 270, Splashes = 13, Haze = 16; // alive at once, at full strength
        private const float DropLife = 0.15f, SplashLife = 0.12f, HazeLife = 6; // on average
        private const float StopSeconds = 0.45f;      // as it goes, from full strength to none (the layer fades meanwhile)
        private const float DropletGravity = 2000;    // the droplets bouncing off the splashes fall back
        private const float BuildSeconds = 0.8f;       // from the first drops to full strength
        private static readonly Color Pale = new Color(0.82f, 0.86f, 0.9f);

        private readonly ParticleSystem haze, drops, splashes, droplets;
        private readonly float width, height;
        private readonly bool storm;
        private readonly float heavy;  // more drops in a storm
        private float slant;           // degrees from the vertical, to the right (a storm's sways)
        private Vector2 fall;
        private float dropDue, splashDue, hazeDue, leftAt = -1; // the particles owed since the last frame; when it went
        private bool leaving;

        public RainParticles(RectTransform layer, bool storm = false)
        {
            width = layer.rect.width;
            height = layer.rect.height;
            this.storm = storm;
            heavy = storm ? 1.3f : 1;
            Gust(0);
            ParticleFx fx = Host("Rain", layer);
            Color clear = new Color(1, 1, 1, 0);
            haze = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.3f, Color.white), (0.7f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.9f), (1, 1.15f)), spin: 0.03f, turbulence: 4, frames: true, swirls: 240, kept_whole: true);
            drops = fx.Add("Streak", ParticleFx.Colors((0, clear), (0.15f, Color.white), (0.8f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 1), (1, 1)), stretched: true);
            splashes = fx.Add("Spark", ParticleFx.Colors((0, Color.white), (1, clear)), ParticleFx.Sizes((0, 0.5f), (1, 1.4f)), stretched: true);
            droplets = fx.Add("Spark", ParticleFx.Colors((0, Color.white), (0.7f, Color.white), (1, clear)), ParticleFx.Sizes((0, 1), (1, 0.6f)), gravity: DropletGravity);

            // The haze is there from the start, its puffs living staggered lives so that it neither thins nor swells
            for (int i = 0; i < Haze; i++)
                EmitHaze(Random.Range(1f, HazeLife + 1));
        }

        private float Floor { get { return -height / 2; } }

        // The way the drops fall: a storm's slant sways with its gusts
        private void Gust(float age)
        {
            slant = storm ? StormSlant + 7 * Mathf.Sin(age * 0.8f) + 3 * Mathf.Sin(age * 2.1f) : 0;
            float angle = slant * Mathf.Deg2Rad;
            fall = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * (storm ? StormSpeed : FallSpeed);
        }

        public override void Tick(float dt, float age)
        {
            Gust(age);
            float strength = Mathf.SmoothStep(0.15f, 1, age / BuildSeconds) * heavy;
            if (leaving)
            {
                if (leftAt < 0)
                    leftAt = age;
                strength *= 1 - Mathf.Clamp01((age - leftAt) / StopSeconds);
            }
            for (dropDue += Drops / DropLife * strength * dt; dropDue >= 1; dropDue--)
                EmitDrop();
            for (splashDue += Splashes / SplashLife * strength * dt; splashDue >= 1; splashDue--)
                EmitSplash();
            if (leaving)
                return;
            for (hazeDue += Haze / HazeLife * dt; hazeDue >= 1; hazeDue--)
                EmitHaze(HazeLife * Random.Range(0.8f, 1.2f));
        }

        // It thins out and stops, its drops falling on as it fades
        public override void Leave()
        {
            leaving = true;
        }

        // Wiped away by the sun: it rains on as it was
        public override void Wiped()
        {
            leaving = false;
        }

        // A drop falling through the row: born anywhere over it, gone after its fall (so that they gather lower in the row),
        // a fine short streak, nearer drops a little longer and brighter
        private void EmitDrop()
        {
            float near = Random.value;
            float life = DropLife * Random.Range(0.6f, 1.4f);
            Vector2 at = new Vector2(Random.Range(-width / 2, width / 2) - fall.x * life / 2, Random.Range(Floor + 10, height / 2 + 10));
            Vector2 size = new Vector2(Mathf.Lerp(1.6f, 2.4f, near), Mathf.Lerp(8f, 15f, near) * (storm ? 1.25f : 1));
            Color color = new Color(Pale.r, Pale.g, Pale.b, Mathf.Lerp(0.2f, 0.45f, near));
            ParticleFx.Emit(drops, at, fall * Mathf.Lerp(0.85f, 1.1f, near), size, life, color, -slant);
        }

        // A drop landing: a small bright splash spreading on the floor, mostly along the row's front edge, now and then a
        // droplet or two bouncing off
        private void EmitSplash()
        {
            bool front = Random.value < 0.8f;
            Vector2 at = new Vector2(Random.Range(-width / 2 + 10, width / 2 - 10), Floor + (front ? Random.Range(7f, 18f) : Random.Range(18f, 60f)));
            float alpha = front ? Random.Range(0.5f, 0.85f) : Random.Range(0.2f, 0.4f);
            ParticleFx.Emit(splashes, at, Vector2.zero, new Vector2(Random.Range(12f, 20f), Random.Range(4f, 7f)),
                SplashLife * Random.Range(0.8f, 1.25f), new Color(Pale.r, Pale.g, Pale.b, alpha));
            for (int i = Random.Range(0, 3); i > 0; i--)
                ParticleFx.Emit(droplets, at, new Vector2(Random.Range(-60f, 60f), Random.Range(105f, 195f)), Random.Range(2.5f, 4f),
                    Random.Range(0.13f, 0.2f), new Color(Pale.r, Pale.g, Pale.b, alpha));
        }

        // The spray: a veil over the row's lower two thirds, thickest at the floor, drifting with the wind; its puffs are
        // large and faint, so that they blend into one
        private void EmitHaze(float life)
        {
            float drift = storm ? Random.Range(25f, 40f) : Random.Range(4f, 8f); // set back by half its drift: it stays centred
            Vector2 at = new Vector2(Random.Range(-width / 2 - 100, width / 2 + 100) - drift * life / 2, Floor + Random.Range(-40f, 10f));
            ParticleFx.Emit(haze, at, new Vector2(drift, Random.Range(-1f, 1f)), Random.Range(420f, 560f), life,
                new Color(0.75f, 0.78f, 0.82f, storm ? Random.Range(0.15f, 0.22f) : Random.Range(0.12f, 0.18f)), Random.Range(-4f, 4f));
        }
    }
}
