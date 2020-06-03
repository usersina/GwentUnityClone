using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PileManager : MonoBehaviour
{
    public void ClearDiscardPile()
    {
        GameObject pile_go = gameObject.transform.Find("Pile").gameObject;
        for (int i = 0; i < pile_go.transform.childCount; i++)
        {
            Destroy(pile_go.transform.GetChild(i).gameObject);
        }
        pile_go.transform.DetachChildren();
    }
}
