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

        transform.Translate(0, 10, 0);
        isCardUp = true;
    }

    public void TranslateDown()
    {
        if (!isCardUp)
            return;

        transform.Translate(0, -10, 0);
        isCardUp = false;
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
