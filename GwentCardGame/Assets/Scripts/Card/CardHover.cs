using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardHover : MonoBehaviour
{
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
        Canvas = GameObject.Find("Main Canvas");
    }

    private void OnDestroy()
    {
        DestroyEffect();
    }

    public void OnHoverEnter()
    {
        ShowBigInfo();

        if (isHoverable && !isCardUp)
        {
            TranslateUp();
        }
    }

    public void OnHoverExit()
    {
        DestroyEffect();

        if (isHoverable && isCardUp)
        {
            TranslateDown();
        }
    }

    public void TranslateUp()
    {
        transform.Translate(0, 10, 0);
        isCardUp = true;
    }
    public void TranslateDown()
    {
        transform.Translate(0, -10, 0);
        isCardUp = false;
    }

    public void DestroyEffect()
    {
        Destroy(bigImage);
        Destroy(bigEffect);
    }


    //--------------------------------------------Big info on the right--------------------------------------------//
    private void ShowBigInfo()
    {
        CardStats cardStats = GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        bigImage = Instantiate(BigImage);
        bigEffect = Instantiate(BigEffect);

        bigImage.transform.SetParent(Canvas.transform, false);
        bigEffect.transform.SetParent(Canvas.transform, false);

        bigImage.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/List/591x380/" + cardStats._id);

        if (cardStats.faction == "Special")
        {// Special Card
            bigEffect.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/EffectBox/" + cardStats._idstr);
        }
        else
        {// Unit Card or Leader
            if (cardStats.ability == "leader") // Leader Card
                bigEffect.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/EffectBox/Leader/" + cardStats._id);
            else // Unit Card
            {
                if (cardStats.unique)
                {
                    bigEffect.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/EffectBox/hero");
                }

                if (cardStats.ability != "")
                    bigEffect.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/EffectBox/" + cardStats.ability);
                else if (!cardStats.unique)
                    // No ability (normal unit)
                    if (cardStats.row == "close_range")
                        bigEffect.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/EffectBox/agile");
                    else
                        bigEffect.GetComponent<Image>().sprite = Resources.Load<Sprite>("Cards/EffectBox/normal_unit");
            }
        }

    }
}
