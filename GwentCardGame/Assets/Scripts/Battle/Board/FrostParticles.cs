using System.Collections.Generic;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // Biting Frost on a row (WeatherEffect), as The Witcher 3 draws it: the cold's cobalt glow along the row's floor,
    // fading by mid-height (Band), and over it a see-through veil of frost grain (Frost), whiter in a haze over the horn
    // slot and by the row's middle; frosted shrubs (Rime), faint on the left, thickest at the right end; cream-white needles
    // of ice (Shard), braced like little pylons, the tallest beaded with frost at the tip; crisp banks of snow along its
    // front edge (Snow); glints running over the ice, a few snowflakes (Flake) drifting down low in the row, and a little
    // cold mist rolling. The haze, the shrubs, the needles and the snow stand just where The Witcher 3's art of the frost
    // has them. As it comes, the glow rises from the floor, then the snow, the
    // needles (snapping up, each glinting as it stands) and the shrubs come, from the row's ends towards its middle; as it
    // goes, everything sinks back into the floor. What stands (the glow, the veil, the hazes, the shrubs, the needles and
    // their beads, the snow) is images of the particles' art, sized by hand as they grow and shrink and left be while
    // they stand: unlike particles, they are not rebuilt every frame. What moves (the mist, the snowflakes, the glints) is
    // particles: the mist in a host of its own between the glow and the shrubs, the rest in one over everything. In layout
    // units and game seconds; one row's frost is about 70 images and 50 particles.
    internal sealed class FrostParticles : WeatherLook
    {
        private const int MistPuffs = 10, Flakes = 35;    // alive at once
        private const float MistLife = 8, FlakeLife = 6;  // on average
        private const float GlintsPerSecond = 4;
        private const float MeltSeconds = 0.45f;
        private static readonly Color Ice = new Color(0.92f, 0.96f, 1f);

        // Where The Witcher 3's art of the frost has its ice, read off it in its pixels (952 x 140, y down), as it was laid
        // over the row (stretched over the horn slot and the lane): it stands just there. Each needle's foot and tip:
        // a tall one and two leaning out at the row's left end, a few by the horn slot's end, pairs further on, the
        // middle bare, a pair past it, a cluster of five towards the right end and at that end the tallest of all, with
        // one leaning out beside it.
        private static readonly Vector4[] ArtNeedles =
        {
            new Vector4(16, 123, 19, 37), new Vector4(24, 121, 42, 75), new Vector4(30, 121, 52, 92),
            new Vector4(122, 124, 121, 76), new Vector4(139, 124, 139, 56), new Vector4(147, 124, 148, 85), new Vector4(156, 124, 156, 102),
            new Vector4(215, 124, 214, 85), new Vector4(276, 126, 278, 70), new Vector4(291, 126, 292, 92),
            new Vector4(381, 127, 382, 105), new Vector4(388, 127, 389, 83),
            new Vector4(666, 128, 663, 95), new Vector4(677, 128, 678, 103),
            new Vector4(763, 125, 753, 83), new Vector4(768, 125, 761, 95), new Vector4(772, 125, 781, 85),
            new Vector4(798, 125, 798, 92), new Vector4(804, 125, 804, 112),
            new Vector4(906, 124, 906, 105), new Vector4(920, 124, 922, 37), new Vector4(928, 124, 936, 82),
        };

        // Its frosted shrubs: each one's middle, top, width and how strongly it shows (barely on the left, most at the right
        // end, where they run together)
        private static readonly Vector4[] ArtShrubs =
        {
            new Vector4(100, 60, 80, 0.3f), new Vector4(195, 55, 70, 0.28f), new Vector4(278, 45, 50, 0.34f), new Vector4(448, 30, 55, 0.36f),
            new Vector4(670, 55, 60, 0.4f), new Vector4(730, 55, 70, 0.48f), new Vector4(790, 50, 70, 0.5f), new Vector4(850, 40, 110, 0.6f),
        };

        // Its whiter haze of frost, over the horn slot, low by the row's middle and at the heart of the shrubs at the right
        // end: each one's middle, width and height
        private static readonly Vector4[] ArtHaze =
        {
            new Vector4(70, 82, 150, 100), new Vector4(525, 110, 120, 40), new Vector4(845, 100, 150, 50),
        };

        // Its snow, along its front edge: each bank's ends and height (heavy at the row's ends, a thin line between)
        private static readonly Vector3[] ArtSnow =
        {
            new Vector3(30, 135, 11), new Vector3(135, 340, 6), new Vector3(640, 740, 6), new Vector3(740, 945, 11),
        };
        private const float ArtWidth = 952, ArtHeight = 140, ArtFloor = 128; // the floor the shrubs stand on

        // Something that stands on the floor while the frost lasts: grown from nothing after its delay
        private struct Standing
        {
            public Vector2 Foot;  // it stands about this point
            public Vector2 Size;  // grown (its sheet cell's: twice its height, standing on the cell's middle)
            public float Lean;    // degrees, clockwise
            public float Delay;   // from the frost's coming
            public bool Shone;    // a needle's glint as it stood
        }

        // A kind of standing thing: its images (one per Standing, in the same order) under a layer of their own, drawn with
        // the art (one of its 2 x 2 sheet's cells each, with frames), how long it takes to grow, how wide it starts as it
        // rises, and whether it snaps into place (a little past its size, settling back)
        private sealed class Stand
        {
            public RectTransform Layer;
            public Texture Art;
            public bool Frames;
            public readonly List<Standing> Things = new List<Standing>();
            public readonly List<RawImage> Images = new List<RawImage>();
            public float GrowSeconds, WidthFrom;
            public bool Snaps;
        }

        private readonly ParticleSystem mist, flakes, glints;
        private readonly Stand band, veil, haze, rime, needles, beads, snow;
        private readonly float width, height;
        private float mistDue, flakeDue, glintDue, leftAt = -1;
        private bool shaping = true, leaving, wiped;

        public FrostParticles(RectTransform layer)
        {
            width = layer.rect.width;
            height = layer.rect.height;
            Color clear = new Color(1, 1, 1, 0);
            AnimationCurve same = ParticleFx.Sizes((0, 1), (1, 1));
            // In the order they are drawn
            band = NewStand(layer, "Band", false, 0.6f, 1);
            veil = NewStand(layer, "Frost", false, 0.8f, 1);
            mist = Host("Frost mist", layer).Add("Mist", ParticleFx.Colors((0, clear), (0.3f, Color.white), (0.7f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.9f), (1, 1.15f)), spin: 0.02f, turbulence: 4, frames: true, swirls: 240);
            haze = NewStand(layer, "Mist", true, 0.8f, 0.6f);
            rime = NewStand(layer, "Rime", true, 0.6f, 0.5f);
            needles = NewStand(layer, "Shard", true, 0.35f, 0.4f, snaps: true);
            beads = NewStand(layer, "Flake", false, 0.2f, 0);
            snow = NewStand(layer, "Snow", true, 0.35f, 0.7f);
            ParticleFx air = Host("Frost air", layer);
            flakes = air.Add("Flake", ParticleFx.Colors((0, clear), (0.15f, Color.white), (0.8f, Color.white), (1, clear)), same,
                spin: 0.5f, turbulence: 9, swirls: 60);
            glints = air.Add("Glint", ParticleFx.Colors((0, clear), (0.25f, Color.white), (1, clear)),
                ParticleFx.Sizes((0, 0.2f), (0.3f, 1f), (1, 0)), spin: 1.5f);

            PlaceGlow();
            PlaceGrass();
            PlaceNeedles();
            PlaceSnow();
            for (int i = 0; i < MistPuffs; i++)
                EmitMist(Random.Range(1f, MistLife + 1));
            for (int i = 0; i < Flakes / 2; i++) // a few already falling as it comes
                EmitFlake(Random.Range(0.5f, FlakeLife));
        }

        private float Floor { get { return -height / 2; } }

        // A point along the row (0 its left end, 1 its right)
        private float Along(float fraction) { return (fraction - 0.5f) * width; }

        // How soon after the frost comes something at x starts: first at the row's ends, last in its middle
        private float Sweep(float x, float from, float over)
        {
            return from + over * (1 - Mathf.Abs(x) / (width / 2)) + Random.Range(0f, 0.08f);
        }

        // The cold's cobalt glow, solid low in the row and gone by mid-height; over it, rising with it, the veil of frost
        // grain, and the art's whiter haze of frost
        private void PlaceGlow()
        {
            Add(band, new Vector2(0, Floor), new Vector2(width + 100, height * 1.1f), 0, new Color(0.26f, 0.46f, 0.8f, 0.66f), 0);
            Add(veil, new Vector2(0, Floor), new Vector2(width + 100, height * 1.4f), 0.1f, new Color(0.95f, 0.96f, 1f, 0.4f), 0);
            foreach (Vector4 cloud in ArtHaze)
            {
                Vector2 middle = Art(cloud.x, cloud.y);
                Add(haze, middle, new Vector2(cloud.z / ArtWidth * width / 0.9f, cloud.w / ArtHeight * height / 0.55f), Sweep(middle.x, 0.1f, 0.5f),
                    new Color(0.9f, 0.92f, 0.96f, 0.36f), 0);
            }
        }

        // A point of The Witcher 3's art (its pixels, y down) where it lies over the row
        private Vector2 Art(float x, float y)
        {
            return new Vector2((x / ArtWidth - 0.5f) * width, (0.5f - y / ArtHeight) * height);
        }

        // The frosted shrubs, each in a faint haze of frost, as strongly as the art shows them
        private void PlaceGrass()
        {
            foreach (Vector4 shrub in ArtShrubs)
            {
                Vector2 foot = Art(shrub.x, ArtFloor);
                float tall = (Art(0, shrub.y).y - foot.y) / 0.8f;                 // a shrub reaches about 80% up its cell's upper half
                float wide = shrub.z / ArtWidth * width / 0.6f;                  // and is about 60% of the cell wide
                float delay = Sweep(foot.x, 0.3f, 0.6f);
                Add(haze, foot + new Vector2(0, tall * 0.55f), new Vector2(wide * 1.2f, tall * 1.3f), delay,
                    new Color(0.85f, 0.78f, 0.8f, shrub.w * 0.3f), Random.Range(-5f, 5f));
                Add(rime, foot, new Vector2(wide, tall * 2), delay, new Color(0.92f, 0.86f, 0.86f, shrub.w), Random.Range(-4f, 4f));
            }
        }

        // The needles, each to its tip as in the art, as wide at its foot as a tenth of its length (the tall ones broader
        // still), see-through, the tallest beaded with frost at the tip as it stands
        private void PlaceNeedles()
        {
            foreach (Vector4 needle in ArtNeedles)
            {
                // From the floor (the art's needles stand on its snow, higher than ours: each is drawn on down, as it
                // leans, into the snow at the row's floor), to its tip
                Vector2 tip = Art(needle.z, needle.w), along = Art(needle.x, needle.y) - tip;
                Vector2 foot = tip + along * ((Floor - tip.y) / along.y), up = tip - foot;
                float length = up.magnitude, lean = Mathf.Atan2(up.x, up.y) * Mathf.Rad2Deg;
                bool tall = length > height * 0.5f;
                float delay = Sweep(foot.x, 0.15f, 0.6f);
                Add(needles, foot, new Vector2(Mathf.Max(4.5f, length * (tall ? 0.12f : 0.1f)), length * 2), delay,
                    new Color(0.95f, 0.93f, 0.88f, tall ? 0.55f : 0.42f), lean);
                if (length > height * 0.6f)
                    Add(beads, tip, new Vector2(7, 7), delay + needles.GrowSeconds * 0.8f, new Color(1, 0.99f, 0.95f, 0.65f), 0);
            }
        }

        // The snow banks, overlapping drifts along each, their heights a little their own
        private void PlaceSnow()
        {
            foreach (Vector3 bank in ArtSnow)
            {
                float from = Art(bank.x, 0).x, to = Art(bank.y, 0).x, high = bank.z / ArtHeight * height;
                bool deep = bank.z > 8;
                for (float x = from + 50; x < to; x += Random.Range(80f, 120f))
                    Add(snow, new Vector2(x, Floor + 1), new Vector2(Random.Range(150f, 210f), high * Random.Range(0.8f, 1.15f) * 3.6f),
                        Sweep(x, 0.05f, 0.5f), new Color(1, 1, 1, deep ? 0.97f : 0.85f), Random.Range(-1.5f, 1.5f));
            }
        }

        // A layer over the whole row for a kind of standing thing (its middle the row's, as the particle hosts')
        private static Stand NewStand(RectTransform layer, string art, bool frames, float grow, float widthFrom, bool snaps = false)
        {
            RectTransform own = UIKit.Rect(art, layer).Stretch();
            return new Stand { Layer = own, Art = ParticleFx.Art(art), Frames = frames, GrowSeconds = grow, WidthFrom = widthFrom, Snaps = snaps };
        }

        // Something standing, centred about its foot (the art stands on its cell's middle), not yet grown
        private void Add(Stand stand, Vector2 foot, Vector2 size, float delay, Color color, float lean)
        {
            stand.Things.Add(new Standing { Foot = foot, Size = size, Lean = lean, Delay = delay });
            RawImage image = UIKit.Rect(stand.Art.name, stand.Layer).gameObject.AddComponent<RawImage>();
            image.texture = stand.Art;
            image.color = color;
            image.raycastTarget = false;
            if (stand.Frames)
            {
                int cell = Random.Range(0, 4);
                image.uvRect = new Rect((cell % 2) * 0.5f, (cell / 2) * 0.5f, 0.5f, 0.5f);
            }
            image.rectTransform.Place(new Vector2(0.5f, 0.5f), foot, Vector2.zero);
            image.rectTransform.localEulerAngles = new Vector3(0, 0, -lean); // a lean is clockwise
            stand.Images.Add(image);
        }

        public override void Tick(float dt, float age)
        {
            if (shaping)
                shaping = Shape(age);
            if (leaving)
                return;
            for (mistDue += MistPuffs / MistLife * dt; mistDue >= 1; mistDue--)
                EmitMist(MistLife * Random.Range(0.8f, 1.2f));
            for (flakeDue += Flakes / FlakeLife * dt; flakeDue >= 1; flakeDue--)
                EmitFlake(FlakeLife * Random.Range(0.7f, 1.3f));
            for (glintDue += GlintsPerSecond * dt; glintDue >= 1; glintDue--)
            {
                Standing needle = needles.Things[Random.Range(0, needles.Things.Count)];
                if (age > needle.Delay + needles.GrowSeconds)
                    Glint(Up(needle, Random.Range(0.3f, 0.95f)), Random.Range(7f, 11f));
            }
        }

        // Everything sinks back into the floor; nothing new comes
        public override void Leave()
        {
            leaving = true;
            shaping = true;
        }

        // Wiped away by the sun: it stands as it was, nothing sinking
        public override void Wiped()
        {
            wiped = true;
        }

        // What stands, at its size for now: growing, or once the frost goes, sinking whole into the floor. A needle
        // glints at its tip as it stands. False once all stand still.
        private bool Shape(float age)
        {
            if (leaving && leftAt < 0)
                leftAt = age;
            bool melting = leaving && !wiped;
            float melt = melting ? Mathf.Clamp01((age - leftAt) / MeltSeconds) : 0;
            float melted = (1 - melt) * (1 - melt);
            bool moving = false;
            foreach (Stand stand in new[] { band, veil, haze, rime, needles, beads, snow })
            {
                for (int index = 0; index < stand.Things.Count; index++)
                {
                    Standing thing = stand.Things[index];
                    float time = (age - thing.Delay) / stand.GrowSeconds;
                    float grown = stand.Snaps ? Snap(time) : Mathf.SmoothStep(0, 1, time);
                    // What spans the row (the glow, the veil: their ends are hard) only sinks; the rest also narrows
                    float narrowed = stand.WidthFrom < 1 ? melted : 1;
                    stand.Images[index].rectTransform.sizeDelta = new Vector2(
                        thing.Size.x * Mathf.Lerp(stand.WidthFrom, 1, Mathf.Min(grown, 1)) * narrowed, thing.Size.y * grown * melted);
                    moving |= melting ? melt < 1 : time < 1;
                    if (stand == needles && !thing.Shone && !leaving && time >= 1)
                    {
                        thing.Shone = true;
                        stand.Things[index] = thing;
                        Glint(Up(thing, 0.95f), Random.Range(11f, 15f));
                    }
                }
            }
            return moving;
        }

        // Grown for how far through its growing (0 to 1): nothing before, then quickly a little past its size (the ice
        // snapping into place), settling back
        private static float Snap(float t)
        {
            t = Mathf.Clamp01(t);
            const float back = 1.6f; // the overshoot
            return t <= 0 ? 0 : 1 + (back + 1) * Mathf.Pow(t - 1, 3) + back * Mathf.Pow(t - 1, 2);
        }

        // A point up a needle (0 its foot, 1 its tip), as it leans
        private static Vector2 Up(Standing needle, float along)
        {
            float lean = needle.Lean * Mathf.Deg2Rad;
            return needle.Foot + new Vector2(Mathf.Sin(lean), Mathf.Cos(lean)) * (needle.Size.y / 2 * along);
        }

        private void Glint(Vector2 at, float size)
        {
            ParticleFx.Emit(glints, at, Vector2.zero, size, Random.Range(0.4f, 0.8f), new Color(0.85f, 0.93f, 1f, 1f), Random.Range(0f, 45f));
        }

        // A snowflake drifting down low in the row, swaying: most small and faint, now and then a near one, larger, softer
        // and fainter still; fading in where it is born and out before it lands
        private void EmitFlake(float life)
        {
            bool near = Random.value < 0.08f;
            Vector2 at = new Vector2(Random.Range(-width / 2, width / 2), Random.Range(Floor + 10, Floor + height * 0.75f));
            ParticleFx.Emit(flakes, at, new Vector2(Random.Range(-5f, 5f), -Random.Range(6f, 14f) * (near ? 1.6f : 1)),
                near ? Random.Range(7f, 10f) : Random.Range(2f, 4.5f), life,
                new Color(Ice.r, Ice.g, Ice.b, near ? Random.Range(0.12f, 0.2f) : Random.Range(0.25f, 0.45f)));
        }

        // A little cold mist rolling low over the glow: big faint blue puffs drifting
        private void EmitMist(float life)
        {
            Vector2 at = new Vector2(Random.Range(-width / 2 - 120, width / 2 + 120), Floor + Random.Range(-30f, 10f));
            ParticleFx.Emit(mist, at, new Vector2(Random.Range(-5f, 5f), Random.Range(-1f, 1f)), Random.Range(380f, 520f), life,
                new Color(0.5f, 0.65f, 0.95f, Random.Range(0.06f, 0.1f)), Random.Range(-4f, 4f));
        }
    }
}
