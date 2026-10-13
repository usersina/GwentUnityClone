using UnityEngine;
using UnityEngine.UI;

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
    }

    private void OnDestroy()
    {
        DestroyEffect();
    }

    public void OnHoverEnter()
    {
        if (Application.isMobilePlatform)
            return;

        ShowPreview();

        if (isHoverable && !isCardUp)
            TranslateUp();
    }

    public void OnHoverExit()
    {
        if (Application.isMobilePlatform)
            return;

        DestroyEffect();

        if (isHoverable && isCardUp)
            TranslateDown();
    }

    public void TranslateUp()
    {
        if (isCardUp)
            return;

        transform.localPosition += new Vector3(0, Lift, 0);
        ExtendHitArea(Lift);
        isCardUp = true;
    }

    public void TranslateDown()
    {
        if (!isCardUp)
            return;

        transform.localPosition -= new Vector3(0, Lift, 0);
        ExtendHitArea(0);
        isCardUp = false;
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
    }

    // Replaces any preview this card already shows (a second enter event used to leave a ghost preview)
    public void ShowPreview()
    {
        DestroyEffect();
        ShowBigInfo();
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

    // Description box shown next to the big card (also used by the deck builder)
    public static string EffectBoxPath(CardStats cardStats)
    {
        if (cardStats.faction == "Special")
            return "Cards/EffectBox/" + cardStats._idstr;
        if (cardStats.ability == "leader")
            return "Cards/EffectBox/Leader/" + cardStats._id;
        if (!string.IsNullOrEmpty(cardStats.ability))
            return "Cards/EffectBox/" + cardStats.ability;
        if (cardStats.unique)
            return "Cards/EffectBox/hero";
        if (cardStats.row == "close_range")
            return "Cards/EffectBox/agile";
        return "Cards/EffectBox/normal_unit";
    }
}
