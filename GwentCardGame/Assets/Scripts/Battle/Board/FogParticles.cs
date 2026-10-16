using UnityEngine;

namespace Gwent.Battle
{
    // Impenetrable Fog rolling over a row (WeatherEffect): a bank of cloud along the row's floor, thickest in its middle and
    // thinning towards its ends, its top billowing, as in The Witcher 3's art of it, and never still. Puffs (Mist) are born
    // across it all the time and drift slowly along the row, swelling and thinning out as they go: big ones make its body
    // low in the row, smaller ones the rounded billows along its top, and faint wisps run faster in front. As it
    // comes it billows up from the floor: its first puffs rise and slow to a halt, and live staggered lives, as many ending
    // each moment as drifting ones are born, so the bank never thins while it fills in. As it goes, it lifts. In layout
    // units and game seconds; one row's fog is about 75 particles.
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
        private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];

        public FogParticles(RectTransform layer)
        {
            width = layer.rect.width;
            height = layer.rect.height;
            ParticleFx fx = ParticleFx.In("Fog", layer);
            // Drifting puffs come in and go out slowly (the bank renews itself unseen); the first ones come in at once
            Color clear = new Color(1, 1, 1, 0);
            billow = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.06f, Color.white), (0.65f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.75f), (0.2f, 1f), (1, 1.15f)), spin: 0.04f, turbulence: 5, frames: true, swirls: 240);
            bank = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.25f, Color.white), (0.7f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.8f), (1, 1.2f)), spin: 0.04f, turbulence: 5, frames: true, swirls: 240);
            wisps = fx.Add("Mist", ParticleFx.Colors((0, clear), (0.3f, Color.white), (0.6f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.7f), (1, 1.3f)), spin: 0.06f, turbulence: 8, frames: true, swirls: 160);

            for (int i = 0; i < BodyPuffs + TopPuffs; i++)
            {
                bool top = i >= BodyPuffs;
                float life = Random.Range(1.5f, (top ? TopLife : BodyLife) + 1);
                float x = Across(life);
                ParticleFx.Emit(billow, new Vector2(x, Floor + (top ? height * Random.Range(0.1f, 0.22f) : Random.Range(-25f, -5f))),
                    new Vector2(Wind * Random.Range(0.7f, 1.3f), RiseSpeed * Random.Range(0.6f, 1.2f)),
                    top ? Random.Range(80f, 130f) : Random.Range(200f, 290f), life,
                    Shade(x, top ? Random.Range(0.3f, 0.45f) : Random.Range(0.3f, 0.42f)), Random.Range(-8f, 8f));
            }
        }

        private float Floor { get { return -height / 2; } }

        public override void Tick(float dt, float age)
        {
            if (rising)
                rising = Settle(dt);
            if (leaving)
                return;
            // The body low in the row, the billows along its top: the bank covers about the row's lower 55%, the row seen
            // through it
            for (bodyDue += BodyPuffs / BodyLife * dt; bodyDue >= 1; bodyDue--)
            {
                float life = BodyLife * Random.Range(0.8f, 1.2f);
                float x = Across(life);
                ParticleFx.Emit(bank, new Vector2(x, Floor + height * Random.Range(0f, 0.22f)),
                    new Vector2(Wind * Random.Range(0.7f, 1.3f), Random.Range(-2f, 2f)),
                    Random.Range(200f, 290f), life, Shade(x, Random.Range(0.3f, 0.42f)), Random.Range(-8f, 8f));
            }
            for (topDue += TopPuffs / TopLife * dt; topDue >= 1; topDue--)
            {
                float life = TopLife * Random.Range(0.8f, 1.2f);
                float x = Across(life);
                ParticleFx.Emit(bank, new Vector2(x, Floor + height * Random.Range(0.3f, 0.45f)),
                    new Vector2(Wind * Random.Range(0.7f, 1.3f), Random.Range(-2f, 2f)),
                    Random.Range(80f, 130f), life, Shade(x, Random.Range(0.3f, 0.45f)), Random.Range(-8f, 8f));
            }
            for (wispDue += Wisps / WispLife * dt; wispDue >= 1; wispDue--)
            {
                float life = WispLife * Random.Range(0.8f, 1.2f);
                float x = Across(life * 2.2f);
                ParticleFx.Emit(wisps, new Vector2(x, Floor + height * Random.Range(0.1f, 0.4f)),
                    new Vector2(Wind * Random.Range(1.8f, 2.6f), Random.Range(-3f, 3f)),
                    Random.Range(120f, 180f), life, Shade(x, Random.Range(0.2f, 0.32f)), Random.Range(-10f, 10f));
            }
        }

        // It stops coming, and what there is of it lifts as it fades
        public override void Leave()
        {
            leaving = true;
            foreach (ParticleSystem system in new[] { billow, bank, wisps })
                Change(system, (ref ParticleSystem.Particle particle) => particle.velocity += new Vector3(0, LiftSpeed, 0));
        }

        // The first puffs' rise slows to a halt (the others barely move up or down); false once all have settled
        private bool Settle(float dt)
        {
            bool moving = false;
            float slow = Mathf.Exp(-RiseDrag * dt);
            Change(billow, (ref ParticleSystem.Particle particle) =>
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

        // Where a puff is born along the row: anywhere, a little past either end, set back by half its drift so that over
        // its life the bank stays centred
        private float Across(float life)
        {
            return Random.Range(-width / 2 - 60, width / 2 + 60) - Wind * life / 2;
        }

        // A puff's colour: thinner towards the row's ends, as the fog is in The Witcher 3's art (the horn slot's end barely
        // clouded)
        private Color Shade(float x, float alpha)
        {
            float along = x / width + 0.5f;
            float thick = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.05f, 0.25f, along)) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 0.88f, along));
            return new Color(Grey.r, Grey.g, Grey.b, alpha * Mathf.Lerp(0.25f, 1, thick));
        }

        private delegate void ParticleChange(ref ParticleSystem.Particle particle);

        private void Change(ParticleSystem system, ParticleChange change)
        {
            int count = system.particleCount;
            if (buffer.Length < count)
                buffer = new ParticleSystem.Particle[count * 2];
            count = system.GetParticles(buffer, count);
            for (int i = 0; i < count; i++)
                change(ref buffer[i]);
            system.SetParticles(buffer, count);
        }
    }
}
