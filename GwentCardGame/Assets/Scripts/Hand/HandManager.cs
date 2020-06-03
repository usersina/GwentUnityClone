using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    public void ClearHandObject()
    {
        for (int i=0; i< transform.childCount; i++)
        {
            //transform.GetChild(i).gameObject.BigImage;
            Destroy(transform.GetChild(i).GetComponent<CardHover>().bigImage);
            Destroy(transform.GetChild(i).GetComponent<CardHover>().bigEffect);
            Destroy(transform.GetChild(i).gameObject);
        }
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        transform.DetachChildren();
        //Debug.Log("THE CHILD COUNT AFTER CLEAR: " + transform.childCount); // SHOULD BE 0
    }

    public void ResizeHand()
    {
        //Debug.Log("Resizing your hand V2...");
        // Initial Spacing
        gameObject.GetComponent<GridLayoutGroup>().spacing = new Vector2(6, 0);

        float maxWidth = gameObject.GetComponent<RectTransform>().rect.width;
        float spacingX = gameObject.GetComponent<GridLayoutGroup>().spacing.x;
        int numberOfCards = 0;
        float cardWidth = 0;

        //Debug.Log("Current number of children: " + transform.childCount);
        foreach (Transform child in transform)
        {
            numberOfCards += 1;
            RectTransform rt = (RectTransform)child.transform;
            cardWidth = rt.rect.width;
        }

        float handWidth = numberOfCards * cardWidth + (numberOfCards - 1) * spacingX;
        float offset = 50;
        if (handWidth > maxWidth - offset)
        {
            spacingX = (maxWidth - offset - numberOfCards * cardWidth) / numberOfCards - 1;
            gameObject.GetComponent<GridLayoutGroup>().spacing = new Vector2(spacingX, 0);
        }
    }
}
