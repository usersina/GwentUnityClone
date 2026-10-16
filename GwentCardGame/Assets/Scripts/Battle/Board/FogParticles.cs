using UnityEngine;

namespace Gwent.Battle
{
    // Impenetrable Fog rolling over a row (WeatherEffect), as in The Witcher 3's art of it: a bank of cloud densest a
    // quarter of the way up the row, half as dense at its floor and gone by two thirds of its height, its top billowing;
    // thickest in the row's middle, a third as thick at its ends; and never still. Puffs (Mist, wide and flat) are born
    // across it all the time and drift slowly along the row, swelling and thinning out as they go: big ones make its body,
    // smaller ones the rounded billows along its top, and faint wisps run faster in front. As it comes it billows up from
    // the floor: its first puffs rise and slow to a halt, and live staggered lives, as many ending each moment as drifting
    // ones are born, so the bank never thins while it fills in. As it goes, it lifts. In layout units and game seconds;
    // one row's fog is about 70 particles.
    internal sealed class FogParticles : WeatherLook
    {
        private const float Wind = 12;                    // the drift along the row (to the right)
        private const int BodyPuffs = 30, TopPuffs = 28, Wisps = 10; // alive at once, once it has settled
        private const float BodyLife = 8, TopLife = 6, WispLife = 5; // their lives, on average
        private const float RiseSpeed = 90, RiseDrag = 3; // the first puffs' billowing up, and how fast it slows (1/s)
        private const float LiftSpeed = 30;               // the fog's rise as it goes
        private static readonly Color Grey = new Color(0.88f, 0.87f, 0.85f);

        private readonly ParticleSystem billow, bank, wisps;
        private readonly float width, height;
        private float bodyDue, topDue, wispDue; // the puffs owed since the last frame
        private bool rising = true, leaving;

        public FogParticles(RectTransform layer)
        {
            width = layer.rect.width;
            height = layer.rect.height;
            ParticleFx fx = Host("Fog", layer);
            // Drifting puffs come in and go out slowly (the bank renews itself unseen); the first ones come in at once
            Color clear = new Color(1, 1, 1, 0);
            billow = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.06f, Color.white), (0.65f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.75f), (0.2f, 1f), (1, 1.15f)), spin: 0.015f, turbulence: 5, frames: true, swirls: 240, stretched: true);
            bank = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.25f, Color.white), (0.7f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.8f), (1, 1.2f)), spin: 0.015f, turbulence: 5, frames: true, swirls: 240, stretched: true);
            wisps = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.3f, Color.white), (0.6f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.7f), (1, 1.3f)), spin: 0.02f, turbulence: 8, frames: true, swirls: 160, stretched: true);

            // The first puffs start a little lower than they settle, and rise there
            for (int i = 0; i < BodyPuffs + TopPuffs; i++)
            {
                bool top = i >= BodyPuffs;
                float life = Random.Range(1.5f, (top ? TopLife : BodyLife) + 1);
                float x = Across(life);
                float y = top ? height * Random.Range(0.22f, 0.32f) : Random.Range(0f, 15f);
                Puff(billow, x, Floor + y, new Vector2(Wind * Random.Range(0.7f, 1.3f), RiseSpeed * Random.Range(0.6f, 1.2f)), top, life);
            }
        }

        // A puff of the body (wide and flat) or of the billows along its top (smaller, rounder)
        private void Puff(ParticleSystem system, float x, float y, Vector2 velocity, bool top, float life)
        {
            float wide = top ? Random.Range(100f, 150f) : Random.Range(220f, 300f);
            Vector2 size = new Vector2(wide, wide * (top ? Random.Range(0.55f, 0.7f) : Random.Range(0.4f, 0.5f)));
            ParticleFx.Emit(system, new Vector2(x, y), velocity, size, life,
                Shade(x + velocity.x * life / 2, top ? Random.Range(0.33f, 0.5f) : Random.Range(0.31f, 0.43f)), Random.Range(-3f, 3f));
        }

        private float Floor { get { return -height / 2; } }

        public override void Tick(float dt, float age)
        {
            if (rising)
                rising = Settle(billow, RiseDrag, dt);
            if (leaving)
                return;
            // The body a quarter of the way up the row, the billows along its top, the row seen through it all
            for (bodyDue += BodyPuffs / BodyLife * dt; bodyDue >= 1; bodyDue--)
            {
                float life = BodyLife * Random.Range(0.8f, 1.2f);
                float x = Across(life);
                Puff(bank, x, Floor + height * Random.Range(0.12f, 0.3f), new Vector2(Wind * Random.Range(0.7f, 1.3f), Random.Range(-2f, 2f)), false, life);
            }
            for (topDue += TopPuffs / TopLife * dt; topDue >= 1; topDue--)
            {
                float life = TopLife * Random.Range(0.8f, 1.2f);
                float x = Across(life);
                Puff(bank, x, Floor + height * Random.Range(0.36f, 0.5f), new Vector2(Wind * Random.Range(0.7f, 1.3f), Random.Range(-2f, 2f)), true, life);
            }
            for (wispDue += Wisps / WispLife * dt; wispDue >= 1; wispDue--)
            {
                float life = WispLife * Random.Range(0.8f, 1.2f);
                float x = Across(life * 2.2f);
                float wide = Random.Range(140f, 200f);
                ParticleFx.Emit(wisps, new Vector2(x, Floor + height * Random.Range(0.15f, 0.4f)),
                    new Vector2(Wind * Random.Range(1.8f, 2.6f), Random.Range(-3f, 3f)),
                    new Vector2(wide, wide * 0.45f), life, Shade(x + Wind * 2.2f * life / 2, Random.Range(0.18f, 0.28f)), Random.Range(-4f, 4f));
            }
        }

        // It stops coming, and what there is of it lifts as it fades
        public override void Leave()
        {
            leaving = true;
            foreach (ParticleSystem system in new[] { billow, bank, wisps })
                Change(system, (ref ParticleSystem.Particle particle, int index) => particle.velocity += new Vector3(0, LiftSpeed, 0));
        }

        // Where a puff is born along the row: anywhere, a little past either end, set back by half its drift so that over
        // its life the bank stays centred
        private float Across(float life)
        {
            return Random.Range(-width / 2 - 60, width / 2 + 60) - Wind * life / 2;
        }

        // A puff's colour, by where it is halfway through its drift: thickest in the row's middle, thinning smoothly to a
        // third at its ends, as the fog is in The Witcher 3's art
        private Color Shade(float x, float alpha)
        {
            float along = Mathf.Clamp01(x / width + 0.5f);
            float thick = Mathf.Pow(Mathf.Sin(Mathf.PI * along), 1.6f);
            return new Color(Grey.r, Grey.g, Grey.b, alpha * Mathf.Lerp(0.3f, 1, thick));
        }
    }
}
