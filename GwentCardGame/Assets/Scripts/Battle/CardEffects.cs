using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The Witcher 3's card effects on the board, on the overlay (game time): the sparkle of a card landing, an ability's
// flare over its card (its white icon in an orange blaze), the medic's flare over the discard pile, the hero's
// landing (dark smoke in a fiery outline), the horn's golden glow along its row and the flames of a scorch.
// Art: Resources/W3/Effects.
public static class CardEffects
{
    public const float SparkleSeconds = 0.35f;
    public const float FlareSeconds = 0.9f;
    public const float HeroSeconds = 0.9f;
    public const float GlowSeconds = 0.8f;
    public const float BurnSeconds = 0.7f;

    private static readonly Color Blaze = new Color(1f, 0.55f, 0.15f);
    private static readonly Color Heal = new Color(0.35f, 1f, 0.45f);

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

    // A soft white light around a card that just landed
    public static IEnumerator Sparkle(GameObject card)
    {
        if (card == null)
            yield break;
        Rect rect = CardMotion.ScreenRect((RectTransform)card.transform);
        Image glow = Overlay("Sparkle", UITheme.Current.softShadow, Color.white, Grow(rect, 1.25f));
        yield return Animate(SparkleSeconds, t =>
        {
            float a = t < 0.3f ? t / 0.3f : 1 - (t - 0.3f) / 0.7f;
            glow.color = new Color(1, 1, 1, 0.6f * a);
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1.08f, t);
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

    // The medic's flare over the discard pile it restores from
    public static IEnumerator PileFlare(RectTransform pile)
    {
        if (pile == null)
            yield break;
        yield return FlareAt(CardMotion.ScreenRect(pile), "medic", Heal);
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

    // A hero landing: dark smoke in a fiery outline over the card, which shows through as the smoke clears
    public static IEnumerator Hero(GameObject card)
    {
        if (card == null)
            yield break;
        Rect rect = CardMotion.ScreenRect((RectTransform)card.transform);
        Image smoke = Overlay("HeroSmoke", UITheme.Current.radial, new Color(0, 0, 0, 0), Grow(rect, 1.5f));
        Image frame = Overlay("HeroFrame", SpriteCache.Load("W3/Effects/hero"), new Color(1, 1, 1, 0), Grow(rect, 1.12f));
        CardMotion.Hide(card, true);
        yield return Animate(HeroSeconds, t =>
        {
            smoke.color = new Color(0, 0, 0, 0.75f * (t < 0.25f ? t / 0.25f : 1 - (t - 0.25f) / 0.75f));
            smoke.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.15f, t);
            frame.color = new Color(1, 1, 1, t < 0.2f ? t / 0.2f : t > 0.7f ? 1 - (t - 0.7f) / 0.3f : 1);
            CanvasGroup group = card != null ? card.GetComponent<CanvasGroup>() : null;
            if (group != null)
                group.alpha = Mathf.Clamp01((t - 0.35f) / 0.4f);
        });
        CardMotion.Hide(card, false);
        Object.Destroy(smoke.gameObject);
        Object.Destroy(frame.gameObject);
    }

    // A horn's warm golden glow along its row
    public static IEnumerator RowGlow(RectTransform row)
    {
        if (row == null)
            yield break;
        Image glow = Overlay("HornGlow", UITheme.Current.softShadow, new Color(1f, 0.78f, 0.35f, 0), Grow(CardMotion.ScreenRect(row), 1.04f));
        yield return Animate(GlowSeconds, t =>
        {
            glow.color = new Color(1f, 0.78f, 0.35f, 0.55f * Mathf.Sin(t * Mathf.PI));
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

    //----------------------------------------Helpers----------------------------------------//
    private static Image Overlay(string name, Sprite sprite, Color color, Rect screen_rect)
    {
        Image image = UIKit.NewImage(UIOverlay.FixedRoot, name, sprite, color);
        image.raycastTarget = false;
        CardMotion.Place(image.rectTransform, screen_rect);
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
