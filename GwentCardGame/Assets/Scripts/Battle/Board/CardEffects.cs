using System.Threading;
using Cysharp.Threading.Tasks;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // The Witcher 3's card effects on the board, on the overlay (game time): the wisps of a card landing, an ability's
    // flare over its card (its white icon over the card burning orange), the medic's sign on the discard pile, the hero's
    // landing (smoke, and a fire running around the card's edge), the horn's golden glow along its row, the flames of a
    // scorch and a lost round's gem breaking. Art: Resources/W3/Effects; smoke, flames and sparks are particles
    // (ParticleFx).
    public static class CardEffects
    {
        public const float WispSeconds = 0.35f; // a landing waits this long for its wisps (they fade on their own)
        public const float FlareSeconds = 1.05f;
        public const float HeroSeconds = 1.6f;
        public const float MedicSeconds = 1.15f;
        public const float GlowSeconds = 0.8f;
        public const float BurnSeconds = 0.7f;
        public const float GemPulseSeconds = 0.55f;
        public const float ShardSeconds = 0.75f;

        private static readonly Color Blaze = new Color(1f, 0.55f, 0.15f);
        private static readonly Color FireLine = new Color(1f, 0.66f, 0.18f);
        private static readonly Color FireGlow = new Color(1f, 0.45f, 0.05f, 0.35f);

        // The W3 flare of an ability ("muster", "morale", "bond", "horn", "spy"), or null (a medic's sign shows on its
        // discard pile instead: MedicPile)
        public static string FlareFor(Ability ability)
        {
            switch (ability)
            {
                case Gwent.Rules.Ability.Muster: return "muster";
                case Gwent.Rules.Ability.MoraleBoost: return "morale";
                case Gwent.Rules.Ability.TightBond: return "bond";
                case Gwent.Rules.Ability.UnitHorn:
                case Gwent.Rules.Ability.CommanderHorn: return "horn";
                case Gwent.Rules.Ability.Spy: return "spy";
                default: return null;
            }
        }

        // A card landing, as in The Witcher 3: wisps of white smoke puff out of its sides and fade
        public static async UniTask Wisps(GameObject card, CancellationToken ct = default)
        {
            if (card == null)
                return;
            Vector2 center, size;
            CardMotion.ToLayout(CardMotion.ScreenRect((RectTransform)card.transform), out center, out size);
            ParticleFx fx = ParticleFx.At("LandingWisps", center);
            ParticleSystem wisps = fx.Add("Smoke", ParticleFx.Colors((0, new Color(1, 0.98f, 0.94f, 0)), (0.2f, new Color(1, 0.98f, 0.94f, 0.5f)), (1, new Color(1, 0.98f, 0.94f, 0))),
                ParticleFx.Sizes((0, 0.55f), (1, 1.3f)), gravity: -10, spin: 0.8f, turbulence: 10, frames: true);
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 at = new Vector2(side * size.x * Random.Range(0.42f, 0.55f), size.y * Random.Range(-0.4f, 0.35f));
                ParticleFx.Emit(wisps, at, new Vector2(side * Random.Range(12f, 40f), Random.Range(4f, 18f)), size.x * Random.Range(0.28f, 0.42f),
                    Random.Range(0.4f, 0.6f), Color.white, Random.Range(0f, 360f));
            }
            fx.Finish().Forget(); // the wisps fade on their own
            await Wait(WispSeconds, ct);
        }

        // An ability's icon blazing over its card
        public static async UniTask Flare(GameObject card, string flare, CancellationToken ct = default)
        {
            if (card == null || flare == null)
                return;
            await FlareAt(ct, CardMotion.ScreenRect((RectTransform)card.transform), flare, Blaze);
        }

        // The medic's sign on its discard pile (The Witcher 3): the card turns green under a large white heart, in a green
        // glow with green flames licking its left and bottom edges; then the heart flashes out
        public static async UniTask MedicPile(RectTransform pile_card, CancellationToken ct = default)
        {
            if (pile_card == null)
                return;
            Rect rect = CardMotion.ScreenRect(pile_card);
            Vector2 center, size;
            CardMotion.ToLayout(rect, out center, out size);
            Image glow = Overlay("MedicGlow", UITheme.Current.radial, new Color(0.3f, 1f, 0.45f, 0), Grow(rect, 1.9f));
            Image tint = Overlay("MedicTint", null, new Color(0.15f, 0.85f, 0.3f, 0), rect);
            ParticleFx fx = ParticleFx.At("MedicFlames", center);
            ParticleSystem haze = fx.Add("Blaze", ParticleFx.Colors((0, new Color(0.5f, 1f, 0.6f, 0)), (0.3f, new Color(0.35f, 0.95f, 0.5f, 0.5f)), (1, new Color(0.1f, 0.6f, 0.25f, 0))),
                ParticleFx.Sizes((0, 0.8f), (1, 1.3f)), gravity: -20, spin: 1.5f, turbulence: 25, frames: true);
            ParticleSystem flames = fx.Add("Fire", ParticleFx.Colors((0, new Color(0.8f, 1f, 0.8f, 0)), (0.2f, new Color(0.55f, 1f, 0.6f, 0.85f)), (1, new Color(0.1f, 0.6f, 0.25f, 0))),
                ParticleFx.Sizes((0, 0.7f), (0.4f, 1f), (1, 0.6f)), gravity: -50, turbulence: 25, frames: true);
            Image heart = Overlay("Medic", SpriteCache.Load("W3/Effects/medic"), new Color(1, 1, 1, 0), Grow(rect, 1.3f));
            heart.preserveAspect = true;
            float age = 0, emit = 0;
            try
            {
                while (age < MedicSeconds)
                {
                    float dt = Time.deltaTime;
                    age += dt;
                    float on = Mathf.Clamp01(age / 0.08f), off = Mathf.Clamp01((age - 0.95f) / 0.2f);
                    tint.color = new Color(0.15f, 0.85f, 0.3f, 0.35f * on * (1 - off));
                    glow.color = new Color(0.3f, 1f, 0.45f, (0.6f + 0.15f * Mathf.Sin(age * 14)) * on * (1 - off));
                    heart.color = new Color(1, 1, 1, on * (1 - off));
                    heart.rectTransform.localScale = Vector3.one * (Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(age / 0.12f)) + 0.15f * off);
                    // Green flames licking out of the left and bottom edges, in a green haze swirling out past them
                    for (emit += (age < 0.95f ? 110 : 0) * dt; emit >= 1; emit--)
                    {
                        if (Random.value < 0.45f)
                        {
                            Vector2 out_left = new Vector2(Random.Range(-0.7f, -0.3f) * size.x, Random.Range(-0.55f, 0.45f) * size.y);
                            Vector2 out_below = new Vector2(Random.Range(-0.6f, 0.4f) * size.x, Random.Range(-0.6f, -0.35f) * size.y);
                            ParticleFx.Emit(haze, Random.value < 0.7f ? out_left : out_below, new Vector2(Random.Range(-20f, 0f), Random.Range(5f, 30f)),
                                size.x * Random.Range(0.45f, 0.75f), Random.Range(0.4f, 0.65f), Color.white, Random.Range(0f, 360f));
                            continue;
                        }
                        bool left = Random.value < 0.6f;
                        Vector2 at = left ? new Vector2(-size.x / 2 - Random.Range(0f, 6f), Random.Range(-size.y / 2, size.y / 2)) : new Vector2(Random.Range(-size.x / 2, size.x / 2), -size.y / 2 - Random.Range(0f, 4f));
                        Vector2 velocity = left ? new Vector2(Random.Range(-30f, -8f), Random.Range(20f, 60f)) : new Vector2(Random.Range(-25f, 10f), Random.Range(-10f, 25f));
                        ParticleFx.Emit(flames, at, velocity, size.x * Random.Range(0.25f, 0.4f), Random.Range(0.3f, 0.5f), Color.white, left ? Random.Range(10f, 40f) : Random.Range(-20f, 20f));
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                fx.Destroy();
                Remove(glow);
                Remove(tint);
                Remove(heart);
            }
        }

        // An ability flaring on its card, as in The Witcher 3: a white flash, then the card burns bright orange, fire
        // churning in it, its glow spilling past its edges, its ability's icon large and white over it; then the fire dies
        // down
        private static async UniTask FlareAt(CancellationToken ct, Rect rect, string flare, Color tint)
        {
            Vector2 center, size;
            CardMotion.ToLayout(rect, out center, out size);
            float w = size.x, h = size.y;
            Image halo = Halo("FlareHalo", new Color(tint.r, tint.g, tint.b, 0), rect);
            Image wash = Overlay("FlareWash", null, new Color(tint.r, tint.g, tint.b, 0), rect);
            ParticleFx fx = ParticleFx.At("FlareFire", center);
            ParticleSystem churn = fx.Add("Blaze", ParticleFx.Colors((0, new Color(1, 0.9f, 0.55f, 0)), (0.25f, new Color(1, 0.78f, 0.32f, 0.6f)), (1, new Color(1, 0.45f, 0.08f, 0))),
                ParticleFx.Sizes((0, 0.8f), (1, 1.15f)), gravity: -40, spin: 1.2f, turbulence: 20, frames: true);
            ParticleSystem fire = fx.Add("Fire", ParticleFx.Colors((0, new Color(1, 0.95f, 0.7f, 0)), (0.15f, new Color(1, 0.85f, 0.45f, 0.8f)),
                (0.6f, new Color(1, 0.55f, 0.12f, 0.5f)), (1, new Color(0.85f, 0.25f, 0.02f, 0))),
                ParticleFx.Sizes((0, 0.7f), (0.4f, 1f), (1, 0.75f)), gravity: -60, turbulence: 30, frames: true);
            ParticleSystem flash = fx.Add("Blaze", ParticleFx.Colors((0, new Color(1, 1, 1, 0.9f)), (1, new Color(1, 1, 1, 0))),
                ParticleFx.Sizes((0, 0.8f), (1, 1.4f)), spin: 1f, frames: true);
            Sprite sprite = SpriteCache.Load("W3/Effects/" + flare);
            Image shadow = LayerImage("FlareIconShadow", sprite, new Color(0, 0, 0, 0), center + new Vector2(2, -3), new Vector2(w, w) * 0.8f);
            Image icon = LayerImage("FlareIcon", sprite, new Color(1, 1, 1, 0), center, new Vector2(w, w) * 0.78f);
            shadow.preserveAspect = icon.preserveAspect = true;
            try
            {
                for (int i = 0; i < 5; i++)
                    ParticleFx.Emit(flash, Random.insideUnitCircle * w * 0.2f, Random.insideUnitCircle * 25, w * Random.Range(0.6f, 0.9f), Random.Range(0.18f, 0.28f), Color.white, Random.Range(0f, 360f));
                float age = 0, emit = 0;
                while (age < FlareSeconds)
                {
                    float dt = Time.deltaTime;
                    age += dt;
                    float on = Mathf.Clamp01((age - 0.04f) / 0.12f), off = Mathf.Clamp01((age - FlareSeconds + 0.3f) / 0.3f);
                    Color washed = Color.Lerp(Color.white, tint, Mathf.Clamp01((age - 0.08f) / 0.12f)); // a white flash, then orange
                    wash.color = new Color(washed.r, washed.g, washed.b, 0.75f * on * (1 - off));
                    halo.color = new Color(tint.r, tint.g, tint.b, 0.55f * on * (1 - off));
                    float pop = Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((age - 0.04f) / 0.14f));
                    icon.color = new Color(1, 1, 1, on * (1 - off));
                    shadow.color = new Color(0, 0, 0, 0.35f * on * (1 - off));
                    icon.rectTransform.localScale = shadow.rectTransform.localScale = Vector3.one * pop;

                    // Fire churning in the card: bright clouds, flames rising in them, small ones at its edges
                    for (emit += (age > 0.04f && age < FlareSeconds - 0.25f ? 160 : 0) * dt; emit >= 1; emit--)
                    {
                        float pick = Random.value;
                        if (pick < 0.4f)
                            ParticleFx.Emit(churn, new Vector2(Random.Range(-0.4f, 0.4f) * w, Random.Range(-0.42f, 0.35f) * h), new Vector2(Random.Range(-10f, 10f), Random.Range(15f, 40f)),
                                w * Random.Range(0.5f, 0.8f), Random.Range(0.35f, 0.6f), Color.white, Random.Range(0f, 360f));
                        else if (pick < 0.85f)
                            ParticleFx.Emit(fire, new Vector2(Random.Range(-0.42f, 0.42f) * w, Random.Range(-0.45f, 0.2f) * h), new Vector2(Random.Range(-12f, 12f), Random.Range(20f, 50f)),
                                w * Random.Range(0.28f, 0.45f), Random.Range(0.3f, 0.5f), Color.white, Random.Range(-20f, 20f));
                        else
                        {
                            Vector2 edge = Around(Random.value * 2 * (w + h), w, h);
                            ParticleFx.Emit(fire, edge * 0.97f, Outward(edge, w, h) * 15 + new Vector2(0, 20), w * Random.Range(0.12f, 0.22f), Random.Range(0.2f, 0.35f), Color.white, Random.Range(-30f, 30f));
                        }
                    }
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                fx.Destroy();
                Remove(halo);
                Remove(wash);
                Remove(icon);
                Remove(shadow);
            }
        }

        // A hero landing, as in The Witcher 3: smoke billows up over the card, which darkens beneath it; a fire runs once
        // around its edge (down the left side, along the bottom, up the right side, back along the top) as a swarm of
        // sparks falling away, flames rising off the top edge, and leaves a thin glowing outline; then the smoke clears,
        // the card shows and embers drift up from its top
        public static async UniTask Hero(GameObject card, CancellationToken ct = default)
        {
            if (card == null)
                return;
            Vector2 center, size;
            CardMotion.ToLayout(CardMotion.ScreenRect((RectTransform)card.transform), out center, out size);
            float w = size.x, h = size.y, perimeter = 2 * (w + h);

            // The card darkened, behind the smoke; then the outline the fire leaves; in front, the fire
            Image shade = LayerImage("HeroShade", null, new Color(0.17f, 0.17f, 0.19f, 0), center, size);
            ParticleFx back = ParticleFx.At("HeroSmoke", center);
            ParticleSystem smoke = back.Add("Smoke", ParticleFx.Colors((0, new Color(1, 1, 1, 0)), (0.1f, new Color(1, 1, 1, 1)), (0.75f, new Color(1, 1, 1, 0.9f)), (1, new Color(1, 1, 1, 0))),
                ParticleFx.Sizes((0, 0.8f), (1, 1.25f)), gravity: -8, spin: 0.5f, turbulence: 10, frames: true);
            RectTransform frame = UIKit.Rect("HeroOutline", CardMotion.Layer);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = center;
            frame.sizeDelta = size;
            Vector2[] corners = { new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1) };
            Vector2[] pivots = { new Vector2(0.5f, 1), new Vector2(0, 0.5f), new Vector2(0.5f, 0), new Vector2(1, 0.5f) };
            Image[] glows = new Image[4], lines = new Image[4];
            for (int e = 0; e < 4; e++)
            {
                glows[e] = Edge(frame, corners[e], pivots[e], FireGlow);
                lines[e] = Edge(frame, corners[e], pivots[e], FireLine);
            }
            ParticleFx front = ParticleFx.At("HeroFire", center);
            ParticleSystem flames = front.Add("Fire", ParticleFx.Colors((0, new Color(1, 0.92f, 0.6f, 0.95f)), (0.4f, new Color(1, 0.58f, 0.14f, 0.8f)), (1, new Color(0.8f, 0.2f, 0.02f, 0))),
                ParticleFx.Sizes((0, 0.6f), (0.3f, 1f), (1, 0.45f)), gravity: -220, frames: true);
            ParticleSystem sparks = front.Add("Spark", ParticleFx.Colors((0, new Color(1, 0.97f, 0.82f, 1)), (0.35f, new Color(1, 0.72f, 0.28f, 1)), (1, new Color(1, 0.35f, 0.05f, 0))),
                ParticleFx.Sizes((0, 1f), (1, 0.5f)), gravity: 240);
            ParticleSystem glow = front.Add("Spark", ParticleFx.Colors((0, new Color(1, 0.55f, 0.12f, 0.28f)), (1, new Color(1, 0.35f, 0.05f, 0))),
                ParticleFx.Sizes((0, 1f), (1, 1.3f)));
            ParticleSystem embers = front.Add("Spark", ParticleFx.Colors((0, new Color(1, 0.9f, 0.5f, 1)), (1, new Color(1, 0.4f, 0.05f, 0))),
                ParticleFx.Sizes((0, 1f), (1, 0.6f)), gravity: -35, turbulence: 20);

            Color grey = new Color(0.55f, 0.55f, 0.58f, 0.95f), dark = new Color(0.16f, 0.16f, 0.18f, 0.7f);
            try
            {
                // The smoke billows up: a dark shadow to the lower right, grey puffs over the card spilling past its edges
                for (int i = 0; i < 5; i++)
                    ParticleFx.Emit(smoke, new Vector2(w * Random.Range(0.1f, 0.35f), h * Random.Range(-0.35f, 0.1f)), new Vector2(Random.Range(4f, 14f), Random.Range(-4f, 6f)),
                        w * Random.Range(0.7f, 0.95f), Random.Range(1.2f, 1.35f), dark, Random.Range(0f, 360f));
                for (int i = 0; i < 24; i++)
                {
                    Vector2 at = new Vector2(Random.Range(-0.5f, 0.5f) * w, Random.Range(-0.52f, 0.45f) * h);
                    ParticleFx.Emit(smoke, at, at.normalized * Random.Range(10f, 30f) + new Vector2(0, 6), w * Random.Range(0.6f, 0.95f), Random.Range(1.1f, 1.3f),
                        Color.Lerp(grey, Color.white, Random.Range(0f, 0.15f)), Random.Range(0f, 360f));
                }

                float age = 0, emit_sparks = 0, emit_glow = 0, emit_flames = 0, emit_smoke = 0, emit_embers = 0;
                const float burn_from = 0.07f, burn_seconds = 1f;
                while (age < HeroSeconds && card != null)
                {
                    float dt = Time.deltaTime;
                    age += dt;
                    // The card darkens under the smoke, then shows again as it clears
                    float cover = Mathf.Clamp01(age / 0.08f) * (1 - Mathf.Clamp01((age - 1.12f) / 0.15f));
                    shade.color = new Color(0.17f, 0.17f, 0.19f, 0.9f * cover);
                    for (emit_smoke += (age < 0.75f ? 14 : 0) * dt; emit_smoke >= 1; emit_smoke--) // it all clears with the fire out
                        ParticleFx.Emit(smoke, new Vector2(Random.Range(-0.4f, 0.4f) * w, Random.Range(-0.42f, 0.38f) * h), new Vector2(Random.Range(-8f, 8f), Random.Range(4f, 14f)),
                            w * Random.Range(0.45f, 0.7f), Random.Range(0.5f, 0.65f), grey, Random.Range(0f, 360f));

                    // The fire's way around the edge, and the outline it leaves
                    float burnt = Mathf.Clamp01((age - burn_from) / burn_seconds) * perimeter;
                    float outline = 1 - Mathf.Clamp01((age - 1.15f) / 0.2f);
                    for (int e = 0; e < 4; e++)
                    {
                        bool upright = e % 2 == 0;
                        float start = e == 0 ? 0 : e == 1 ? h : e == 2 ? h + w : 2 * h + w; // left, bottom, right, top
                        float length = Mathf.Clamp(burnt - start, 0, upright ? h : w);
                        lines[e].rectTransform.sizeDelta = upright ? new Vector2(2.5f, length) : new Vector2(length, 2.5f);
                        glows[e].rectTransform.sizeDelta = upright ? new Vector2(11, length) : new Vector2(length, 11);
                        lines[e].color = new Color(FireLine.r, FireLine.g, FireLine.b, outline * Random.Range(0.85f, 1f));
                        glows[e].color = new Color(FireGlow.r, FireGlow.g, FireGlow.b, FireGlow.a * outline);
                    }

                    // The burning stretch: sparks thrown off it fall away; flames rise from it, tall along the top edge
                    bool burning = age >= burn_from && age < burn_from + burn_seconds;
                    bool top = burnt > 2 * h + w;
                    for (emit_sparks += (burning ? 1000 : 0) * dt; emit_sparks >= 1; emit_sparks--)
                    {
                        Vector2 at = Around(Mathf.Max(0, burnt - Random.Range(0f, 30f) * Random.value), w, h) + Random.insideUnitCircle * 3;
                        Vector2 velocity = Random.insideUnitCircle * 80 + Outward(at, w, h) * 20 + (top ? new Vector2(0, Random.Range(30f, 90f)) : Vector2.zero);
                        ParticleFx.Emit(sparks, at, velocity, Random.Range(9f, 17f), Random.Range(0.25f, 0.65f), Color.white); // the art's hot core is a tenth of it
                    }
                    for (emit_glow += (burning ? 60 : 0) * dt; emit_glow >= 1; emit_glow--) // the burning stretch glows softly
                        ParticleFx.Emit(glow, Around(Mathf.Max(0, burnt - Random.Range(0f, 24f)), w, h), Vector2.zero, Random.Range(20f, 28f), Random.Range(0.12f, 0.2f), Color.white);
                    for (emit_flames += (burning ? (top ? 170 : 90) : 0) * dt; emit_flames >= 1; emit_flames--)
                    {
                        Vector2 at = Around(Mathf.Max(0, burnt - Random.Range(0f, 26f)), w, h);
                        float tall = top ? Random.Range(14f, 26f) : Random.Range(8f, 16f);
                        ParticleFx.Emit(flames, at, Outward(at, w, h) * 12 + new Vector2(0, Random.Range(15f, 40f)), tall, Random.Range(0.16f, 0.32f), Color.white, Random.Range(-20f, 20f));
                    }

                    // Once the fire is out, embers drift up from the top edge (where it ended)
                    for (emit_embers += (age > burn_from + burn_seconds - 0.1f && age < 1.5f ? 45 : 0) * dt; emit_embers >= 1; emit_embers--)
                        ParticleFx.Emit(embers, new Vector2(w * Random.Range(-0.5f, 0.1f), h / 2 + Random.Range(-4f, 4f)), new Vector2(Random.Range(-15f, 15f), Random.Range(35f, 80f)),
                            Random.Range(8f, 13f), Random.Range(0.6f, 1f), Color.white);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                Remove(shade);
                Remove(frame);
                back.Destroy();
                front.Finish().Forget(); // the last embers fade on their own
            }
        }

        // Out of a card's edge from a point on it (from the card's centre)
        private static Vector2 Outward(Vector2 at, float w, float h)
        {
            return new Vector2(Mathf.Abs(at.x) >= w / 2 - 2 ? Mathf.Sign(at.x) : 0, Mathf.Abs(at.y) >= h / 2 - 2 ? Mathf.Sign(at.y) : 0);
        }

        // A point on a card's edge this far around it, from its top-left corner going down, then right, up and left
        // (from the card's centre)
        private static Vector2 Around(float distance, float w, float h)
        {
            if (distance < h)
                return new Vector2(-w / 2, h / 2 - distance);
            distance -= h;
            if (distance < w)
                return new Vector2(-w / 2 + distance, -h / 2);
            distance -= w;
            if (distance < h)
                return new Vector2(w / 2, -h / 2 + distance);
            distance -= h;
            return new Vector2(w / 2 - Mathf.Min(distance, w), h / 2);
        }

        private static Image Edge(RectTransform frame, Vector2 corner, Vector2 pivot, Color color)
        {
            Image edge = UIKit.NewImage(frame, "Edge", null, color);
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return edge;
        }

        // A horn's warm golden glow around its row (the cards in it stay clear)
        public static async UniTask RowGlow(RectTransform row, CancellationToken ct = default)
        {
            if (row == null)
                return;
            Image glow = Halo("HornGlow", new Color(1f, 0.72f, 0.28f, 0), CardMotion.ScreenRect(row));
            await Animate(ct, GlowSeconds, t =>
            {
                glow.color = new Color(1f, 0.72f, 0.28f, 0.9f * Mathf.Sin(t * Mathf.PI));
            });
            Object.Destroy(glow.gameObject);
        }

        // Scorch: flames over each card about to be destroyed
        public static async UniTask Burn(RectTransform[] cards, CancellationToken ct = default)
        {
            Image[] flames = new Image[cards.Length];
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null)
                {
                    Rect rect = CardMotion.ScreenRect(cards[i]);
                    flames[i] = Overlay("Scorch", SpriteCache.Load("W3/Effects/scorch"), new Color(1, 1, 1, 0), Grow(rect, 1.15f));
                }
            await Animate(ct, BurnSeconds, t =>
            {
                float a = t < 0.2f ? t / 0.2f : 1;
                foreach (Image flame in flames)
                    if (flame != null)
                    {
                        flame.color = new Color(1, 1, 1, a);
                        flame.rectTransform.localScale = new Vector3(1, Mathf.Lerp(0.6f, 1.05f, t), 1);
                    }
            });
            foreach (Image flame in flames)
                if (flame != null)
                    Object.Destroy(flame.gameObject);
        }

        // A lost round: the gem pulses, then bursts into shards that fall away, leaving it grey
        public static async UniTask Shatter(Image gem, Sprite broken, CancellationToken ct = default)
        {
            if (gem == null)
                return;
            Rect rect = CardMotion.ScreenRect(gem.rectTransform);
            Color red = new Color(0.86f, 0.12f, 0.1f);
            Image glow = Overlay("GemGlow", UITheme.Current.radial, new Color(red.r, red.g, red.b, 0), Grow(rect, 2.6f));
            Image pulse = Overlay("Gem", gem.sprite, Color.white, rect);
            pulse.preserveAspect = true;
            await Animate(ct, GemPulseSeconds, t =>
            {
                float beat = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2)); // two beats
                pulse.rectTransform.localScale = Vector3.one * (1 + 0.22f * beat);
                glow.color = new Color(red.r, red.g, red.b, 0.75f * beat);
            });
            Object.Destroy(pulse.gameObject);
            Object.Destroy(glow.gameObject);
            gem.sprite = broken;

            // The shards: thin red slivers flying out, spinning and falling
            const int count = 10;
            Image[] shards = new Image[count];
            Vector2[] start = new Vector2[count], velocity = new Vector2[count];
            float[] spin = new float[count];
            for (int i = 0; i < count; i++)
            {
                float width = rect.width * Random.Range(0.2f, 0.32f);
                Vector2 size = new Vector2(width, width * Random.Range(1.5f, 2.4f));
                Color tint = Color.Lerp(red, i % 3 == 0 ? new Color(1f, 0.75f, 0.7f) : new Color(0.45f, 0.03f, 0.03f), Random.Range(0.1f, 0.5f));
                shards[i] = Overlay("Shard", UITheme.Current.diamond, tint, new Rect(rect.center - size / 2, size));
                start[i] = shards[i].rectTransform.anchoredPosition;
                float angle = (i * 360f / count + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(160f, 300f) + new Vector2(0, 110);
                spin[i] = Random.Range(-600f, 600f);
                shards[i].rectTransform.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));
            }
            await Animate(ct, ShardSeconds, t =>
            {
                float seconds = t * ShardSeconds;
                for (int i = 0; i < count; i++)
                {
                    RectTransform shard = shards[i].rectTransform;
                    shard.anchoredPosition = start[i] + velocity[i] * seconds + new Vector2(0, -0.5f * 1100f * seconds * seconds);
                    shard.localEulerAngles += new Vector3(0, 0, spin[i] * Time.deltaTime);
                    Color color = shards[i].color;
                    color.a = 1 - t * t;
                    shards[i].color = color;
                }
            });
            foreach (Image shard in shards)
                Object.Destroy(shard.gameObject);
        }

        //----------------------------------------Helpers----------------------------------------//
        // An image on the overlay's layer, centred at a point of the layer
        private static Image LayerImage(string name, Sprite sprite, Color color, Vector2 center, Vector2 size)
        {
            Image image = UIKit.NewImage(CardMotion.Layer, name, sprite, color);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return image;
        }

        private static Image Overlay(string name, Sprite sprite, Color color, Rect screen_rect)
        {
            Image image = UIKit.NewImage(CardMotion.Layer, name, sprite, color);
            image.raycastTarget = false;
            CardMotion.Place(image.rectTransform, screen_rect);
            return image;
        }

        // A glow around a screen rectangle on the effects layer: the ring's clear middle covers the rectangle
        private const float HaloReach = 32; // the GlowRing sprite's slice border

        private static Image Halo(string name, Color color, Rect screen_rect)
        {
            Image image = Overlay(name, UITheme.Current.glowRing, color, screen_rect);
            image.rectTransform.sizeDelta += Vector2.one * 2 * HaloReach;
            return image;
        }

        private static Rect Grow(Rect rect, float factor)
        {
            Vector2 size = rect.size * factor;
            return new Rect(rect.center - size / 2, size);
        }

        // Destroys an effect's part, unless it went already (the layer cleared at the end of a battle)
        private static void Remove(Component part)
        {
            if (part != null)
                Object.Destroy(part.gameObject);
        }

        // A wait in game time (the pause stops it)
        private static UniTask Wait(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(System.TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
        }

        private static async UniTask Animate(CancellationToken ct, float seconds, System.Action<float> step)
        {
            float age = 0;
            while (age < seconds)
            {
                age += Time.deltaTime;
                step(Mathf.Clamp01(age / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
    }
}
