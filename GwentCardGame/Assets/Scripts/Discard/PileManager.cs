using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PileManager : MonoBehaviour
{
    private GameObject pile;

    public void ClearDiscardPile()
    {
        if (pile == null)
            pile = transform.Find("Pile").gameObject;
        GameObject pile_go = pile;
        for (int i = 0; i < pile_go.transform.childCount; i++)
        {
            Destroy(pile_go.transform.GetChild(i).gameObject);
        }
        pile_go.transform.DetachChildren();
    }
}
