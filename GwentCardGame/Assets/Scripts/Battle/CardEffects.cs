using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Witcher 3's card effects on the board, on the overlay (game time): the sparkle of a card landing, an ability's
// flare over its card (its white icon in an orange blaze), the medic's sign on the discard pile, the hero's landing
// (smoke, and a flame running around the card's edge), the horn's golden glow along its row, the flames of a scorch
// and a lost round's gem breaking. Art: Resources/W3/Effects; the fire, sparks and smoke are drawn here.
public static class CardEffects
{
    public const float SparkleSeconds = 0.35f;
    public const float FlareSeconds = 0.9f;
    public const float HeroSeconds = 1.6f;
    public const float MedicSeconds = 1.15f;
    public const float GlowSeconds = 0.8f;
    public const float BurnSeconds = 0.7f;
    public const float GemPulseSeconds = 0.55f;
    public const float ShardSeconds = 0.75f;

    private static readonly Color Blaze = new Color(1f, 0.55f, 0.15f);
    private static readonly Color FireLine = new Color(1f, 0.66f, 0.18f);
    private static readonly Color FireGlow = new Color(1f, 0.45f, 0.05f, 0.35f);
    private static readonly Color Smoke = new Color(0.42f, 0.42f, 0.44f);

    // The W3 flare of an ability ("muster", "medic", "morale", "bond", "horn", "spy"), or null
    public static string FlareFor(string ability)
    {
        switch (ability)
        {
            case Gwent.Rules.Abilities.Muster: return "muster";
            case Gwent.Rules.Abilities.Medic: return "medic";
            case Gwent.Rules.Abilities.MoraleBoost: return "morale";
            case Gwent.Rules.Abilities.TightBond: return "bond";
            case Gwent.Rules.Abilities.UnitHorn:
            case Gwent.Rules.Abilities.CommanderHorn: return "horn";
            case Gwent.Rules.Abilities.Spy: return "spy";
            default: return null;
        }
    }

    // A soft white light around a card that just landed (around it: its art stays clear)
    public static IEnumerator Sparkle(GameObject card)
    {
        if (card == null)
            yield break;
        Image glow = Halo("Sparkle", new Color(1f, 0.95f, 0.8f, 0), CardMotion.ScreenRect((RectTransform)card.transform));
        yield return Animate(SparkleSeconds, t =>
        {
            float a = t < 0.3f ? t / 0.3f : 1 - (t - 0.3f) / 0.7f;
            glow.color = new Color(1f, 0.95f, 0.8f, 0.9f * a);
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.98f, 1.04f, t);
        });
        Object.Destroy(glow.gameObject);
    }

    // An ability's icon blazing over its card
    public static IEnumerator Flare(GameObject card, string flare)
    {
        if (card == null || flare == null)
            yield break;
        yield return FlareAt(CardMotion.ScreenRect((RectTransform)card.transform), flare, Blaze);
    }

    // The medic's sign on its discard pile (The Witcher 3): the card turns green under a large white heart, in a green
    // glow with green flames licking its left and bottom edges; then the heart flashes out
    public static IEnumerator MedicPile(RectTransform pile_card)
    {
        if (pile_card == null)
            yield break;
        Rect rect = CardMotion.ScreenRect(pile_card);
        Vector2 center, size;
        CardMotion.ToLayout(rect, out center, out size);
        Image glow = Overlay("MedicGlow", UITheme.Current.radial, new Color(0.3f, 1f, 0.45f, 0), Grow(rect, 1.9f));
        Image tint = Overlay("MedicTint", null, new Color(0.15f, 0.85f, 0.3f, 0), rect);
        Image heart = Overlay("Medic", SpriteCache.Load("W3/Effects/medic"), new Color(1, 1, 1, 0), Grow(rect, 1.3f));
        heart.preserveAspect = true;
        List<Particle> wisps = new List<Particle>();
        float age = 0, emit = 0;
        while (age < MedicSeconds)
        {
            float dt = Time.deltaTime;
            age += dt;
            float on = Mathf.Clamp01(age / 0.08f), off = Mathf.Clamp01((age - 0.95f) / 0.2f);
            tint.color = new Color(0.15f, 0.85f, 0.3f, 0.35f * on * (1 - off));
            glow.color = new Color(0.3f, 1f, 0.45f, (0.6f + 0.15f * Mathf.Sin(age * 14)) * on * (1 - off));
            heart.color = new Color(1, 1, 1, on * (1 - off));
            heart.rectTransform.localScale = Vector3.one * (Mathf.Lerp(0.85f, 1f, Mathf.Clamp01(age / 0.12f)) + 0.15f * off);
            // Green flames along the left and bottom edges, curling up and away
            for (emit += (age < 0.95f ? 90 : 0) * dt; emit >= 1; emit--)
            {
                bool left = Random.value < 0.6f;
                Vector2 at = center + (left ? new Vector2(-size.x / 2, Random.Range(-size.y / 2, size.y / 2)) : new Vector2(Random.Range(-size.x / 2, size.x / 2), -size.y / 2));
                Vector2 velocity = left ? new Vector2(Random.Range(-30f, -8f), Random.Range(20f, 60f)) : new Vector2(Random.Range(-25f, 10f), Random.Range(-10f, 25f));
                wisps.Add(Emit(UITheme.Current.radial, at, velocity, Random.Range(0.09f, 0.17f) * size.x, Random.Range(0.35f, 0.6f),
                    new Color(0.65f, 1f, 0.65f, 0.75f), new Color(0.1f, 0.75f, 0.3f, 0.35f), 0.8f));
            }
            Step(wisps, dt, -25);
            yield return null;
        }
        Clear(wisps);
        Object.Destroy(glow.gameObject);
        Object.Destroy(tint.gameObject);
        Object.Destroy(heart.gameObject);
    }

    private static IEnumerator FlareAt(Rect rect, string flare, Color tint)
    {
        Image blaze = Overlay("FlareGlow", UITheme.Current.radial, tint, Grow(rect, 2.0f));
        Image icon = Overlay("Flare", SpriteCache.Load("W3/Effects/" + flare), Color.white, Grow(rect, 0.95f));
        icon.preserveAspect = true;
        yield return Animate(FlareSeconds, t =>
        {
            float a = t < 0.15f ? t / 0.15f : t > 0.65f ? 1 - (t - 0.65f) / 0.35f : 1;
            float pulse = 1 + 0.06f * Mathf.Sin(t * Mathf.PI * 3);
            float grow = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(t / 0.2f)) * pulse;
            blaze.color = new Color(tint.r, tint.g, tint.b, 0.8f * a);
            icon.color = new Color(1, 1, 1, a);
            blaze.rectTransform.localScale = Vector3.one * grow;
            icon.rectTransform.localScale = Vector3.one * grow;
        });
        Object.Destroy(blaze.gameObject);
        Object.Destroy(icon.gameObject);
    }

    // A hero landing, as in The Witcher 3: grey smoke covers the card and a flame runs once around its edge (down the
    // left side, along the bottom, up the right side, back along the top), leaving a glowing outline and throwing
    // sparks; then the smoke clears, the card shows and a few embers drift up
    public static IEnumerator Hero(GameObject card)
    {
        if (card == null)
            yield break;
        Vector2 center, size;
        CardMotion.ToLayout(CardMotion.ScreenRect((RectTransform)card.transform), out center, out size);
        float w = size.x, h = size.y, perimeter = 2 * (w + h);

        // The smoke fills the card: a grey veil its size, darker puffs drifting in it
        Image veil = LayerImage("HeroSmoke", null, new Color(Smoke.r, Smoke.g, Smoke.b, 0), center, size);
        Image[] smoke = new Image[4];
        Vector2[] drift = new Vector2[smoke.Length];
        for (int i = 0; i < smoke.Length; i++)
        {
            Vector2 at = center + new Vector2((i % 2 == 0 ? -0.15f : 0.15f) * w, (i < 2 ? 0.18f : -0.18f) * h);
            smoke[i] = LayerImage("HeroSmoke", UITheme.Current.radial, new Color(0.3f, 0.3f, 0.32f, 0), at, Vector2.one * w * Random.Range(0.75f, 0.95f));
            drift[i] = new Vector2(Random.Range(-5f, 5f), Random.Range(3f, 9f));
        }

        // The outline: a line (and its glow) per edge, each growing from where the flame enters it
        RectTransform frame = new GameObject("HeroFire", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(CardMotion.Layer, false);
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
        Image hot = UIKit.NewImage(frame, "FlameStreak", UITheme.Current.radial, new Color(1f, 0.85f, 0.4f, 0)); // the burning stretch behind the head
        Image flame = LayerImage("Flame", UITheme.Current.radial, new Color(1f, 0.5f, 0.08f, 0), center, Vector2.one * w * 0.6f);
        Image core = LayerImage("FlameCore", UITheme.Current.radial, new Color(1f, 0.95f, 0.7f, 0), center, Vector2.one * w * 0.26f);
        List<Particle> sparks = new List<Particle>(), embers = new List<Particle>();
        float age = 0, emit = 0;
        while (age < HeroSeconds && card != null)
        {
            float dt = Time.deltaTime;
            age += dt;
            // The smoke covers the card, then clears
            float cover = Mathf.Clamp01((age - 0.08f) / 0.1f) * (1 - Mathf.Clamp01((age - 1.22f) / 0.18f));
            veil.color = new Color(Smoke.r, Smoke.g, Smoke.b, 0.82f * cover);
            for (int i = 0; i < smoke.Length; i++)
            {
                smoke[i].color = new Color(0.3f, 0.3f, 0.32f, 0.55f * cover);
                smoke[i].rectTransform.anchoredPosition += drift[i] * dt;
                smoke[i].rectTransform.sizeDelta += Vector2.one * 10 * dt;
            }
            CardMotion.SetAlpha(card, 1 - 0.88f * cover);

            // The flame's way around the edge, and what it has burnt
            float burnt = Mathf.Clamp01((age - 0.17f) / 1f) * perimeter;
            float[] lengths = { Mathf.Clamp(burnt, 0, h), Mathf.Clamp(burnt - h, 0, w), Mathf.Clamp(burnt - h - w, 0, h), Mathf.Clamp(burnt - 2 * h - w, 0, w) };
            float outline = 1 - Mathf.Clamp01((age - 1.2f) / 0.2f);
            for (int e = 0; e < 4; e++)
            {
                bool upright = e % 2 == 0;
                lines[e].rectTransform.sizeDelta = upright ? new Vector2(2.5f, lengths[e]) : new Vector2(lengths[e], 2.5f);
                glows[e].rectTransform.sizeDelta = upright ? new Vector2(9, lengths[e]) : new Vector2(lengths[e], 9);
                lines[e].color = new Color(FireLine.r, FireLine.g, FireLine.b, outline * Random.Range(0.85f, 1f));
                glows[e].color = new Color(FireGlow.r, FireGlow.g, FireGlow.b, FireGlow.a * outline);
            }
            bool burning = age >= 0.17f && age < 1.2f;
            Vector2 head = center + Around(burnt, w, h);
            float fire = burning ? Mathf.Clamp01((age - 0.17f) / 0.05f) : 1 - Mathf.Clamp01((age - 1.2f) / 0.1f);
            flame.rectTransform.anchoredPosition = core.rectTransform.anchoredPosition = head;
            flame.rectTransform.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
            flame.color = new Color(1f, 0.5f, 0.08f, 0.9f * fire);
            core.color = new Color(1f, 0.95f, 0.7f, fire);
            // A streak of bright flame trailing the head along its edge
            float streak = Mathf.Min(0.3f * h, burnt);
            Vector2 tail = Around(Mathf.Max(0, burnt - streak), w, h);
            Vector2 along = head - center - tail;
            RectTransform hot_rect = hot.rectTransform;
            hot_rect.anchorMin = hot_rect.anchorMax = hot_rect.pivot = new Vector2(0.5f, 0.5f);
            hot_rect.anchoredPosition = tail + along / 2;
            hot_rect.sizeDelta = Mathf.Abs(along.x) > Mathf.Abs(along.y) ? new Vector2(Mathf.Abs(along.x) + 8, 12) : new Vector2(12, Mathf.Abs(along.y) + 8);
            hot.color = new Color(1f, 0.85f, 0.4f, 0.9f * fire * Random.Range(0.8f, 1f));

            // Sparks thrown off the flame fall away; once it is out, a few embers drift up
            for (emit += (burning ? 260 : age < 1.45f ? 25 : 0) * dt; emit >= 1; emit--)
            {
                if (burning)
                    sparks.Add(Emit(null, head + Random.insideUnitCircle * 3, Random.insideUnitCircle * 130 + new Vector2(0, 25),
                        Random.Range(2.5f, 4.5f), Random.Range(0.35f, 0.75f), new Color(1f, 0.95f, 0.6f), new Color(1f, 0.45f, 0.05f)));
                else
                    embers.Add(Emit(null, center + new Vector2(Random.Range(-w / 2, w / 2), h / 2), new Vector2(Random.Range(-10f, 10f), Random.Range(25f, 55f)),
                        Random.Range(2f, 3.5f), Random.Range(0.3f, 0.6f), new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.05f)));
            }
            Step(sparks, dt, 220);
            Step(embers, dt, -20);
            yield return null;
        }
        CardMotion.SetAlpha(card, 1);
        Clear(sparks);
        Clear(embers);
        foreach (Image puff in smoke)
            Object.Destroy(puff.gameObject);
        Object.Destroy(veil.gameObject);
        Object.Destroy(frame.gameObject);
        Object.Destroy(flame.gameObject);
        Object.Destroy(core.gameObject);
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
    public static IEnumerator RowGlow(RectTransform row)
    {
        if (row == null)
            yield break;
        Image glow = Halo("HornGlow", new Color(1f, 0.72f, 0.28f, 0), CardMotion.ScreenRect(row));
        yield return Animate(GlowSeconds, t =>
        {
            glow.color = new Color(1f, 0.72f, 0.28f, 0.9f * Mathf.Sin(t * Mathf.PI));
        });
        Object.Destroy(glow.gameObject);
    }

    // Scorch: flames over each card about to be destroyed
    public static IEnumerator Burn(RectTransform[] cards)
    {
        Image[] flames = new Image[cards.Length];
        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null)
            {
                Rect rect = CardMotion.ScreenRect(cards[i]);
                flames[i] = Overlay("Scorch", SpriteCache.Load("W3/Effects/scorch"), new Color(1, 1, 1, 0), Grow(rect, 1.15f));
            }
        yield return Animate(BurnSeconds, t =>
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
    public static IEnumerator Shatter(Image gem, Sprite broken)
    {
        if (gem == null)
            yield break;
        Rect rect = CardMotion.ScreenRect(gem.rectTransform);
        Color red = new Color(0.86f, 0.12f, 0.1f);
        Image glow = Overlay("GemGlow", UITheme.Current.radial, new Color(red.r, red.g, red.b, 0), Grow(rect, 2.6f));
        Image pulse = Overlay("Gem", gem.sprite, Color.white, rect);
        pulse.preserveAspect = true;
        yield return Animate(GemPulseSeconds, t =>
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
        yield return Animate(ShardSeconds, t =>
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

    //----------------------------------------Particles----------------------------------------//
    // Sparks, embers and wisps: small glowing images on the overlay, moved by the effect that throws them. There are
    // many (a hero's fire throws about 250): their images are put away and used again rather than made and destroyed
    // each time, which would leave garbage for the collector to stutter over on phones.
    private class Particle
    {
        public Image Image;
        public Vector2 Velocity;
        public float Age, Life, Size, Growth;
        public Color From, To;
    }

    private const int MaxSpare = 400;
    private static readonly Stack<Image> spare = new Stack<Image>();

    private static Particle Emit(Sprite sprite, Vector2 at, Vector2 velocity, float size, float life, Color from, Color to, float growth = 0)
    {
        return new Particle
        {
            Image = SparkImage(sprite, from, at, size),
            Velocity = velocity, Life = life, Size = size, Growth = growth, From = from, To = to,
        };
    }

    private static Image SparkImage(Sprite sprite, Color color, Vector2 at, float size)
    {
        Image image = null;
        while (image == null && spare.Count > 0)
            image = spare.Pop(); // gone if the layer was cleared since (a battle cut short)
        if (image == null)
            return LayerImage("Spark", sprite, color, at, Vector2.one * size);
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; // as made new
        image.color = color;
        image.rectTransform.anchoredPosition = at;
        image.rectTransform.sizeDelta = Vector2.one * size;
        image.transform.SetAsLastSibling(); // drawn over the effect, as a new image would be
        image.gameObject.SetActive(true);
        return image;
    }

    private static void PutAway(Image image)
    {
        if (image == null)
            return;
        if (spare.Count >= MaxSpare)
        {
            Object.Destroy(image.gameObject);
            return;
        }
        image.gameObject.SetActive(false);
        spare.Push(image);
    }

    // One frame: each particle moves (gravity pulls it down, a negative one lifts it), changes color and fades
    private static void Step(List<Particle> particles, float dt, float gravity)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle particle = particles[i];
            particle.Age += dt;
            if (particle.Age >= particle.Life || particle.Image == null)
            {
                PutAway(particle.Image);
                particles.RemoveAt(i);
                continue;
            }
            float t = particle.Age / particle.Life;
            particle.Velocity.y -= gravity * dt;
            RectTransform rect = particle.Image.rectTransform;
            rect.anchoredPosition += particle.Velocity * dt;
            rect.sizeDelta = Vector2.one * particle.Size * (1 + particle.Growth * t);
            Color color = Color.Lerp(particle.From, particle.To, t);
            color.a *= 1 - t * t;
            particle.Image.color = color;
        }
    }

    private static void Clear(List<Particle> particles)
    {
        foreach (Particle particle in particles)
            PutAway(particle.Image);
        particles.Clear();
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

    private static IEnumerator Animate(float seconds, System.Action<float> step)
    {
        float age = 0;
        while (age < seconds)
        {
            age += Time.deltaTime;
            step(Mathf.Clamp01(age / seconds));
            yield return null;
        }
    }
}
