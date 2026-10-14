using UnityEngine;
using UnityEngine.UI;
using Gwent.Rules;

public class CardHover : MonoBehaviour
{
    private static GameObject mainCanvas;

    [HideInInspector]
    public GameObject Canvas;
    public GameObject BigImage;
    public GameObject BigEffect;

    public GameObject bigImage;
    public GameObject bigEffect;

    public bool isHoverable = true;
    public bool isCardUp = false;

    // How far a hovered or selected card rises (canvas units, so the same at every resolution)
    private const float Lift = 14f;

    private void Start()
    {
        // One lookup per scene instead of one per card
        if (mainCanvas == null)
            mainCanvas = GameObject.Find("Main Canvas");
        Canvas = mainCanvas;
        // Touch controls: a finger held on the card shows it on the right, as hovering does
        LongPress.On(gameObject, ShowHeldPreview, DestroyEffect);
    }

    private bool ShowHeldPreview()
    {
        ShowPreview();
        return previewing == this;
    }

    private void OnDestroy()
    {
        DestroyEffect();
    }

    public void OnHoverEnter()
    {
        if (GameInput.IsTouch)
            return;

        BattleKeys.PointerEntered(gameObject);
        ShowPreview();

        if (isHoverable && !isCardUp)
            TranslateUp();
    }

    public void OnHoverExit()
    {
        if (GameInput.IsTouch)
            return;

        DestroyEffect();

        if (isHoverable && isCardUp)
            TranslateDown();
    }

    // A card lane raises its hovered card itself
    private bool InLane { get { return transform.parent != null && transform.parent.GetComponent<CardLane>() != null; } }

    public void TranslateUp()
    {
        if (isCardUp)
            return;

        if (!InLane)
            transform.localPosition += new Vector3(0, Lift, 0);
        ExtendHitArea(Lift);
        SetFocusEdge(true);
        isCardUp = true;
    }

    public void TranslateDown()
    {
        if (!isCardUp)
            return;

        if (!InLane)
            transform.localPosition -= new Vector3(0, Lift, 0);
        ExtendHitArea(0);
        SetFocusEdge(false);
        isCardUp = false;
    }

    // A raised hand card has a thin gold edge and the light rising across it, as in The Witcher 3
    private const string FocusEdge = "FocusEdge";
    private const string FocusShine = "FocusShine";
    private const string FocusGlow = "FocusGlow";

    private void SetFocusEdge(bool on)
    {
        Transform edge = transform.Find(FocusEdge);
        if (on && edge == null)
        {
            CardShine.Attach((RectTransform)transform, FocusShine, 1f, 3.2f);
            UIKit.Frame(transform, FocusEdge, UITheme.Current.goldBright, 3, 2); // square, as the card is
            // A faint glow around it: behind the art (after the card's shadow), so only its fringe shows
            Image glow = UIKit.NewImage(transform, FocusGlow, UITheme.Current.softShadow, new Color(1f, 0.78f, 0.3f, 0.6f));
            glow.rectTransform.Stretch(-12, -12, -12, -12);
            glow.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
        }
        else if (!on && edge != null)
        {
            foreach (Transform part in new[] { edge, transform.Find(FocusShine), transform.Find(FocusGlow) })
                if (part != null)
                {
                    part.SetParent(null, false);
                    Destroy(part.gameObject);
                }
        }
    }

    // While raised, the card's old footprint stays clickable: otherwise a pointer near its lower edge leaves the
    // raised card, which drops back under the pointer and rises again every frame (and clicks hit the tray behind)
    private void ExtendHitArea(float below)
    {
        float world = below * (transform.parent != null ? transform.parent.lossyScale.y : 1f);
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (!graphic.raycastTarget)
                continue;
            Vector4 padding = graphic.raycastPadding;
            padding.y = -world / Mathf.Max(graphic.transform.lossyScale.y, 0.0001f); // negative padding grows the area
            graphic.raycastPadding = padding;
        }
    }

    public void DestroyEffect()
    {
        Destroy(bigImage);
        Destroy(bigEffect);
        if (previewing == this)
            previewing = null;
    }

    // There is one place for a card on the right: the card whose preview shows there, if any
    private static CardHover previewing;

    // Closes the preview on the right (the showcase takes the place: the opponent's card comes first)
    public static void ClosePreview()
    {
        if (previewing != null)
            previewing.DestroyEffect();
        previewing = null;
    }

    // Replaces any preview this card already shows (a second enter event used to leave a ghost preview).
    // Not during the redraw (the carousel shows the cards big, with their description), nor while a card is in the
    // showcase (it stands there)
    public void ShowPreview()
    {
        if (RedrawPicker.IsOpen || CardShowcase.IsShown)
            return;
        ClosePreview();
        DestroyEffect();
        ShowBigInfo();
        previewing = this;
    }

    //--------------------------------------------Big info on the right--------------------------------------------//
    private void ShowBigInfo()
    {
        if (Canvas == null)
            return;

        CardStats cardStats = GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        bigImage = Instantiate(BigImage);
        bigEffect = Instantiate(BigEffect);

        bigImage.transform.SetParent(Canvas.transform, false);
        bigEffect.transform.SetParent(Canvas.transform, false);

        bigImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/591x380/" + cardStats._id);
        bigEffect.GetComponent<Image>().sprite = SpriteCache.Load(EffectBoxPath(cardStats));
        // Only a preview: it must never catch clicks meant for what is under it
        foreach (Graphic graphic in bigImage.GetComponentsInChildren<Graphic>())
            graphic.raycastTarget = false;
        foreach (Graphic graphic in bigEffect.GetComponentsInChildren<Graphic>())
            graphic.raycastTarget = false;
    }

    // Description box shown next to the big card (also used by the deck builder and the redraw carousel)
    public static string EffectBoxPath(CardStats cardStats)
    {
        return EffectBoxPath(cardStats.faction, cardStats._idstr, cardStats.ability, cardStats._id, cardStats.unique, cardStats.row);
    }

    public static string EffectBoxPath(Card card)
    {
        return EffectBoxPath(card.faction, card._idstr, card.ability, card._id, card.unique, card.row);
    }

    private static string EffectBoxPath(string faction, string idstr, string ability, int id, bool unique, string row)
    {
        if (faction == Factions.Special)
            return "Cards/EffectBox/" + idstr;
        if (ability == Abilities.Leader)
            return "Cards/EffectBox/Leader/" + id;
        if (!string.IsNullOrEmpty(ability))
            return "Cards/EffectBox/" + ability;
        if (unique)
            return "Cards/EffectBox/hero";
        if (row == Rows.Agile)
            return "Cards/EffectBox/agile";
        return "Cards/EffectBox/normal_unit";
    }
}
